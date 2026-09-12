import { appendFile, readFile, realpath } from "node:fs/promises";
import http from "node:http";
import path from "node:path";

const args = parseArgs(process.argv.slice(2));
const expectedFact = "# Mission: {Topic}";
const codexPrompt = "Load the teach skill. Follow its mission-format link from the installed skill base reported or exposed by the host. Reply exactly with the Markdown H1 template from that resource and no other text.";
let state = 0;
let derivedResourcePath;

const server = http.createServer(async (request, response) => {
  try {
    if (args.mode === "codex" && request.method === "GET" && request.url === "/v1/models") {
      await appendFile(args.requests, `${JSON.stringify({ state, method: "GET", url: request.url, response: { model: "skill-canary" } })}\n`, "utf8");
      response.writeHead(200, { "content-type": "application/json" });
      return response.end(JSON.stringify({ object: "list", data: [{ id: "skill-canary", object: "model", created: 0, owned_by: "dyd91" }] }));
    }
    if (request.method !== "POST") {
      await appendFile(args.requests, `${JSON.stringify({ state, method: request.method, url: request.url, unexpected: true })}\n`, "utf8");
      return deny(response, `unexpected ${request.method} ${request.url}`);
    }

    const body = await readBody(request);
    let payload;
    try {
      payload = JSON.parse(body);
    } catch {
      return deny(response, "request body was not JSON");
    }

    if (args.mode === "codex") return handleCodex(request, response, payload);
    await appendFile(args.requests, `${JSON.stringify({ state, method: "POST", url: request.url, payload })}\n`, "utf8");
    const serialized = JSON.stringify(payload);
    if (!request.url?.endsWith("/chat/completions") && !request.url?.endsWith("/responses")) {
      return deny(response, `unexpected provider endpoint ${request.url}`);
    }

    if (state === 0) {
      assert(serialized.includes('"skill"'), "first request did not expose the native skill tool");
      assert(serialized.includes("teach"), "first request did not expose teach in the native inventory");
      state = 1;
      return providerTool(request.url, response, "dyd91-skill", "skill", { name: "teach" });
    }

    if (state === 1) {
      const skillResult = findToolOutput(payload, "dyd91-skill");
      assert(skillResult?.includes("mission-format.md"), "native skill result omitted the mission-format link");
      const base = extractSkillBase(skillResult);
      derivedResourcePath = path.resolve(base, "resources", "mission-format.md");
      const fact = await readFile(derivedResourcePath, "utf8");
      assert(fact.includes(expectedFact), "derived resource did not contain the expected fact");
      state = 2;
      return providerTool(request.url, response, "dyd91-read", "read", { filePath: derivedResourcePath });
    }

    if (state === 2) {
      assert(serialized.includes(expectedFact), "native read result omitted the expected fact");
      state = 3;
      return providerText(request.url, response, expectedFact);
    }

    return deny(response, "unexpected extra provider request (including title generation)");
  } catch (error) {
    return deny(response, error instanceof Error ? error.message : String(error));
  }
});

