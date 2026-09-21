import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

// Regression for DYD-219: the OpenCode leg of run-host-canaries.mjs starts this provider
// without --candidate (openai-sse-provider-args.mjs makes --candidate optional outside Codex
// mode). A prior fix hop hoisted the teach-directory resolution to module top level, which
// dereferenced the absent args.candidate and crashed the process before it could print its
// port line. This test spawns the real provider script the same way the runner does for the
// OpenCode leg and asserts it starts and reports a port, instead of exiting non-zero.

const scriptPath = fileURLToPath(new URL("../../HostCanaries/openai-sse-provider.mjs", import.meta.url));

function waitForPort(child) {
  return new Promise((resolve, reject) => {
    let stdout = "";
    let stderr = "";
    const timer = setTimeout(() => reject(new Error(`timed out waiting for port line; stderr: ${stderr}`)), 10000);
    child.stdout.setEncoding("utf8").on("data", chunk => {
      stdout += chunk;
      const line = stdout.split("\n").find(candidate => candidate.trim().length > 0);
      if (!line) return;
      try {
        const parsed = JSON.parse(line);
        if (typeof parsed.port === "number") {
          clearTimeout(timer);
          resolve(parsed.port);
        }
      } catch {
        // not a complete JSON line yet
      }
    });
    child.stderr.setEncoding("utf8").on("data", chunk => { stderr += chunk; });
    child.once("exit", code => {
      if (code !== null) {
        clearTimeout(timer);
        reject(new Error(`provider exited ${code} before printing a port line; stderr: ${stderr}`));
      }
    });
  });
}

test("opencode mode starts the loopback provider and prints its port without --candidate", async () => {
  const requestsDir = await mkdtemp(path.join(tmpdir(), "dyd219-opencode-start-"));
  const requestsPath = path.join(requestsDir, "requests.ndjson");
  const child = spawn(process.execPath, [scriptPath, "--requests", requestsPath, "--mode", "opencode"], {
    stdio: ["ignore", "pipe", "pipe"],
    windowsHide: true,
  });
  try {
    const port = await waitForPort(child);
    assert.ok(Number.isInteger(port) && port > 0, `expected a positive integer port, got ${port}`);
  } finally {
    child.kill();
    await rm(requestsDir, { recursive: true, force: true });
  }
});
