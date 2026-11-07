#!/usr/bin/env node
import { type ChildProcess, spawn } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { parseArgs } from "node:util";
import { killGroup, messageOf } from "./run.ts";
import { guardWorker } from "./windows.ts";

const USAGE = `Usage: statechart SPEC.tla [--config FILE] [--view FILE] [--out DIR]

Checks SPEC.tla with TLC and draws its state graph as Mermaid. Above 500 states, the chart
draws only the paths to the rules that break, and with no rule broken there is no chart.mmd.
  --config FILE  TLC config. Default: SPEC.cfg beside the spec.
  --view FILE    A module that exports state(vars) and, if you want, step(action, from, to),
                 group(vars), colors, and title. state() returns a string for a flat chart, an
                 array for parent states, or an object for regions. Default: each state's values.
                 colors gives each group blue, orange, purple, yellow, teal, tan, or pink.
  --out DIR      Where to write report.md, chart.mmd, tlc.out, and tlc-RULE.out for each
                 property that breaks and each rule that breaks off the chart. The folder must
                 be new or empty. Default: SPEC-chart.
Exit code: TLC's own on the config as written: 0 when every rule holds, 1 when a rule breaks,
and 2 when TLC stops with an error, a signal kills the check, or the command cannot run. 130 or
143 when SIGINT or SIGTERM stops the check. Only exits 0 and 1 write output.`;

let stop: NodeJS.Signals | undefined;
const stopped = new Promise<void>((resolve) => {
  for (const name of ["SIGINT", "SIGTERM"] as const)
    process.on(name, () => {
      stop ??= name;
      resolve();
    });
});

const codeOf = (error: unknown) =>
  error instanceof Error && "code" in error ? error.code : undefined;

function destination(out: string) {
  let entries: string[] = [];
  let dest: string;
  try {
    entries = fs.readdirSync(out);
    dest = fs.realpathSync(out);
  } catch (error) {
    if (codeOf(error) !== "ENOENT")
      throw new Error(
        `${out} cannot be the output folder. ${messageOf(error)}. Pass --out with a new folder.`,
        { cause: error },
      );
    if (fs.lstatSync(out, { throwIfNoEntry: false }))
      throw new Error(
        `${out} cannot be the output folder. It is a broken link. Pass --out with a new folder.`,
        { cause: error },
      );
    dest = path.resolve(out);
  }
  if (entries.length)
    throw new Error(
      `${out} is not empty. It has ${entries.sort()[0]}. The command writes only to a new or empty folder. Pass --out with a new folder.`,
    );
  return dest;
}

type Flags = { config?: string; view?: string; out?: string };

function parseCommand(): { flags: Flags; specFile: string } {
  try {
    const args = parseArgs({
      allowPositionals: true,
      options: { config: { type: "string" }, view: { type: "string" }, out: { type: "string" } },
    });
    if (args.positionals.length !== 1) throw new Error("Give one SPEC.tla.");
    return { flags: args.values, specFile: args.positionals[0] };
  } catch (error) {
    console.error(`${messageOf(error)}\n\n${USAGE}`);
    process.exit(2);
  }
}

type Run = {
  stage: string;
  tmp?: string;
  janitor?: ReturnType<typeof startJanitor>;
  closeGuard?: () => Promise<void>;
};

function startWorker(stage: string, spec: string, config: string, tmp: string, view?: string) {
  const runtime = process.versions.deno
    ? ["run", "--quiet", "--no-prompt", "--allow-read", "--allow-write", "--allow-run", "--allow-env"]
    : process.execArgv;
  return spawn(process.execPath, [
    ...runtime, path.join(import.meta.dirname, "worker.ts"), stage, spec, config, ...(view ? [view] : []),
  ], {
    detached: true,
    windowsHide: true,
    stdio: [host.workerStdin, "inherit", "inherit"],
    env: { ...process.env, ...host.temp(tmp) },
  });
}

