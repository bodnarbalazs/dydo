import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";
import { pathToFileURL } from "node:url";

const signal = process.env.STATECHART_DIAG_SIGNAL;
assert.ok(signal === "SIGINT" || signal === "SIGTERM");
const scripts = "/toolchain/repo/skills/engineering/statechart/scripts";
const base = `/output/signal-${signal}`;
const eventsFile = `${base}/events.jsonl`;
const janitorScript = 'while read -r line; do :; done; kill -s KILL -- "-$1"; rm -rf -- "$2" "$3"';

let observationError;
let wakeFailure;
const record = (value) => {
  try {
    if (fs.existsSync(eventsFile) && fs.statSync(eventsFile).size >= 16 * 1024 * 1024)
      throw new Error("Observation file limit reached");
    fs.appendFileSync(eventsFile,
      JSON.stringify({ monotonicNs: String(process.hrtime.bigint()), observer: "fixture", pid: process.pid, ...value }) + "\n");
  } catch (error) {
    observationError ??= error;
    wakeFailure?.(error);
  }
};
const events = () => fs.readFileSync(eventsFile, "utf8").trim().split("\n").filter(Boolean).map(JSON.parse);
function identity(pid) {
  const raw = fs.readFileSync(`/proc/${pid}/stat`, "utf8");
  const fields = raw.slice(raw.lastIndexOf(") ") + 2).split(" ");
  const argv = fs.readFileSync(`/proc/${pid}/cmdline`).toString().split("\0").filter(Boolean);
  const confirm = fs.readFileSync(`/proc/${pid}/stat`, "utf8");
  assert.equal(confirm.slice(confirm.lastIndexOf(") ") + 2).split(" ")[19], fields[19]);
  return { pid, raw, starttime: fields[19], ppid: Number(fields[1]), pgid: Number(fields[2]),
    session: Number(fields[3]), state: fields[0], argv };
}

function within(promise, milliseconds) {
  let timer;
  const expired = new Promise((resolve) => { timer = setTimeout(() => resolve({ expired: true }), milliseconds); });
  return Promise.race([promise, expired]).finally(() => clearTimeout(timer));
}

// Only the two witnessed direct CLI children qualify. Never infer ownership from PID absence.
function cancelDescendants(cliPid) {
  const spawned = events().filter((event) => event.observer === "cli" && event.parent === cliPid
    && event.event === "spawn" && ["worker", "janitor"].includes(event.role));
  assert.equal(spawned.length, 2, "Cannot prove both CLI children for cancellation");
  const worker = spawned.find((event) => event.role === "worker");
  const janitor = spawned.find((event) => event.role === "janitor");
  assert.ok(worker?.pid && worker.identity?.starttime && janitor?.pid && janitor.identity?.starttime);
  assert.equal(worker.file, process.execPath);
  assert.ok(worker.args.includes(`${scripts}/worker.ts`));
  assert.deepEqual(janitor.args.slice(0, 4), ["-c", janitorScript, "sh", String(worker.pid)]);
  for (const child of [worker, janitor]) {
    const alreadyClosed = events().some((event) => event.observer === "cli" && event.parent === cliPid
      && event.event === "close" && event.pid === child.pid && event.role === child.role);
    if (alreadyClosed) { record({ event: "cancel-child-already-closed", role: child.role, pid: child.pid }); continue; }
    let current;
    try { current = identity(child.pid); }
    catch (error) {
      // Missing proc data is uncertainty, not a synthetic close. Do not signal an unproved PID.
      record({ event: "cancel-identity-unavailable", role: child.role, pid: child.pid, message: error.message });
      throw error;
    }
    assert.equal(current.starttime, child.identity.starttime);
    assert.equal(current.ppid, cliPid);
    assert.equal(current.pgid, child.pid);
    assert.equal(current.session, child.pid);
    if (current.state !== "Z") {
      assert.deepEqual(current.argv, [child.file, ...child.args]);
      record({ event: "cancel-group-send", signal: "SIGKILL", role: child.role, identity: current });
      process.kill(-child.pid, "SIGKILL");
      record({ event: "cancel-group-send-return", role: child.role, pid: child.pid });
    } else record({ event: "cancel-owned-zombie-not-reaped", role: child.role, identity: current });
  }
}

