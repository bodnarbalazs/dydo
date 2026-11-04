import assert from "node:assert/strict";
import { spawn, spawnSync } from "node:child_process";
import { once } from "node:events";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";
import { pathToFileURL } from "node:url";

const windows = process.platform === "win32";
const scripts = path.resolve(import.meta.dirname, "..");
for (const finish of ["normal", "killed"] as const) {
  test(`Windows job owns descendants and scratch on ${finish} parent exit`, { skip: !windows, timeout: 30_000 }, async (t) => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "statechart-job-test-"));
    const stage = path.join(dir, "stage");
    const scratch = path.join(dir, "scratch");
    fs.mkdirSync(stage);
    fs.mkdirSync(scratch);
    const worker = path.join(dir, "worker.mjs");
    fs.writeFileSync(worker, `import { spawn } from 'node:child_process';
let go = ''; for await (const chunk of process.stdin) go += chunk;
if (go !== 'go') process.exit(2);
const child = spawn(process.execPath, ['-e', 'process.stdin.resume()'], {stdio: ['pipe', 'inherit', 'inherit'], windowsHide: true});
console.log(JSON.stringify({worker: process.pid, child: child.pid}));
`);
    const runner = path.join(dir, "runner.mjs");
    fs.writeFileSync(runner, `import { spawn } from 'node:child_process';
import { guardWorker } from ${JSON.stringify(pathToFileURL(path.join(scripts, "windows.ts")).href)};
const worker = spawn(process.execPath, [${JSON.stringify(worker)}], {stdio: ['pipe','inherit','inherit'], windowsHide: true});
const close = await guardWorker(worker, ${JSON.stringify(stage)}, ${JSON.stringify(scratch)});
process.stdin.once('data', async () => { await close(); process.exit(0); });
`);
    const parent = spawn(process.execPath, [runner], { stdio: ["pipe", "pipe", "pipe"], windowsHide: true });
    let stderr = "";
    parent.stderr.on("data", chunk => { stderr += chunk; });
    t.after(() => { parent.kill(); fs.rmSync(dir, { recursive: true, force: true }); });
    const closed = once(parent, "close");
    const [data] = await Promise.race([once(parent.stdout, "data"), closed.then(() => { throw new Error(stderr || "runner exited before ready"); })]);
    const pids = JSON.parse(data.toString());
    const cleaned = new Promise<void>((resolve) => {
      const watcher = fs.watch(dir, () => {
        if (!fs.existsSync(stage) && !fs.existsSync(scratch)) {
          watcher.close();
          resolve();
        }
      });
      t.after(() => watcher.close());
    });
    if (finish === "normal") parent.stdin.end("finish");
    else parent.kill("SIGKILL");
    await closed;
    await cleaned;
    for (const pid of [pids.worker, pids.child]) assert.throws(() => process.kill(pid, 0), { code: "ESRCH" });
    assert.equal(fs.existsSync(stage), false);
    assert.equal(fs.existsSync(scratch), false);
  });
}

for (const mode of ["relative", "absolute", "bare", "unset"] as const) {
test(`Windows resolves ${mode} TLC overrides from the caller and accepts empty output`, {
  skip: !windows || (!process.env.TLA2TOOLS && "requires real TLC"),
}, (t) => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "statechart-relative-"));
  t.after(() => fs.rmSync(dir, { recursive: true, force: true }));
  const spec = path.join(scripts, "test/corpus/CounterJump/CounterJump.tla");
  fs.copyFileSync(process.env.TLA2TOOLS!, path.join(dir, "tools.jar"));
  const out = path.join(dir, "out");
  fs.mkdirSync(out);
  const resolved = spawnSync(process.env.JAVA ?? "java", ["-XshowSettings:properties", "-version"], { encoding: "utf8", windowsHide: true });
  assert.equal(resolved.status, 0, resolved.stderr);
  const home = resolved.stderr.match(/^\s*java.home = (.+)$/m)?.[1].trim();
  assert.ok(home, "Java must report its actual installation for the relative-tool test");
  const java = path.join(home, "bin", "java.exe");
  assert.ok(fs.existsSync(java), java);
  const env: NodeJS.ProcessEnv = { ...process.env, TLA2TOOLS: "./tools.jar" };
  // The caller's supported PATH configuration also supplies the real relative executable.
  const javaKey = Object.keys(env).find(key => key.toUpperCase() === "JAVA");
  if (javaKey) delete env[javaKey];
  if (mode === "relative") env.JAVA = path.relative(dir, java);
  if (mode === "absolute") env.JAVA = java;
  if (mode === "bare") env.JAVA = "java";
  const pathKey = Object.keys(env).find(key => key.toUpperCase() === "PATH") ?? "PATH";
  env[pathKey] = path.dirname(java) + path.delimiter + (env[pathKey] ?? "");
  const r = spawnSync(process.execPath, [path.join(scripts, "statechart.ts"), spec, "--out", out], {
    cwd: dir, encoding: "utf8", env, windowsHide: true,
  });
  assert.equal(r.status, 1, r.stderr);
  assert.match(fs.readFileSync(path.join(out, "report.md"), "utf8"), /The shortest path has 1 step/);
});
}
