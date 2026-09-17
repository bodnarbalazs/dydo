import path from "node:path";

// Extracted from openai-sse-provider.mjs (DYD-91): the provider starts an HTTP server at
// module top level, so importing it to reach parseArgs would start a live listener. This
// module holds only the pure argument shaping, kept behaviour-identical, so it can be
// imported and tested without starting a host or a server.
export function parseArgs(argv) {
  const result = { mode: "opencode" };
  for (let index = 0; index < argv.length; index += 2) {
    const key = argv[index];
    const value = argv[index + 1];
    if (!value || !["--requests", "--mode", "--candidate"].includes(key)) throw new Error("usage: openai-sse-provider.mjs --requests <path> [--mode opencode|codex] [--candidate <path>]");
    if (key === "--requests") result.requests = path.resolve(value);
    if (key === "--mode") result.mode = value;
    if (key === "--candidate") result.candidate = path.resolve(value);
  }
  if (!result.requests) throw new Error("--requests is required");
  if (!["opencode", "codex"].includes(result.mode)) throw new Error("--mode must be opencode or codex");
  if (result.mode === "codex" && !result.candidate) throw new Error("--candidate is required for Codex mode");
  return result;
}