function startJanitor(worker: number, stage: string, tmp: string) {
  const child = spawn(
    "/bin/sh",
    [
      "-c",
      'while read -r line; do :; done; kill -s KILL -- "-$1"; rm -rf -- "$2" "$3"',
      "sh",
      String(worker),
      stage,
      tmp,
    ],
    { detached: true, stdio: ["pipe", "ignore", "ignore"] },
  );
  let failure: Error | undefined;
  let closing = false;
  let finished = false;
  const closed = new Promise<Error | undefined>((resolve) => {
    child.on("error", (error) => { failure ??= error; });
    child.once("close", (code, signal) => {
      finished = true;
      if (!closing || signal !== "SIGKILL")
        failure ??= new Error(`Statechart cleanup process closed early (${signal ?? code}).`);
      resolve(failure);
    });
  });
  return {
    closed,
    check() {
      if (failure) throw failure;
    },
    async drain() {
      let killError: unknown;
      if (!finished) {
        closing = true;
        try { child.kill("SIGKILL"); }
        catch (error) { killError = error; }
      }
      const error = await closed;
      const errors = [killError, error].filter((value) => value !== undefined);
      if (errors.length) throw new AggregateError(errors, errors.map(messageOf).join("\n"));
    },
  };
}

async function superviseOnPosix(worker: ChildProcess, exited: Promise<number>, run: Run) {
  // Retain a worker spawn error even when janitor setup fails before the first await.
  const settled = exited.then(() => undefined, (error: unknown) => error);
  try {
    if (worker.pid === undefined) {
      const error = await settled;
      throw error ?? new Error("Statechart worker did not start.");
    }
    run.janitor = startJanitor(worker.pid, run.stage, run.tmp as string);
    await Promise.race([stopped, exited, run.janitor.closed.then((error) => {
      throw error ?? new Error("Statechart cleanup process closed before drain.");
    })]);
  } catch (error) {
    const errors = [error];
    try { if (worker.pid !== undefined) await killGroup(worker.pid); }
    catch (cleanupError) { errors.push(cleanupError); }
    const workerError = await settled;
    if (workerError !== undefined && workerError !== error) errors.push(workerError);
    throw new AggregateError(errors, errors.map(messageOf).join("\n"));
  }
  await killGroup(worker.pid);
}

async function cleanupOnPosix(run: Run, failure: unknown) {
  const errors: unknown[] = [];
  for (const folder of [run.stage, run.tmp]) {
    if (!folder) continue;
    try { fs.rmSync(folder, { recursive: true, force: true }); }
    catch (error) { errors.push(error); }
  }
  try { await run.janitor?.drain(); }
  catch (error) { errors.push(error); }
  if (!errors.length) return;
  if (failure !== undefined) errors.unshift(failure);
  throw new AggregateError(errors, errors.map(messageOf).join("\n"));
}

async function superviseOnWindows(worker: ChildProcess, exited: Promise<number>, run: Run) {
  run.closeGuard = await guardWorker(worker, run.stage, run.tmp as string);
  await Promise.race([stopped, exited]);
  if (stop) await run.closeGuard();
}

async function cleanupOnWindows(run: Run) {
  if (run.closeGuard) await run.closeGuard();
  fs.rmSync(run.stage, { recursive: true, force: true });
  if (run.tmp) fs.rmSync(run.tmp, { recursive: true, force: true });
}

function showReport(dest: string, out: string) {
  process.stdout.write(fs.readFileSync(path.join(dest, "report.md")));
  console.log(`\nWrote ${out}`);
}

type Host = {
  workerStdin: "ignore" | "pipe";
  temp(tmp: string): NodeJS.ProcessEnv;
  supervise(worker: ChildProcess, exited: Promise<number>, run: Run): Promise<void>;
  cleanup(run: Run, failure: unknown): Promise<void>;
  replaceEmpty(dest: string): void;
  published(dest: string, out: string): void;
  cleaned(dest: string, out: string): void;
};

