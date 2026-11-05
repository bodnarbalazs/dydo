import assert from "node:assert/strict";
import test from "node:test";
import { spawn } from "node:child_process";
import { chmod, mkdir, mkdtemp, readFile, rm, symlink, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { CLAUDE_GUARD_COMMAND, candidateFingerprint, prewarmClaudeGuard, resolveDydo } from "../../HostCanaries/candidate-fingerprint.mjs";

// A stand-in for `dydo` that keeps the real guard's Read-time write contract
// (GuardCommand.RunDailyValidationIfDue): write dydo/_system/.local/last-validation when it is absent
// or a day old, and nothing else. DYDO_STUB_PER_CALL plays a future guard that writes on every call.
const STUB = `import { appendFileSync, existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
const args = process.argv.slice(2);
if (args[0] === "--version") { process.stdout.write("0.0.0-stub\\n"); process.exit(0); }
if (args.join(" ") !== "guard --host claude") { process.stderr.write("unexpected argv " + args.join(" ")); process.exit(64); }
const payload = JSON.parse(readFileSync(0, "utf8"));
appendFileSync(process.env.DYDO_STUB_LOG, JSON.stringify({ cwd: process.cwd(), payload }) + "\\n");
if (process.env.DYDO_STUB_EXIT) { process.stderr.write("guard refused"); process.exit(Number(process.env.DYDO_STUB_EXIT)); }
const marker = join("dydo", "_system", ".local", "last-validation");
if (!existsSync(marker) || Date.now() - statSync(marker).mtimeMs > 86_400_000) {
  mkdirSync(dirname(marker), { recursive: true });
  writeFileSync(marker, new Date().toISOString());
}
if (process.env.DYDO_STUB_PER_CALL) writeFileSync(join(dirname(marker), "call-" + process.hrtime.bigint()), "");
`;

async function scene(t) {
  const root = await mkdtemp(join(tmpdir(), "dyd545-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  const bin = join(root, "bin");
  const checkout = join(root, "candidate");
  await mkdir(bin);
  await mkdir(join(checkout, ".git"), { recursive: true });
  await mkdir(join(checkout, "skills", "demo"), { recursive: true });
  await writeFile(join(checkout, "AGENTS.md"), "# Agents\n");
  await writeFile(join(checkout, "skills", "demo", "SKILL.md"), "---\nname: demo\n---\n");
  await writeFile(join(bin, "dydo-stub.mjs"), STUB);
  await writeFile(join(bin, "dydo"), `#!/bin/sh\nexec "${process.execPath}" "${join(bin, "dydo-stub.mjs")}" "$@"\n`);
  await chmod(join(bin, "dydo"), 0o755);
  await writeFile(join(bin, "dydo.cmd"), `@"${process.execPath}" "%~dp0dydo-stub.mjs" %*\r\n`);
  const log = join(root, "stub.ndjson");
  const env = { ...withoutPath(process.env), PATH: bin, DYDO_STUB_LOG: log };
  return { root, bin, checkout, env, log, readPath: join(checkout, "AGENTS.md") };
}

function withoutPath(env) {
  return Object.fromEntries(Object.entries(env).filter(([key]) => key.toUpperCase() !== "PATH" && !key.startsWith("DYDO_STUB_")));
}

// What Claude does on a Read once the hook is configured: the hook command, through a shell, in the checkout.
function claudeHookRead({ checkout, env, readPath }) {
  return new Promise((resolveRun, reject) => {
    const child = spawn(CLAUDE_GUARD_COMMAND, { cwd: checkout, env, shell: true, stdio: ["pipe", "ignore", "pipe"], windowsHide: true });
    let stderr = "";
    child.stderr.setEncoding("utf8").on("data", chunk => { stderr += chunk; });
    child.once("error", reject);
    child.once("close", code => code === 0 ? resolveRun() : reject(new Error(`hook Read exited ${code}: ${stderr}`)));
    child.stdin.end(JSON.stringify({ session_id: "claude-session", hook_event_name: "PreToolUse", tool_name: "Read", tool_input: { file_path: readPath }, cwd: checkout }));
  });
}

async function stubCalls(log) {
  return (await readFile(log, "utf8")).split("\n").filter(Boolean).map(line => JSON.parse(line));
}

test("without the pre-warm, the first hook Read writes last-validation and changes the fingerprint", async t => {
  const s = await scene(t);
  const before = await candidateFingerprint(s.checkout);

  await claudeHookRead(s);

  assert.notEqual(await candidateFingerprint(s.checkout), before);
  assert.match(await readFile(join(s.checkout, "dydo", "_system", ".local", "last-validation"), "utf8"), /^\d{4}-/);
});

test("after the pre-warm, hook Reads leave the fingerprint unchanged", async t => {
  const s = await scene(t);

  const record = await prewarmClaudeGuard(s);
  const before = await candidateFingerprint(s.checkout);
  await claudeHookRead(s);
  await claudeHookRead(s);

  assert.equal(await candidateFingerprint(s.checkout), before);
  assert.equal(record.exitCode, 0);
  assert.equal(record.dydo.path, join(s.bin, process.platform === "win32" ? "dydo.cmd" : "dydo"));
  assert.equal(record.dydo.version, "0.0.0-stub");
  assert.equal(record.dydo.versionExitCode, 0);
  assert.equal(record.command, "dydo guard --host claude");
  const [prewarm, ...reads] = await stubCalls(s.log);
  assert.equal(reads.length, 2);
  assert.equal(prewarm.cwd, s.checkout);
  assert.deepEqual(prewarm.payload, { session_id: "host-canary-guard-prewarm", hook_event_name: "PreToolUse", tool_name: "Read", tool_input: { file_path: s.readPath }, cwd: s.checkout });
});

test("after the pre-warm, any other write still changes the fingerprint", async t => {
  const local = ["dydo", "_system", ".local"];
  const writes = {
    "another file in the guard's .local folder": s => writeFile(join(s.checkout, ...local, "other-state"), "x"),
    "a rewritten last-validation": s => writeFile(join(s.checkout, ...local, "last-validation"), "2000-01-01T00:00:00.000Z"),
    "a file elsewhere in the candidate": s => writeFile(join(s.checkout, "skills", "demo", "notes.md"), "x"),
    "a file turned into a folder": async s => { await rm(join(s.checkout, "AGENTS.md")); await mkdir(join(s.checkout, "AGENTS.md")); },
    "a guard that writes on every call": s => claudeHookRead({ ...s, env: { ...s.env, DYDO_STUB_PER_CALL: "1" } })
  };
  for (const [name, write] of Object.entries(writes)) {
    await t.test(name, async t => {
      const s = await scene(t);
      await prewarmClaudeGuard(s);
      const before = await candidateFingerprint(s.checkout);

      await write(s);

      assert.notEqual(await candidateFingerprint(s.checkout), before);
    });
  }
});

test("the fingerprint ignores the top-level .git and sees a link target change", async t => {
  const s = await scene(t);
  const linkType = process.platform === "win32" ? "junction" : "dir";
  await symlink(join(s.checkout, "skills", "demo"), join(s.checkout, "projection"), linkType);
  const before = await candidateFingerprint(s.checkout);

  await writeFile(join(s.checkout, ".git", "index"), "x");
  assert.equal(await candidateFingerprint(s.checkout), before);

  await rm(join(s.checkout, "projection"), { recursive: true });
  await symlink(join(s.checkout, "skills"), join(s.checkout, "projection"), linkType);
  assert.notEqual(await candidateFingerprint(s.checkout), before);
});

test("dydo missing from the inherited PATH fails the Claude leg closed before any hook runs", async t => {
  const s = await scene(t);
  const empty = join(s.root, "empty");
  await mkdir(empty);
  const env = { ...s.env, PATH: empty };

  await assert.rejects(resolveDydo(env), /dydo is not on the PATH the Claude canary inherits/);
  await assert.rejects(prewarmClaudeGuard({ ...s, env }), /dydo is not on the PATH the Claude canary inherits/);
  await assert.rejects(readFile(s.log, "utf8"), { code: "ENOENT" });
});

test("a guard that fails the pre-warm is reported with its exit and stderr", async t => {
  const s = await scene(t);

  const record = await prewarmClaudeGuard({ ...s, env: { ...s.env, DYDO_STUB_EXIT: "2" } });

  assert.equal(record.exitCode, 2);
  assert.equal(record.stderr, "guard refused");
});
