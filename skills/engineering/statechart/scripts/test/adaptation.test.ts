import assert from "node:assert/strict";
import childProcess, { type ChildProcess, spawnSync } from "node:child_process";
import { EventEmitter } from "node:events";
import fs from "node:fs";
import { syncBuiltinESMExports } from "node:module";
import os from "node:os";
import path from "node:path";
import { PassThrough } from "node:stream";
import { test } from "node:test";
import { guardWorker } from "../windows.ts";
import { pinnedTlc } from "../run.ts";
import { skip as noTlc } from "./tlc.ts";

const replayJar = noTlc ? "" : pinnedTlc(path.resolve(process.env.TLA2TOOLS!));

for (const input of ["go", "refused"]) {
  test(`Worker ownership handshake ${input} cannot publish an unchecked result`, t => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "statechart-worker-handshake-"));
    t.after(() => fs.rmSync(dir, { recursive: true, force: true }));
    const bootstrap = "Object.defineProperty(process, 'platform', { value: 'win32' });";
    const result = spawnSync(process.execPath, ["--import", `data:text/javascript,${encodeURIComponent(bootstrap)}`,
      path.resolve(import.meta.dirname, "../worker.ts"), dir, "missing-spec", "missing-config",
      path.join(dir, "missing-view.mjs")], { input, encoding: "utf8", windowsHide: true });
    assert.equal(result.status, 2, result.stderr);
    if (input === "refused") assert.equal(result.stderr, "");
    else assert.match(result.stderr, /missing-view/);
    assert.deepEqual(fs.readdirSync(dir), []);
  });
}

test("Windows cleanup removes its stage when scratch creation fails before the worker starts", t => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "statechart-early-cleanup-"));
  t.after(() => fs.rmSync(dir, { recursive: true, force: true }));
  const spec = path.join(dir, "Spec.tla");
  fs.writeFileSync(spec, "---- MODULE Spec ----\n====\n");
  fs.writeFileSync(path.join(dir, "Spec.cfg"), "");
  const bootstrap = `import fs from 'node:fs';
import path from 'node:path';
import { syncBuiltinESMExports } from 'node:module';
Object.defineProperty(process, 'platform', { value: 'win32' });
const make = fs.mkdtempSync;
fs.mkdtempSync = (prefix, ...args) => {
  if (path.basename(String(prefix)) === 'statechart-') throw new Error('scratch creation refused');
  return make(prefix, ...args);
};
syncBuiltinESMExports();`;
  const result = spawnSync(process.execPath, ["--import", `data:text/javascript,${encodeURIComponent(bootstrap)}`,
    path.resolve(import.meta.dirname, "../statechart.ts"), spec, "--out", path.join(dir, "out")],
  { encoding: "utf8", windowsHide: true });
  assert.equal(result.status, 2, result.stderr);
  assert.equal(result.stderr, "scratch creation refused\n");
  assert.deepEqual(fs.readdirSync(dir).sort(), ["Spec.cfg", "Spec.tla"]);
});

function child() {
  const events = new EventEmitter();
  const stdin = new PassThrough();
  const stdout = new PassThrough();
  const state = { killed: false, input: "" };
  stdin.on("data", chunk => { state.input += chunk; });
  stdin.on("finish", () => queueMicrotask(() => events.emit("close", 0)));
  return Object.assign(events, { stdin, stdout, pid: 123, state, kill() { state.killed = true; } });
}

for (const result of ["ready", "bad-response", "exit", "error"] as const) {
  test(`Windows ownership handshake: ${result}`, async t => {
    const worker = child();
    const guard = child();
    const janitor = child();
    const spawned = [guard, janitor];
    t.mock.method(childProcess, "spawn", () => spawned.shift());
    syncBuiltinESMExports();
    t.after(() => { t.mock.restoreAll(); syncBuiltinESMExports(); });
    const protectedWorker = guardWorker(worker as unknown as ChildProcess, "stage", "scratch");
    if (result === "exit") guard.emit("exit", 2);
    else if (result === "error") guard.emit("error", new Error("spawn refused"));
    else guard.stdout.write(result);
    if (result !== "ready") {
      await assert.rejects(protectedWorker, /before ready|bad-response|spawn refused/);
      assert.equal(worker.state.killed, true);
      assert.equal(worker.state.input, "");
    } else {
      const close = await protectedWorker;
      assert.equal(worker.state.input, "go");
      await close();
      await close();
    }
    assert.equal(guard.stdin.writableEnded, true);
    assert.equal(janitor.stdin.writableEnded, true);
  });
}

