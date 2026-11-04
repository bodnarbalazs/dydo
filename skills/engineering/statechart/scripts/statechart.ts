#!/usr/bin/env node
import { spawn } from "node:child_process";
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

let flags;
let specFile;
try {
  const args = parseArgs({
    allowPositionals: true,
    options: { config: { type: "string" }, view: { type: "string" }, out: { type: "string" } },
  });
  flags = args.values;
  [specFile] = args.positionals;
  if (args.positionals.length !== 1) throw new Error("Give one SPEC.tla.");
} catch (error) {
  console.error(`${messageOf(error)}\n\n${USAGE}`);
  process.exit(2);
}

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

function startWorker(stage: string, spec: string, config: string, tmp: string, view?: string) {
  const runtime = process.versions.deno
    ? ["run", "--quiet", "--no-prompt", "--allow-read", "--allow-write", "--allow-run", "--allow-env"]
    : process.execArgv;
  return spawn(process.execPath, [
    ...runtime, path.join(import.meta.dirname, "worker.ts"), stage, spec, config, ...(view ? [view] : []),
  ], {
    detached: true,
    windowsHide: true,
    stdio: [process.platform === "win32" ? "pipe" : "ignore", "inherit", "inherit"],
    env: { ...process.env, TMPDIR: tmp, ...(process.platform === "win32" ? { TEMP: tmp, TMP: tmp } : {}) },
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

async function cleanupPosix(stage: string, tmp: string | undefined, janitor?: ReturnType<typeof startJanitor>) {
  const errors: unknown[] = [];
  for (const folder of [stage, tmp]) {
    if (!folder) continue;
    try { fs.rmSync(folder, { recursive: true, force: true }); }
    catch (error) { errors.push(error); }
  }
  try { await janitor?.drain(); }
  catch (error) { errors.push(error); }
  return errors;
}

function showReport(dest: string, out: string) {
  process.stdout.write(fs.readFileSync(path.join(dest, "report.md")));
  console.log(`\nWrote ${out}`);
}

function publish(done: string, dest: string, out: string) {
  fs.mkdirSync(path.dirname(dest), { recursive: true });
  try {
    // Windows cannot replace even an empty directory with rename. rmdir is
    // deliberately non-recursive: a file arriving during the run is kept.
    if (process.platform === "win32" && fs.existsSync(dest)) fs.rmdirSync(dest);
    fs.renameSync(done, dest);
  } catch (error) {
    if (codeOf(error) !== "ENOTEMPTY" && codeOf(error) !== "EEXIST") throw error;
    throw new Error(
      `${path.join(out, fs.readdirSync(dest).sort()[0])} appeared while the check ran, so the command wrote nothing. Pass --out with a new folder.`,
      { cause: error },
    );
  }
  if (process.platform === "win32") showReport(dest, out);
}

try {
  const configFile = flags.config ?? specFile.replace(/\.tla$/, ".cfg");
  if (!fs.existsSync(configFile))
    throw new Error(`Cannot find the TLC config ${configFile}. Pass --config FILE.`);
  const out = flags.out ?? specFile.replace(/\.tla$/, "-chart");
  const dest = destination(out);

  let base = path.dirname(dest);
  while (!fs.existsSync(base)) base = path.dirname(base);
  const stage = fs.mkdtempSync(path.join(base, `.${path.basename(dest)}.statechart-`));
  const done = path.join(stage, "done");
  let tmp: string | undefined;
  let janitor: ReturnType<typeof startJanitor> | undefined;
  let closeGuard: (() => Promise<void>) | undefined;
  let failure: unknown;
  let published = false;
  let exitCode: number;
  try {
    tmp = fs.mkdtempSync(path.join(os.tmpdir(), "statechart-"));
    const worker = startWorker(stage, specFile, configFile, tmp, flags.view);
    const exited = new Promise<number>((resolve, reject) => {
      worker.on("error", reject);
      worker.on("exit", (code) => resolve(code ?? 2));
    });
    if (process.platform === "win32") closeGuard = await guardWorker(worker, stage, tmp);
    else {
      // Retain a worker spawn error even when janitor setup fails before the first await.
      const settled = exited.then(() => undefined, (error: unknown) => error);
      try {
        if (worker.pid === undefined) {
          const error = await settled;
          throw error ?? new Error("Statechart worker did not start.");
        }
        janitor = startJanitor(worker.pid, stage, tmp);
        await Promise.race([stopped, exited, janitor.closed.then((error) => {
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
    }
    if (process.platform === "win32") await Promise.race([stopped, exited]);
    if (closeGuard) {
      if (stop) await closeGuard();
    } else await killGroup(worker.pid as number);
    const verdict = await exited;
    janitor?.check();
    if (stop) {
      console.error(`${stop} stopped the check, so the command wrote nothing.`);
      exitCode = 128 + os.constants.signals[stop];
    } else if (worker.signalCode) {
      console.error(`${worker.signalCode} killed the check, so the command wrote nothing.`);
      exitCode = 2;
    } else if ((verdict === 0 || verdict === 1) && fs.existsSync(done)) {
      publish(done, dest, out);
      published = true;
      exitCode = verdict;
    } else exitCode = 2;
  } catch (error) {
    failure = error;
    throw error;
  } finally {
    if (process.platform === "win32") {
      if (closeGuard) await closeGuard();
      fs.rmSync(stage, { recursive: true, force: true });
      if (tmp) fs.rmSync(tmp, { recursive: true, force: true });
    } else {
      const errors = await cleanupPosix(stage, tmp, janitor);
      if (errors.length) {
        if (failure !== undefined) errors.unshift(failure);
        throw new AggregateError(errors, errors.map(messageOf).join("\n"));
      }
    }
  }
  if (process.platform !== "win32" && published) showReport(dest, out);
  process.exit(exitCode);
} catch (error) {
  console.error(messageOf(error));
  process.exit(2);
}
