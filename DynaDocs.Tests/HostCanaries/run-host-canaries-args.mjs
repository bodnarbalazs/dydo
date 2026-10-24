import { isAbsolute } from "node:path";

// Split out of run-host-canaries.mjs, which runs its CLI at module top level: importing it for
// parseArgs would run the whole host-canary suite.
export function parseArgs(argv) {
  const result = {};
  for (let index = 0; index < argv.length; index += 2) {
    const key = argv[index];
    const value = argv[index + 1];
    if (!value || !["--candidate", "--scratch-root", "--evidence", "--opencode-archive", "--ripgrep", "--only"].includes(key)) throw new Error(`unknown or incomplete argument: ${key}`);
    result[key.slice(2).replaceAll("-", "_")] = value;
  }
  assert(result.candidate && result.scratch_root && result.evidence, "usage: run-host-canaries.mjs --candidate <path> --scratch-root <absolute-clean-path> --evidence <absolute-retained-path> --opencode-archive <absolute-zip> --ripgrep <absolute-exe>");
  assert(isAbsolute(result.scratch_root), "--scratch-root must be absolute");
  assert(isAbsolute(result.evidence), "--evidence must be absolute");
  if (result.only) assert(["claude", "codex", "opencode"].includes(result.only), "--only must be claude, codex, or opencode");
  return { candidate: result.candidate, scratchRoot: result.scratch_root, evidence: result.evidence, opencodeArchive: result.opencode_archive, ripgrep: result.ripgrep, only: result.only };
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}
