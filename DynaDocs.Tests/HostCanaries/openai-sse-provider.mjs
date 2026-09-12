import { appendFile, readFile } from "node:fs/promises";
import http from "node:http";
import path from "node:path";

const args = parseArgs(process.argv.slice(2));
const expectedFact = "# Mission: {Topic}";
let state = 0;
let derivedResourcePath;

const server = http.createServer(async (request, response) => {
  try {
    if (request.method !== "POST") {
      return deny(response, `unexpected ${request.method} ${request.url}`);
    }

    const body = await readBody(request);
    let payload;
    try {
      payload = JSON.parse(body);
    } catch {
      return deny(response, "request body was not JSON");
    }

    await appendFile(args.requests, `${JSON.stringify({ state, url: request.url, payload })}\n`, "utf8");
    const serialized = JSON.stringify(payload);
    if (!request.url?.endsWith("/chat/completions")) {
      return deny(response, `unexpected provider endpoint ${request.url}`);
    }

    if (state === 0) {
      assert(serialized.includes('"skill"'), "first request did not expose the native skill tool");
      assert(serialized.includes("teach"), "first request did not expose teach in the native inventory");
      state = 1;
      return chatTool(response, "dyd91-skill", "skill", { name: "teach" });
    }

    if (state === 1) {
      assert(serialized.includes("mission-format.md"), "native skill result omitted the mission-format link");
      const base = extractSkillBase(serialized);
      derivedResourcePath = path.resolve(base, "resources", "mission-format.md");
      const fact = await readFile(derivedResourcePath, "utf8");
      assert(fact.includes(expectedFact), "derived resource did not contain the expected fact");
      state = 2;
      return chatTool(response, "dyd91-read", "read", { filePath: derivedResourcePath });
    }

    if (state === 2) {
      assert(serialized.includes(expectedFact), "native read result omitted the expected fact");
      state = 3;
      return chatText(response, expectedFact);
    }

    return deny(response, "unexpected extra provider request (including title generation)");
  } catch (error) {
    return deny(response, error instanceof Error ? error.message : String(error));
  }
});

server.listen(0, "127.0.0.1", () => {
  const address = server.address();
  process.stdout.write(`${JSON.stringify({ port: address.port })}\n`);
});

for (const signal of ["SIGINT", "SIGTERM"]) {
  process.on(signal, () => server.close(() => process.exit(state === 3 ? 0 : 1)));
}

function chatTool(response, id, name, value) {
  stream(response, [
    {
      id: "chatcmpl-dyd91",
      object: "chat.completion.chunk",
      created: 0,
      model: "skill-canary",
      choices: [{ index: 0, delta: { role: "assistant", tool_calls: [{ index: 0, id, type: "function", function: { name, arguments: JSON.stringify(value) } }] }, finish_reason: null }]
    },
    {
      id: "chatcmpl-dyd91",
      object: "chat.completion.chunk",
      created: 0,
      model: "skill-canary",
      choices: [{ index: 0, delta: {}, finish_reason: "tool_calls" }]
    }
  ]);
}

function chatText(response, content) {
  stream(response, [
    {
      id: "chatcmpl-dyd91",
      object: "chat.completion.chunk",
      created: 0,
      model: "skill-canary",
      choices: [{ index: 0, delta: { role: "assistant", content }, finish_reason: null }]
    },
    {
      id: "chatcmpl-dyd91",
      object: "chat.completion.chunk",
      created: 0,
      model: "skill-canary",
      choices: [{ index: 0, delta: {}, finish_reason: "stop" }]
    }
  ]);
}

function stream(response, chunks) {
  response.writeHead(200, {
    "content-type": "text/event-stream",
    "cache-control": "no-cache",
    connection: "keep-alive"
  });
  for (const chunk of chunks) response.write(`data: ${JSON.stringify(chunk)}\n\n`);
  response.end("data: [DONE]\n\n");
}

function deny(response, message) {
  process.stderr.write(`${message}\n`);
  response.writeHead(409, { "content-type": "application/json" });
  response.end(JSON.stringify({ error: { message, type: "dyd91_unexpected_request" } }));
}

function extractSkillBase(serialized) {
  const normalized = serialized.replaceAll("\\\\", "\\");
  const candidates = [
    /base directory(?: is|:)?\s*([^\n\r"`]+?teach)(?:[\\/]SKILL\.md)?(?=[\n\r"`])/i,
    /((?:[A-Za-z]:)?[^\n\r"`]*[\\/]skills[\\/]teach)(?:[\\/]SKILL\.md)?(?=[\n\r"`])/i
  ];
  for (const candidate of candidates) {
    const match = normalized.match(candidate);
    if (match) return match[1].trim().replace(/^<|>$/g, "");
  }
  throw new Error("could not derive teach's installed base from the native skill result");
}

function readBody(request) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    request.on("data", chunk => chunks.push(chunk));
    request.on("end", () => resolve(Buffer.concat(chunks).toString("utf8")));
    request.on("error", reject);
  });
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function parseArgs(argv) {
  const result = {};
  for (let index = 0; index < argv.length; index += 2) {
    const key = argv[index];
    if (key !== "--requests" || !argv[index + 1]) throw new Error("usage: openai-sse-provider.mjs --requests <path>");
    result.requests = path.resolve(argv[index + 1]);
  }
  if (!result.requests) throw new Error("--requests is required");
  return result;
}