test(`diagnose original fs.watch controlled ${signal} boundary`, { timeout: 50_000 }, async () => {
  fs.mkdirSync(base);
  // The watched directory remains a Windows-host bind. All instrumentation logs are OUTSIDE it.
  const proof = `${base}/proof`;
  const parent = `${proof}/parent`;
  const tmpdir = `${proof}/tmp`;
  fs.mkdirSync(parent, { recursive: true });
  fs.mkdirSync(tmpdir);
  const ready = `${proof}/ready`;
  const view = `${proof}/view.mjs`;
  const fixture = JSON.parse(fs.readFileSync("/input/input-manifest.json", "utf8")).generatedViews[signal];
  fs.writeFileSync(view, fixture.content);
  record({ event: "fixture-start", signal, watched: proof });
  let child;
  let cliIdentity;
  let signalled = false;
  let readyObserved = false;
  let failure;
  const failed = new Promise((resolve) => { wakeFailure = resolve; });
  const watcher = fs.watch(proof, (eventType, filename) => {
    record({ event: "original-watch-callback", eventType, filename: filename?.toString(), readyExists: fs.existsSync(ready) });
    if (!signalled && fs.existsSync(ready)) {
      signalled = true;
      watcher.close();
      try {
        record({ event: "original-signal-send-attempt", signal, child: child ? identity(child.pid) : null });
        const result = child?.kill(signal);
        record({ event: "original-signal-send-return", signal, result });
        assert.ok(result);
      } catch (error) { record({ event: "original-signal-send-error", message: error.message }); wakeFailure(error); }
    }
  });
  watcher.on("error", (error) => { record({ event: "watcher-error", message: error.message }); wakeFailure(error); });
  // Observation only: this polling NEVER sends a signal or substitutes for the original callback.
  const observeReady = setInterval(() => {
    try {
      if (!readyObserved && fs.existsSync(ready)) {
        readyObserved = true;
        record({ event: "ready-observed", stat: { bytes: fs.statSync(ready).size } });
      }
    } catch (error) { observationError ??= error; wakeFailure(error); }
  }, 2);
  let directClosed = false;
  let directExited = false;
  let closePromise;
  let stdout = "";
  let stderr = "";
  try {
    const argv = ["--import", pathToFileURL(path.join(import.meta.dirname, "preload.mjs")).href,
      `${scripts}/statechart.ts`, `${scripts}/test/corpus/Counter/Counter.tla`, "--view", view, "--out", `${parent}/out`];
    record({ event: "cli-spawn-attempt", file: process.execPath, argv });
    child = spawn(process.execPath, argv, {
      env: { ...process.env, JAVA: `${scripts}/test/java.ts`, TLA2TOOLS: "/input/tla2tools.jar",
        STATECHART_REPLAY: `${scripts}/test/corpus/Counter`, TMPDIR: tmpdir, STATECHART_DIAG_EVENTS: eventsFile },
      stdio: ["ignore", "pipe", "pipe"],
    });
    closePromise = new Promise((resolve) => {
      child.on("error", (error) => { record({ event: "cli-error", message: error.message }); wakeFailure(error); });
      child.once("exit", (code, killed) => { directExited = true; record({ event: "cli-direct-exit", code, signal: killed }); });
      child.once("close", (code, killed) => { directClosed = true; record({ event: "cli-direct-close", code, signal: killed }); resolve({ code, killed }); });
    });
    cliIdentity = child.pid ? identity(child.pid) : null;
    record({ event: "cli-owned", identity: cliIdentity });
    const capture = (name, chunk) => {
      try {
        if ((name === "stdout" ? stdout.length : stderr.length) + chunk.length > 16 * 1024 * 1024)
          throw new Error("CLI output bound exceeded");
        if (name === "stdout") stdout += chunk;
        else stderr += chunk;
        fs.appendFileSync(`${base}/cli.${name}`, chunk);
      } catch (error) { observationError ??= error; wakeFailure(error); }
    };
    child.stdout.on("data", (chunk) => capture("stdout", chunk));
    child.stderr.on("data", (chunk) => capture("stderr", chunk));
    const outcome = await within(Promise.race([closePromise, failed]), 30_000);
    if (outcome?.expired) throw new Error("Original signal boundary exceeded 30000ms");
    if (outcome instanceof Error) throw outcome;
    assert.equal(outcome.killed, null);
    assert.equal(outcome.code, 128 + os.constants.signals[signal]);
    assert.equal(stdout, "");
    assert.equal(stderr, `${signal} stopped the check, so the command wrote nothing.\n`);
    assert.deepEqual(fs.readdirSync(parent), []);
    assert.deepEqual(fs.readdirSync(tmpdir), []);
    assert.ok(signalled, "Original watcher must have sent the real selected signal");
    const raw = events();
    assert.ok(raw.some((event) => event.event === "handler-entry" && event.signal === signal));
    const closeIndex = raw.findIndex((event) => event.observer === "cli" && event.role === "janitor" && event.event === "close");
    const exitIndex = raw.findIndex((event) => event.event === "cli-exit");
    assert.ok(closeIndex >= 0 && closeIndex < exitIndex);
    const spawned = raw.filter((event) => event.observer === "cli" && event.role === "janitor" && event.event === "spawn");
    const closes = raw.filter((event) => event.observer === "cli" && event.role === "janitor" && event.event === "close");
    assert.equal(spawned.length, 1);
    assert.equal(closes.length, 1);
    assert.equal(closes[0].pid, spawned[0].pid);
    assert.equal(closes[0].signal, "SIGKILL");
    assert.throws(() => process.kill(spawned[0].pid, 0), { code: "ESRCH" });
    const worker = raw.find((event) => event.observer === "cli" && event.role === "worker" && event.event === "spawn");
    assert.ok(worker?.pid);
    assert.ok(raw.some((event) => event.role === "worker" && event.event === "exit" && event.pid === worker.pid));
    for (const event of raw)
      if (event.event === "cleanup-return") assert.equal(fs.existsSync(event.file), false);
    if (observationError) throw observationError;
    record({ event: "original-assertions-passed", signal });
  } catch (error) {
    failure = error;
    record({ event: "case-failed-before-cancellation", message: error.message, signalled, readyObserved });
  } finally {
    // Fixture cleanup is bounded. Missing product close stays a failed observation.
    try { watcher.close(); } catch (error) { failure ??= error; }
    clearInterval(observeReady);
    record({ event: "fixture-watchers-closed" });
    if (child && !directClosed) {
      failure ??= new Error("Original boundary did not establish actual direct close");
      // Normal cancellation uses the existing controlled CLI path, distinct from the proof send.
      record({ event: "cancel-cli-send", signal: "SIGTERM", childPid: child.pid });
      try {
        const current = identity(child.pid);
        assert.equal(current.starttime, cliIdentity.starttime);
        assert.equal(current.ppid, process.pid);
        record({ event: "cancel-cli-send-return", identity: current, result: child.kill("SIGTERM") });
      }
      catch (error) { record({ event: "cancel-cli-send-error", message: error.message }); }
      if ((await within(closePromise, 5_000))?.expired) {
        try { cancelDescendants(child.pid); }
        catch (error) { record({ event: "cancel-descendants-uncertain", message: error.message }); failure ??= error; }
        if ((await within(closePromise, 5_000))?.expired) {
          record({ event: "cancel-direct-cli-sigkill", pid: child.pid });
          try {
            const current = identity(child.pid);
            assert.equal(current.starttime, cliIdentity.starttime);
            assert.equal(current.ppid, process.pid);
            record({ event: "cancel-direct-identity", identity: current });
            record({ event: "cancel-direct-kill-return", result: child.kill("SIGKILL") });
          } catch (error) {
            record({ event: "cancel-direct-identity-or-send-error", message: error.message });
            failure ??= error;
          }
          // Failed diagnostics may truncate owned pipes after escalation. No such output can pass.
          child.stdout.destroy();
          child.stderr.destroy();
          const drained = await within(closePromise, 5_000);
          if (drained?.expired || !directExited || !directClosed) {
            record({ event: "product-close-unconfirmed", directExited, directClosed,
              required: "FAIL/INCONCLUSIVE; exact owned container emergency containment only" });
            failure ??= new Error("Product close unconfirmed within bound; emergency containment required");
            // Permit this FAILED diagnostic body to settle; this is not close/reap proof.
            child.unref();
          }
        }
      }
    }
    record({ event: "case-drain-observation", directExited, directClosed, signalled, readyObserved,
      productOutcome: failure || observationError ? "FAIL_OR_INCONCLUSIVE" : "ASSERTIONS_PASSED_PENDING_STRICT_CENSUS",
      infrastructureContainment: "NOT_YET_ATTEMPTED" });
  }
  failure ??= observationError;
  if (failure) throw failure;
});