server.on("connect", async (request, socket) => {
  socket.on("error", () => {});
  await appendFile(args.requests, `${JSON.stringify({ state, method: "CONNECT", url: request.url, unexpected: true })}\n`, "utf8").catch(() => {});
  process.stderr.write(`unexpected CONNECT ${request.url}\n`);
  socket.end("HTTP/1.1 409 Conflict\r\nConnection: close\r\n\r\n");
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

function providerTool(url, response, id, name, value) {
  if (url.endsWith("/responses")) return responsesTool(response, id, name, value);
  return chatTool(response, id, name, value);
}

function providerText(url, response, content) {
  if (url.endsWith("/responses")) return responsesText(response, content);
  return chatText(response, content);
}

async function handleCodex(request, response, payload) {
  if (!request.url?.endsWith("/responses")) return deny(response, `unexpected Codex provider endpoint ${request.url}`);
  const strings = collectStrings(payload);
  if (state === 0) {
    const body = await readFile(path.join(args.candidate, "skills", "teach", "SKILL.md"), "utf8");
    assert(countAcross(strings, body) === 1, "Codex first request did not contain the exact canonical teach body once");
    assert(countAcross(strings, codexPrompt) === 1, "Codex first request did not contain the exact prompt once");
    assert(countAcross(strings, expectedFact) === 0, "Codex first request pre-inlined the resource-only fact");
    assert(JSON.stringify(payload).includes('"shell_command"'), "Codex first request did not offer shell_command");
    const selectedSkill = extractInstalledSkillPath(strings);
    const link = body.match(/\[mission-format\]\(([^)]+)\)/)?.[1];
    assert(link, "canonical teach body did not expose mission-format link");
    derivedResourcePath = path.resolve(path.dirname(selectedSkill), link);
    const realResource = await realpath(derivedResourcePath);
    const realSkill = await realpath(path.dirname(selectedSkill));
    assert(isInside(realSkill, realResource), "derived Codex resource escaped the selected skill");
    assert(samePath(realResource, path.join(args.candidate, "skills", "teach", "resources", "mission-format.md")), "derived Codex resource did not resolve to canonical mission-format.md");
    const command = `Get-Content -Raw -LiteralPath '${derivedResourcePath}'`;
    const argumentsJson = { command, workdir: args.candidate, timeout_ms: 10000 };
    await appendFile(args.requests, `${JSON.stringify({ state, method: "POST", url: request.url, payload, derivedResourcePath, response: { tool: "shell_command", arguments: argumentsJson } })}\n`, "utf8");
    state = 1;
    return responsesTool(response, "dyd91-codex-read", "shell_command", argumentsJson);
  }
  if (state === 1) {
    const output = findToolOutput(payload, "dyd91-codex-read");
    assert(output?.includes(expectedFact), "Codex command output omitted the resource-only fact");
    await appendFile(args.requests, `${JSON.stringify({ state, method: "POST", url: request.url, payload, response: { text: expectedFact } })}\n`, "utf8");
    state = 2;
    return responsesText(response, expectedFact);
  }
  await appendFile(args.requests, `${JSON.stringify({ state, method: "POST", url: request.url, payload, unexpected: true })}\n`, "utf8");
  return deny(response, "unexpected extra Codex provider request");
}

function responsesTool(response, callId, name, value) {
  const itemId = `fc_${callId}`;
  const args = JSON.stringify(value);
  const item = { id: itemId, type: "function_call", status: "completed", arguments: args, call_id: callId, name };
  responsesStream(response, [
    ["response.created", { response: responseObject("in_progress", []) }],
    ["response.output_item.added", { output_index: 0, item: { ...item, status: "in_progress", arguments: "" } }],
    ["response.function_call_arguments.delta", { item_id: itemId, output_index: 0, delta: args }],
    ["response.function_call_arguments.done", { item_id: itemId, output_index: 0, arguments: args }],
    ["response.output_item.done", { output_index: 0, item }],
    ["response.completed", { response: responseObject("completed", [item]) }]
  ]);
}

function responsesText(response, content) {
  const itemId = "msg_dyd91";
  const part = { type: "output_text", text: content, annotations: [], logprobs: [] };
  const item = { id: itemId, type: "message", status: "completed", role: "assistant", content: [part] };
  responsesStream(response, [
    ["response.created", { response: responseObject("in_progress", []) }],
    ["response.output_item.added", { output_index: 0, item: { ...item, status: "in_progress", content: [] } }],
    ["response.content_part.added", { item_id: itemId, output_index: 0, content_index: 0, part: { ...part, text: "" } }],
    ["response.output_text.delta", { item_id: itemId, output_index: 0, content_index: 0, delta: content, logprobs: [] }],
    ["response.output_text.done", { item_id: itemId, output_index: 0, content_index: 0, text: content, logprobs: [] }],
    ["response.content_part.done", { item_id: itemId, output_index: 0, content_index: 0, part }],
    ["response.output_item.done", { output_index: 0, item }],
    ["response.completed", { response: responseObject("completed", [item]) }]
  ]);
}

function responseObject(status, output) {
  return {
    id: "resp_dyd91",
    object: "response",
    created_at: 0,
    status,
    error: null,
    incomplete_details: null,
    instructions: null,
    max_output_tokens: null,
    model: "skill-canary",
    output,
    parallel_tool_calls: true,
    previous_response_id: null,
    reasoning: { effort: null, summary: null },
    store: false,
    temperature: 0,
    text: { format: { type: "text" } },
    tool_choice: "auto",
    tools: [],
    top_p: 1,
    truncation: "disabled",
    usage: status === "completed" ? { input_tokens: 1, input_tokens_details: { cached_tokens: 0 }, output_tokens: 1, output_tokens_details: { reasoning_tokens: 0 }, total_tokens: 2 } : null,
    user: null,
    metadata: {}
  };
}

function responsesStream(response, events) {
  response.writeHead(200, {
    "content-type": "text/event-stream",
    "cache-control": "no-cache",
    connection: "keep-alive"
  });
  let sequence = 0;
  for (const [type, value] of events) {
    response.write(`event: ${type}\ndata: ${JSON.stringify({ type, sequence_number: sequence++, ...value })}\n\n`);
  }
  response.end();
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
    /base directory for this skill:\s*([^\n\r"`]+?teach)(?:[\\/]SKILL\.md)?(?=[\n\r"`])/i,
    /base directory(?: is|:)[ \t]*([^\n\r"`]+?teach)(?:[\\/]SKILL\.md)?(?=[\n\r"`])/i,
    /((?:[A-Za-z]:)?[^\n\r"`]*[\\/]skills[\\/]teach)(?:[\\/]SKILL\.md)?(?=[\n\r"`])/i
  ];
  for (const candidate of candidates) {
    const match = normalized.match(candidate);
    if (match) return match[1].trim().replace(/^<|>$/g, "");
  }
  throw new Error("could not derive teach's installed base from the native skill result");
}

function findToolOutput(value, callId) {
  if (Array.isArray(value)) {
    for (const item of value) {
      const found = findToolOutput(item, callId);
      if (found) return found;
    }
  } else if (value && typeof value === "object") {
    if (value.type === "function_call_output" && value.call_id === callId && typeof value.output === "string") return value.output;
    for (const item of Object.values(value)) {
      const found = findToolOutput(item, callId);
      if (found) return found;
    }
  }
}

function collectStrings(value, result = []) {
  if (typeof value === "string") result.push(value);
  else if (Array.isArray(value)) value.forEach(item => collectStrings(item, result));
  else if (value && typeof value === "object") Object.values(value).forEach(item => collectStrings(item, result));
  return result;
}

function countAcross(strings, needle) {
  return strings.reduce((total, value) => total + value.split(needle).length - 1, 0);
}

function extractInstalledSkillPath(strings) {
  for (const value of strings) {
    const match = value.match(/([A-Za-z]:[\\/][^\n\r"`<>]*[\\/]skills[\\/]teach[\\/]SKILL\.md)/i);
    if (match) return match[1];
  }
  throw new Error("Codex first request did not expose the selected installed skill path");
}

function samePath(left, right) {
  return path.resolve(left).toLowerCase() === path.resolve(right).toLowerCase();
}

function isInside(parent, child) {
  const relative = path.relative(parent, child);
  return relative === "" || (!relative.startsWith("..") && !path.isAbsolute(relative));
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
