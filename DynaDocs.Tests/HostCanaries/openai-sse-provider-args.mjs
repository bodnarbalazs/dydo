import path from "node:path";

// Split out of openai-sse-provider.mjs, which starts an HTTP server at module top level: importing
// it for parseArgs would start a live listener.
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