// POSIX holds the worker's process group behind a janitor and shows the report once cleanup has
// drained it. Windows holds it in a Job Object, and shows the report as soon as it is published.
const posix: Host = {
  workerStdin: "ignore",
  temp: (tmp: string) => ({ TMPDIR: tmp }),
  supervise: superviseOnPosix,
  cleanup: cleanupOnPosix,
  replaceEmpty: () => {},
  published: () => {},
  cleaned: showReport,
};
const windows: Host = {
  workerStdin: "pipe",
  temp: (tmp: string) => ({ TMPDIR: tmp, TEMP: tmp, TMP: tmp }),
  supervise: superviseOnWindows,
  cleanup: cleanupOnWindows,
  // Windows cannot replace even an empty directory with rename. rmdir is
  // deliberately non-recursive: a file arriving during the run is kept.
  replaceEmpty: (dest: string) => { if (fs.existsSync(dest)) fs.rmdirSync(dest); },
  published: showReport,
  cleaned: () => {},
};
const host = process.platform === "win32" ? windows : posix;

function publish(done: string, dest: string, out: string) {
  fs.mkdirSync(path.dirname(dest), { recursive: true });
  try {
    host.replaceEmpty(dest);
    fs.renameSync(done, dest);
  } catch (error) {
    if (codeOf(error) !== "ENOTEMPTY" && codeOf(error) !== "EEXIST") throw error;
    throw new Error(
      `${path.join(out, fs.readdirSync(dest).sort()[0])} appeared while the check ran, so the command wrote nothing. Pass --out with a new folder.`,
      { cause: error },
    );
  }
  host.published(dest, out);
}

function outcomeOf(worker: ChildProcess, verdict: number, done: string, dest: string, out: string) {
  if (stop) {
    console.error(`${stop} stopped the check, so the command wrote nothing.`);
    return { exitCode: 128 + os.constants.signals[stop], published: false };
  }
  if (worker.signalCode) {
    console.error(`${worker.signalCode} killed the check, so the command wrote nothing.`);
    return { exitCode: 2, published: false };
  }
  if ((verdict === 0 || verdict === 1) && fs.existsSync(done)) {
    publish(done, dest, out);
    return { exitCode: verdict, published: true };
  }
  return { exitCode: 2, published: false };
}

async function checkSpec(specFile: string, flags: Flags) {
  const configFile = flags.config ?? specFile.replace(/\.tla$/, ".cfg");
  if (!fs.existsSync(configFile))
    throw new Error(`Cannot find the TLC config ${configFile}. Pass --config FILE.`);
  const out = flags.out ?? specFile.replace(/\.tla$/, "-chart");
  const dest = destination(out);

  let base = path.dirname(dest);
  while (!fs.existsSync(base)) base = path.dirname(base);
  const run: Run = { stage: fs.mkdtempSync(path.join(base, `.${path.basename(dest)}.statechart-`)) };
  let failure: unknown;
  let outcome: ReturnType<typeof outcomeOf>;
  try {
    run.tmp = fs.mkdtempSync(path.join(os.tmpdir(), "statechart-"));
    const worker = startWorker(run.stage, specFile, configFile, run.tmp, flags.view);
    const exited = new Promise<number>((resolve, reject) => {
      worker.on("error", reject);
      worker.on("exit", (code) => resolve(code ?? 2));
    });
    await host.supervise(worker, exited, run);
    const verdict = await exited;
    run.janitor?.check();
    outcome = outcomeOf(worker, verdict, path.join(run.stage, "done"), dest, out);
  } catch (error) {
    failure = error;
    throw error;
  } finally {
    await host.cleanup(run, failure);
  }
  if (outcome.published) host.cleaned(dest, out);
  return outcome.exitCode;
}

const command = parseCommand();
try {
  process.exit(await checkSpec(command.specFile, command.flags));
} catch (error) {
  console.error(messageOf(error));
  process.exit(2);
}
