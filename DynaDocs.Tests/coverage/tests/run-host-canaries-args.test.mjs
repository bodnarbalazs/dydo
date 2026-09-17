import assert from "node:assert/strict";
import test from "node:test";
import { parseArgs } from "../../HostCanaries/run-host-canaries-args.mjs";

const REQUIRED = ["--candidate", "C:/candidate", "--scratch-root", "C:/scratch", "--evidence", "C:/evidence"];

test("a valid --only codex is accepted and carried through", () => {
  const result = parseArgs([...REQUIRED, "--only", "codex"]);
  assert.equal(result.only, "codex");
  assert.equal(result.candidate, "C:/candidate");
  assert.equal(result.scratchRoot, "C:/scratch");
  assert.equal(result.evidence, "C:/evidence");
});

test("an unknown --only value is rejected with the exact message", () => {
  assert.throws(
    () => parseArgs([...REQUIRED, "--only", "bogus"]),
    error => error.message === "--only must be claude, codex, or opencode",
  );
});

test("a missing required argument is rejected", () => {
  assert.throws(
    () => parseArgs(["--candidate", "C:/candidate", "--scratch-root", "C:/scratch"]),
    /usage: run-host-canaries\.mjs/,
  );
});

test("an unknown flag is rejected", () => {
  assert.throws(
    () => parseArgs(["--bogus", "value"]),
    /unknown or incomplete argument: --bogus/,
  );
});