for (const failure of ["exit", "error"] as const) {
  test(`Windows cleanup ${failure} is reported to the caller`, async t => {
    const worker = child();
    const guard = child();
    const janitor = child();
    const spawned = [guard, janitor];
    t.mock.method(childProcess, "spawn", () => spawned.shift());
    syncBuiltinESMExports();
    t.after(() => { t.mock.restoreAll(); syncBuiltinESMExports(); });
    const protectedWorker = guardWorker(worker as unknown as ChildProcess, "stage", "scratch");
    guard.stdout.write("ready");
    const close = await protectedWorker;
    const closed = close();
    if (failure === "exit") janitor.emit("close", 2);
    else janitor.emit("error", new Error("janitor refused"));
    await assert.rejects(closed, /Windows cleanup exited 2|janitor refused/);
  });
}

for (const result of ["gone", "alive", "denied"] as const) {
  test(`Windows janitor retains ownership until worker is ${result}`, t => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "statechart-cleanup-test-"));
    t.after(() => fs.rmSync(dir, { recursive: true, force: true }));
    const stage = path.join(dir, "stage");
    const scratch = path.join(dir, "scratch");
    fs.mkdirSync(stage);
    fs.mkdirSync(scratch);
    const bootstrap = `import timers from 'node:timers/promises';
import { syncBuiltinESMExports } from 'node:module';
timers.setTimeout = async () => {};
syncBuiltinESMExports();
process.kill = () => {
  if (${JSON.stringify(result)} === 'alive') return true;
  throw Object.assign(new Error('worker probe'), {code: ${JSON.stringify(result === "gone" ? "ESRCH" : "EPERM")}});
};`;
    const r = spawnSync(process.execPath, ["--import", `data:text/javascript,${encodeURIComponent(bootstrap)}`,
      path.resolve(import.meta.dirname, "../windows-cleanup.ts"), "123", stage, scratch], { encoding: "utf8", input: "", windowsHide: true });
    if (result === "gone") {
      assert.equal(r.status, 0, r.stderr);
      assert.equal(fs.existsSync(stage), false);
      assert.equal(fs.existsSync(scratch), false);
    } else {
      assert.equal(r.status, result === "alive" ? 2 : 1, r.stderr);
      assert.match(r.stderr, result === "alive" ? /scratch files were retained/ : /EPERM/);
      assert.equal(fs.existsSync(stage), true);
      assert.equal(fs.existsSync(scratch), true);
    }
  });
}

for (const tools of ["present", "installed", "failed"] as const) {
  test(`Caller tool discovery when mise tools are ${tools}`, {
    skip: process.platform === "win32" || noTlc,
  }, t => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "statechart-mise-test-"));
    t.after(() => fs.rmSync(dir, { recursive: true, force: true }));
    const toolsJar = path.join(dir, "tlc");
    fs.mkdirSync(toolsJar);
    fs.copyFileSync(replayJar, path.join(toolsJar, "tla2tools.jar"));
    fs.writeFileSync(path.join(dir, "mise"), `#!/bin/sh
if [ "$1" = install ]; then
  [ "$STATECHART_TEST_TOOLS" = failed ] && echo 'fixture install refused' >&2 && exit 1
  : > "$STATECHART_TEST_READY"
  exit 0
fi
[ "$STATECHART_TEST_TOOLS" = present ] || [ -e "$STATECHART_TEST_READY" ] || exit 1
if [ "$1" = where ]; then printf '%s\\n' "$STATECHART_TEST_JAR"; else printf '%s\\n' "$STATECHART_TEST_JAVA"; fi
`, { mode: 0o755 });
    // PATH holds only the fake mise, so the replay Java names Node by absolute path.
    const replayJava = path.join(dir, "replay-java");
    fs.writeFileSync(replayJava, `#!/bin/sh
exec ${JSON.stringify(process.execPath)} ${JSON.stringify(path.resolve(import.meta.dirname, "java.ts"))} "$@"
`, { mode: 0o755 });
    const env: NodeJS.ProcessEnv = { ...process.env, PATH: dir,
      STATECHART_TEST_TOOLS: tools, STATECHART_TEST_READY: path.join(dir, "ready"),
      STATECHART_TEST_JAR: toolsJar,
      STATECHART_TEST_JAVA: replayJava,
      STATECHART_REPLAY: path.join(import.meta.dirname, "corpus/CounterJump") };
    delete env.JAVA;
    delete env.TLA2TOOLS;
    const out = path.join(dir, "out");
    const r = spawnSync(process.execPath, [path.resolve(import.meta.dirname, "../statechart.ts"),
      path.join(import.meta.dirname, "corpus/CounterJump/CounterJump.tla"), "--out", out], { env, encoding: "utf8", windowsHide: true });
    if (tools === "failed") {
      assert.equal(r.status, 2);
      assert.match(r.stderr, /Cannot find TLC: mise install failed: fixture install refused/);
      assert.equal(fs.existsSync(out), false);
    } else {
      assert.equal(r.status, 1, r.stderr);
      assert.match(fs.readFileSync(path.join(out, "report.md"), "utf8"), /The shortest path has 1 step/);
    }
  });
}
