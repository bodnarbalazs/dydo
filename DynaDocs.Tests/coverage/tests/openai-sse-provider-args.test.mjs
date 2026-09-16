import assert from "node:assert/strict";
import test from "node:test";
import { parseArgs } from "../../HostCanaries/openai-sse-provider-args.mjs";

test("mode defaults to opencode and requests resolves to an absolute path", () => {
  const result = parseArgs(["--requests", "requests.ndjson"]);
  assert.equal(result.mode, "opencode");
  assert.ok(result.requests.endsWith("requests.ndjson"));
});

test("codex mode requires --candidate", () => {
  assert.throws(
    () => parseArgs(["--requests", "requests.ndjson", "--mode", "codex"]),
    error => error.message === "--candidate is required for Codex mode",
  );
});

test("codex mode with a candidate is accepted", () => {
  const result = parseArgs(["--requests", "requests.ndjson", "--mode", "codex", "--candidate", "."]);
  assert.equal(result.mode, "codex");
  assert.ok(result.candidate);
});

test("missing --requests is rejected", () => {
  assert.throws(
    () => parseArgs(["--mode", "opencode"]),
    error => error.message === "--requests is required",
  );
});

test("an unknown mode is rejected", () => {
  assert.throws(
    () => parseArgs(["--requests", "requests.ndjson", "--mode", "bogus"]),
    error => error.message === "--mode must be opencode or codex",
  );
});

test("an unknown flag is rejected", () => {
  assert.throws(
    () => parseArgs(["--bogus", "value"]),
    error => error.message === "usage: openai-sse-provider.mjs --requests <path> [--mode opencode|codex] [--candidate <path>]",
  );
});
