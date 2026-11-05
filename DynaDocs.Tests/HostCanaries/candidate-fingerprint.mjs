import { createHash } from "node:crypto";
import { constants } from "node:fs";
import { access, lstat, readdir, readFile, readlink, stat } from "node:fs/promises";
import { spawn } from "node:child_process";
import { delimiter, join } from "node:path";

// The PreToolUse hook the candidate's .claude/settings.json gives Claude. Claude runs a command hook
// through a shell, so the pre-warm does too.
export const CLAUDE_GUARD_COMMAND = "dydo guard --host claude";
const TIMEOUT = 120_000;

// Bytes, entry types and link targets of every entry but the top-level .git. Nothing else is excluded:
// a host write the pre-warm did not account for must change it.
export async function candidateFingerprint(root) {
  const entries = [];
  async function visit(directory, prefix = "") {
    for (const entry of (await readdir(directory, { withFileTypes: true })).sort((left, right) => ordinal(left.name, right.name))) {
      if (!prefix && entry.name === ".git") continue;
      const path = join(directory, entry.name);
      const relativePath = prefix ? `${prefix}/${entry.name}` : entry.name;
      const info = await lstat(path);
      if (info.isSymbolicLink()) entries.push({ path: relativePath, type: "link", target: await readlink(path) });
      else if (info.isDirectory()) {
        entries.push({ path: relativePath, type: "directory" });
        await visit(path, relativePath);
      } else if (info.isFile()) entries.push({ path: relativePath, type: "regular-file", sha256: createHash("sha256").update(await readFile(path)).digest("hex") });
      else entries.push({ path: relativePath, type: "other" });
    }
  }
  await visit(root);
  return createHash("sha256").update(JSON.stringify(entries)).digest("hex");
}

// The first file a shell would run for `dydo` on this PATH, in PATH order and, on Windows, PATHEXT order.
export async function resolveDydo(env) {
  const windows = process.platform === "win32";
  const pathKey = windows ? Object.keys(env).find(key => key.toUpperCase() === "PATH") : "PATH";
  const extensions = windows ? (env.PATHEXT ?? ".COM;.EXE;.BAT;.CMD").split(";") : [""];
  for (const folder of (env[pathKey] ?? "").split(delimiter).filter(Boolean)) {
    for (const extension of extensions) {
      const candidate = join(folder, `dydo${extension}`);
      if (await isExecutableFile(candidate)) return candidate;
    }
  }
  throw new Error("dydo is not on the PATH the Claude canary inherits, so the candidate's guard hook would fail on every Read; put dydo on PATH before running the Claude leg");
}

// Runs the guard hook once with a candidate Read, as Claude would, so the guard's once-a-day
// validation write lands before the candidate is fingerprinted. Returns the record; the caller judges it.
export async function prewarmClaudeGuard({ checkout, env, readPath }) {
  const path = await resolveDydo(env);
  const version = await shell("dydo --version", { cwd: checkout, env });
  const payload = { session_id: "host-canary-guard-prewarm", hook_event_name: "PreToolUse", tool_name: "Read", tool_input: { file_path: readPath }, cwd: checkout };
  const guard = await shell(CLAUDE_GUARD_COMMAND, { cwd: checkout, env, input: JSON.stringify(payload) });
  return {
    dydo: { path, version: version.stdout.trim(), versionExitCode: version.exitCode },
    command: CLAUDE_GUARD_COMMAND,
    payload,
    exitCode: guard.exitCode,
    stdout: guard.stdout,
    stderr: guard.stderr
  };
}

function shell(command, { cwd, env, input = "" }) {
  return new Promise((resolveRun, reject) => {
    const child = spawn(command, { cwd, env, shell: true, stdio: ["pipe", "pipe", "pipe"], windowsHide: true });
    let stdout = "";
    let stderr = "";
    child.stdout.setEncoding("utf8").on("data", chunk => { stdout += chunk; });
    child.stderr.setEncoding("utf8").on("data", chunk => { stderr += chunk; });
    const timer = setTimeout(() => child.kill(), TIMEOUT);
    child.once("error", reject);
    child.once("close", code => {
      clearTimeout(timer);
      resolveRun({ exitCode: code ?? -1, stdout, stderr });
    });
    child.stdin.end(input);
  });
}

async function isExecutableFile(path) {
  try {
    await access(path, constants.X_OK);
    return (await stat(path)).isFile();
  } catch {
    return false;
  }
}

function ordinal(left, right) { if (left < right) return -1; if (left > right) return 1; return 0; }
