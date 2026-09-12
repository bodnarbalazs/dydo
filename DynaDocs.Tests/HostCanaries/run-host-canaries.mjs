import { createHash } from "node:crypto";
import { createReadStream } from "node:fs";
import { access, cp, lstat, mkdir, mkdtemp, readFile, readdir, readlink, realpath, rm, stat, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { spawn } from "node:child_process";

const EXPECTED_FACT = "# Mission: {Topic}";
const CODEX_IMPLICIT_PROMPT = "If the project skill teach appears in the model-visible skill inventory, invoke it. Otherwise reply exactly DYDO_TEACH_HIDDEN. Do not use slash-command syntax.";
const PROMPT = "Load the teach skill. Follow its mission-format link from the installed skill base reported or exposed by the host. Reply exactly with the Markdown H1 template from that resource and no other text.";
const OPENCODE_ARCHIVE_SHA256 = "c8c0e0d05ac3dac544a0edfad8de9eb244bf46c6c7a131c38619d40fcf31bd1f";
const OPENCODE_EXE_SHA256 = "c1bdbb18767048e1853af4238311b3f7e16ff2f91b68fb4c7ced3c5175347eea";
const RIPGREP_SHA256 = "673c96c34aff066742faddd08566dbaf9f3a64bcc81f62d7260ce12d4a3c0e84";
const TIMEOUT = 120_000;
const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const options = parseArgs(process.argv.slice(2));
const candidate = resolve(options.candidate);
const evidence = resolve(options.evidence);
let checkout;
const manifest = { issue: "DYD-91 — Make one canonical skill tree easy to install for Claude, Codex, and OpenCode", startedAt: new Date().toISOString(), commands: [], assertions: [], environment: {}, artifacts: {} };

try {
  await prepareCandidate();
  if (!options.only || options.only === "claude") await runClaudeCanary();
  if (!options.only || options.only === "codex") await runCodexCanary();
  if (!options.only || options.only === "opencode") await runOpenCodeCanary();
  manifest.finishedAt = new Date().toISOString();
  manifest.result = "PASS";
  await writeManifest();
  process.stdout.write(`Host canaries passed. Evidence: ${evidence}\n`);
} catch (error) {
  manifest.finishedAt = new Date().toISOString();
  manifest.result = "FAIL";
  manifest.error = error instanceof Error ? error.stack : String(error);
  await mkdir(evidence, { recursive: true }).catch(() => {});
  await writeManifest().catch(() => {});
  process.stderr.write(`${manifest.error}\n`);
  process.exitCode = 1;
} finally {
  if (checkout) await rm(checkout, { recursive: true, force: true }).catch(() => {});
}

async function prepareCandidate() {
  assert(process.platform === "win32", "the pinned host-canary contract currently requires Windows");
  const status = await run("git", ["status", "--porcelain=v1", "--untracked-files=all"], { cwd: candidate });
  assert(status.stdout.trim() === "", "candidate must be Git-clean before a host gate is recorded");
  const sha = (await run("git", ["rev-parse", "HEAD"], { cwd: candidate })).stdout.trim();
  assert(/^[0-9a-f]{40}$/.test(sha), "candidate HEAD was not a commit SHA");
  manifest.candidateSha = sha;

  await rm(evidence, { recursive: true, force: true });
  await mkdir(evidence, { recursive: true });
  checkout = await mkdtemp(join(tmpdir(), "dyd91-host-canary-"));
  await mkdir(checkout, { recursive: true });
  await archiveCommit(sha, checkout);
  await run(process.execPath, ["setup-skills.mjs"], { cwd: checkout });
  await run(process.execPath, ["setup-skills.mjs"], { cwd: checkout });
  const isolatedStatus = await run("git", ["-C", candidate, "status", "--porcelain=v1"]);
  assert(isolatedStatus.stdout.trim() === "", "setup changed the source candidate");
  manifest.environment.candidate = checkout;
}

async function runClaudeCanary() {
  const version = (await run("claude", ["--version"])).stdout.trim();
  manifest.environment.claude = version;
  const common = ["--print", "--output-format", "stream-json", "--verbose", "--no-session-persistence", "--permission-mode", "dontAsk", "--allowedTools", "Skill,Read"];
  const implicitPrompt = "If the project skill teach appears in the model-visible skill inventory, invoke it. Otherwise reply exactly DYDO_TEACH_HIDDEN. Do not use slash-command syntax.";
  const implicit = await run("claude", [...common, implicitPrompt], { cwd: checkout });
  await writeArtifact("claude-implicit.ndjson", implicit.stdout);
  const explicit = await run("claude", [...common, `/teach ${PROMPT}`], { cwd: checkout });
  await writeArtifact("claude-explicit.ndjson", explicit.stdout);

  const implicitEvents = parseNdjson(implicit.stdout, "Claude implicit output");
  const explicitEvents = parseNdjson(explicit.stdout, "Claude explicit output");
  const implicitText = JSON.stringify(implicitEvents);
  const explicitText = JSON.stringify(explicitEvents);
  assert(implicitText.includes("DYDO_TEACH_HIDDEN"), "Claude implicit canary did not return the hidden marker");
  assert(!implicitText.includes("mission-format.md") && !implicitText.includes(EXPECTED_FACT), "Claude implicitly exposed teach");
  assert(explicitText.includes("teach") && explicitText.includes("mission-format.md"), "Claude explicit canary did not load teach");
  assert(explicitText.includes(EXPECTED_FACT), "Claude explicit canary did not read the resource fact");
  assert(finalText(explicitEvents) === EXPECTED_FACT, "Claude explicit final response was not exact");
  pass("Claude keeps teach explicit-only and resolves its linked resource through the installed base");
}

async function runCodexCanary() {
  const resolvedCodex = await resolveCodexExecutable();
  const version = (await run(resolvedCodex, ["--version"])).stdout.trim();
  manifest.environment.codex = version;
  const yaml = await readFile(join(checkout, "skills", "teach", "agents", "openai.yaml"), "utf8");
  assert(yaml.includes("allow_implicit_invocation: false"), "canonical teach lost its Codex explicit-only control");
  await recordCodexProjection();

  const isolation = join(evidence, "codex-isolation");
  const home = join(isolation, "home");
  for (const name of ["home", "appdata", "localappdata", "temp"]) await mkdir(join(isolation, name), { recursive: true });
  const requestsPath = join(evidence, "codex-provider-requests.ndjson");
  const provider = await startProvider(requestsPath, "codex", checkout);
  const config = `model = "skill-canary"\nmodel_provider = "dyd91_loopback"\napproval_policy = "never"\nsandbox_mode = "read-only"\ndisable_response_storage = true\n\n[model_providers.dyd91_loopback]\nname = "DYD-91 loopback Responses mock"\nbase_url = "http://127.0.0.1:${provider.port}/v1"\nwire_api = "responses"\nrequires_openai_auth = false\nsupports_websockets = false\nrequest_max_retries = 0\nstream_max_retries = 0\n`;
  const configPath = join(home, "config.toml");
  await writeFile(configPath, config, "utf8");
  const env = codexEnv(isolation, home, resolvedCodex, provider.port);
  manifest.environment.codexIsolation = { executable: resolvedCodex, argv: [resolvedCodex, "app-server", "--stdio"], env: sanitizeCodexEnv(env, isolation, resolvedCodex) };
  manifest.artifacts["codex-isolation/config.toml"] = { sha256: await sha256(configPath), bytes: (await stat(configPath)).size };

  const rpc = startJsonRpc(resolvedCodex, ["app-server", "--stdio"], { cwd: checkout, env });
  try {
    await rpc.request({ method: "initialize", id: 1, params: { clientInfo: { name: "dyd91-skill-canary", title: "DYD-91 skill canary", version: "1.0.0" }, capabilities: {} } });
    rpc.send({ method: "initialized", params: {} });
    const inventoryResponse = await rpc.request({ method: "skills/list", id: 2, params: { cwds: [checkout], forceReload: true } });
    const records = findSkillRecords(inventoryResponse.result, "teach");
    assert(records.length === 1, `Codex inventory returned ${records.length} enabled repository teach skills`);
    const record = records[0];
    assert(record.description === "Teach the human a new skill or concept, within this workspace.", "Codex inventory lost teach's exact description");
    const selectedPath = record.path;
    await writeArtifact("codex-explicit.json", JSON.stringify({ selectedSkill: record }, null, 2));
    assert(typeof selectedPath === "string" && samePath(selectedPath, join(checkout, "skills", "teach", "SKILL.md")), `Codex inventory did not supply teach's canonical SKILL.md path: ${selectedPath}`);
    assert(samePath(await realpath(selectedPath), join(checkout, "skills", "teach", "SKILL.md")), "Codex inventory path did not real-resolve to canonical teach");

    const implicitThread = await rpc.request({ method: "thread/start", id: 3, params: { cwd: checkout, model: "skill-canary", modelProvider: "dyd91_loopback", approvalPolicy: "never", sandbox: "read-only", ephemeral: true } });
    const implicitThreadId = implicitThread.result?.thread?.id;
    assert(typeof implicitThreadId === "string", "Codex implicit thread/start did not return result.thread.id");
    const implicitStart = rpc.mark();
    rpc.send({ method: "turn/start", id: 4, params: { threadId: implicitThreadId, cwd: checkout, sandboxPolicy: { type: "readOnly", networkAccess: false }, input: [{ type: "text", text: CODEX_IMPLICIT_PROMPT }] } });
    await rpc.wait(message => message.id === 4, implicitStart);
    await rpc.wait(message => message.method === "turn/completed", implicitStart);
    const implicitIncoming = rpc.incomingSince(implicitStart);
    assert(!implicitIncoming.some(isCodexToolItem), "Codex implicit control emitted a tool item");
    assert(exactCodexFinal(implicitIncoming) === "DYDO_TEACH_HIDDEN", "Codex implicit control final was not exactly DYDO_TEACH_HIDDEN");
    await writeArtifact("codex-implicit.ndjson", implicitIncoming.map(message => JSON.stringify(message)).join("\n") + "\n");

    const explicitThread = await rpc.request({ method: "thread/start", id: 5, params: { cwd: checkout, model: "skill-canary", modelProvider: "dyd91_loopback", approvalPolicy: "never", sandbox: "read-only", ephemeral: true } });
    const explicitThreadId = explicitThread.result?.thread?.id;
    assert(typeof explicitThreadId === "string", "Codex explicit thread/start did not return result.thread.id");
    const explicitStart = rpc.mark();
    rpc.send({ method: "turn/start", id: 6, params: { threadId: explicitThreadId, cwd: checkout, sandboxPolicy: { type: "readOnly", networkAccess: false }, input: [{ type: "skill", name: "teach", path: selectedPath }, { type: "text", text: PROMPT }] } });
    await rpc.wait(message => message.id === 6, explicitStart);
    await rpc.wait(message => message.method === "turn/completed", explicitStart);
    const incoming = rpc.incomingSince(explicitStart);
    const started = incoming.findIndex(message => message.method === "turn/started");
    const command = incoming.findIndex(message => message.method === "item/completed" && JSON.stringify(message).includes("Get-Content -Raw -LiteralPath") && JSON.stringify(message).includes("mission-format.md") && JSON.stringify(message).includes('"exitCode":0'));
    const message = incoming.findIndex(item => item.method === "item/completed" && findString(item, value => value === EXPECTED_FACT));
    const completed = incoming.findIndex(item => item.method === "turn/completed" && !JSON.stringify(item).includes('"status":"failed"'));
    assert(started >= 0 && command > started && message > command && completed > message, "Codex transcript lacked the ordered successful turn/read/agent-message/completion sequence");

    const providerEntries = parseNdjson(await readFile(requestsPath, "utf8"), "Codex provider requests");
    const providerPosts = providerEntries.filter(entry => entry.method === "POST");
    assert(providerPosts.length === 3, `Codex made ${providerPosts.length} provider POSTs instead of the expected three`);
    assert(!providerEntries.some(entry => entry.unexpected), "Codex made an unexpected provider or outbound request");
    assert(!providerEntries.some(entry => entry.method === "CONNECT"), "Codex deny-proxy transcript was not empty");
    manifest.artifacts["codex-provider-requests.ndjson"] = { sha256: await sha256(requestsPath), bytes: (await stat(requestsPath)).size };
    pass("Codex app-server inventory, implicit omission, structured explicit resource proof, and zero-egress gate passed");
  } finally {
    await writeArtifact("codex-live.ndjson", rpc.transcript.map(entry => JSON.stringify(entry)).join("\n") + "\n");
    await writeArtifact("codex-app-server.stderr.txt", rpc.stderr());
    if (await fileExists(requestsPath)) {
      manifest.artifacts["codex-provider-requests.ndjson"] = { sha256: await sha256(requestsPath), bytes: (await stat(requestsPath)).size };
    }
    rpc.stop();
    await rpc.done.catch(() => {});
    provider.child.kill("SIGTERM");
    await provider.done.catch(() => {});
  }
}

async function recordCodexProjection() {
  const projectedDirectory = join(checkout, ".agents", "skills", "teach");
  const projectedBody = join(projectedDirectory, "SKILL.md");
  const projectedResource = join(projectedDirectory, "resources", "mission-format.md");
  const canonicalBody = join(checkout, "skills", "teach", "SKILL.md");
  const canonicalResource = join(checkout, "skills", "teach", "resources", "mission-format.md");
  const entry = await lstat(projectedDirectory);
  assert(entry.isSymbolicLink(), "Codex teach projection was not a directory link/junction");
  assert((await stat(projectedDirectory)).isDirectory(), "Codex teach projection did not target a directory");
  const storedTarget = await readlink(projectedDirectory);
  const projectedBodyReal = await realpath(projectedBody);
  const projectedResourceReal = await realpath(projectedResource);
  assert(samePath(projectedBodyReal, canonicalBody), "Codex projected body did not real-resolve canonical");
  assert(samePath(projectedResourceReal, canonicalResource), "Codex projected resource did not real-resolve canonical");
  const bodyHashes = { projected: await sha256(projectedBody), canonical: await sha256(canonicalBody) };
  const resourceHashes = { projected: await sha256(projectedResource), canonical: await sha256(canonicalResource) };
  assert(bodyHashes.projected === bodyHashes.canonical, "Codex projected body bytes differed from canonical");
  assert(resourceHashes.projected === resourceHashes.canonical, "Codex projected resource bytes differed from canonical");
  const cleanBefore = (await run("git", ["status", "--porcelain=v1", "--untracked-files=all"], { cwd: candidate })).stdout;
  const cleanAfter = (await run("git", ["status", "--porcelain=v1", "--untracked-files=all"], { cwd: candidate })).stdout;
  assert(cleanBefore === "" && cleanAfter === "", "Codex projection proof observed a dirty source candidate");
  const setupRuns = manifest.commands.filter(command => command.command === process.execPath && command.args[0] === "setup-skills.mjs").map(command => ({ argv: [command.command, ...command.args], exitCode: command.exitCode }));
  assert(setupRuns.length === 2 && setupRuns.every(runResult => runResult.exitCode === 0), "Codex projection proof did not observe two successful setup runs");
  await writeArtifact("codex-projection.json", JSON.stringify({
    candidateSha: manifest.candidateSha,
    setupRuns,
    link: { path: projectedDirectory, entryType: process.platform === "win32" ? "directory-junction" : "directory-symlink", storedTarget },
    body: { projectedLexicalPath: projectedBody, canonicalRealPath: projectedBodyReal, sha256: bodyHashes, readSucceeded: (await readFile(projectedBody, "utf8")).length > 0 },
    resource: { projectedLexicalPath: projectedResource, canonicalRealPath: projectedResourceReal, sha256: resourceHashes, readSucceeded: (await readFile(projectedResource, "utf8")).includes(EXPECTED_FACT) },
    git: { before: cleanBefore, after: cleanAfter }
  }, null, 2));
}

async function runOpenCodeCanary() {
  assert(options.opencodeArchive, "--opencode-archive is required for the OpenCode canary");
  assert(options.ripgrep, "--ripgrep is required for the OpenCode canary");
  const archive = resolve(options.opencodeArchive);
  const suppliedRg = resolve(options.ripgrep);
  assert(await sha256(archive) === OPENCODE_ARCHIVE_SHA256, "OpenCode archive hash mismatch");
  assert(await sha256(suppliedRg) === RIPGREP_SHA256, "supplied Ripgrep hash mismatch");

  const originalRoots = nativeOpenCodeRoots();
  const before = await fingerprintRoots(originalRoots);
  const isolation = join(evidence, "opencode-isolation");
  const physicalPaths = Object.fromEntries(["home", "config", "data", "cache", "state", "temp", "appdata", "localappdata", "custom-config", "bin"].map(name => [name, join(isolation, name)]));
  for (const value of Object.values(physicalPaths)) await mkdir(value, { recursive: true });
  await run("tar", ["-xf", archive, "-C", physicalPaths.bin]);
  const physicalExe = await findFile(physicalPaths.bin, "opencode.exe");
  assert(await sha256(physicalExe) === OPENCODE_EXE_SHA256, "extracted OpenCode executable hash mismatch");
  await cp(suppliedRg, join(physicalPaths.bin, "rg.exe"));

  const drive = await unusedDrive();
  await run("subst", [`${drive}:`, isolation]);
  const paths = Object.fromEntries(Object.keys(physicalPaths).map(name => [name, `${drive}:\\${name}`]));
  const exe = `${drive}:\\bin\\${physicalExe.slice(physicalPaths.bin.length + 1)}`;
  let provider;
  try {
    const rgVersion = (await run(`${drive}:\\bin\\rg.exe`, ["--version"])).stdout.split(/\r?\n/)[0];
    assert(/^ripgrep 15\.2\.0(?: \(rev [0-9a-f]+\))?$/.test(rgVersion), `unexpected Ripgrep version: ${rgVersion}`);
    const version = (await run(exe, ["--version"], { env: portableEnv(paths) })).stdout.trim();
    assert(version === "1.18.30", `unexpected OpenCode version: ${version}`);
    manifest.environment.opencode = { version, archiveSha256: OPENCODE_ARCHIVE_SHA256, executableSha256: OPENCODE_EXE_SHA256, ripgrepSha256: RIPGREP_SHA256, rgVersion };

    const requestsPath = join(evidence, "opencode-provider-requests.ndjson");
    provider = await startProvider(requestsPath);
    const configPath = join(paths["custom-config"], "opencode.json");
    await writeFile(configPath, JSON.stringify({
      $schema: "https://opencode.ai/config.json",
      share: "disabled",
      autoupdate: false,
      provider: { openai: { options: { baseURL: `http://127.0.0.1:${provider.port}/v1`, apiKey: "dyd91-loopback" }, models: { "skill-canary": { name: "skill-canary" } } } }
    }, null, 2));
    const env = portableEnv(paths, configPath, provider.port);
    const originalValues = Object.values(process.env).filter(value => typeof value === "string" && /\\Users\\/i.test(value));
    assert(!Object.values(env).some(value => originalValues.includes(value)), "isolated child environment leaked an original user path value");
    manifest.environment.opencodeIsolation = Object.fromEntries(Object.entries(paths).map(([key, value]) => [key, value]));

    const debugPaths = await run(exe, ["--pure", "debug", "paths"], { cwd: checkout, env });
    const debugPathValues = extractAbsolutePaths(debugPaths.stdout);
    const resolvedDebugPaths = await Promise.all(debugPathValues.map(value => realpath(value)));
    assert(resolvedDebugPaths.length > 0 && resolvedDebugPaths.every(value => isInside(isolation, value)), `OpenCode reported a mutable path outside isolation: ${JSON.stringify(debugPathValues)}`);
    const inventory = await run(exe, ["--pure", "debug", "skill"], { cwd: checkout, env });
    await writeArtifact("opencode-inventory.json", inventory.stdout);
    const parsedInventory = JSON.parse(inventory.stdout);
    const expectedNames = (await readdir(join(checkout, "skills"), { withFileTypes: true })).filter(entry => entry.isDirectory()).map(entry => entry.name).sort();
    const actualNames = collectSkillNames(parsedInventory, checkout).sort();
    assert(JSON.stringify(actualNames) === JSON.stringify(expectedNames), "OpenCode inventory did not exactly match canonical skills/*");
    for (const name of expectedNames) {
      const reported = findString(parsedInventory, value => new RegExp(`[\\\\/]skills[\\\\/]${escapeRegex(name)}(?:[\\\\/]SKILL\\.md)?$`, "i").test(value));
      assert(reported, `OpenCode inventory omitted the canonical location for ${name}`);
      assert(isInside(join(checkout, "skills", name), await realpath(reported)), `OpenCode inventory location for ${name} was not canonical`);
    }

    const live = await run(exe, ["--pure", "run", "--dir", ".", "--title", "DYD-91-skill-canary", "--model", "openai/skill-canary", "--format", "json", PROMPT], { cwd: checkout, env });
    await writeArtifact("opencode-live.ndjson", live.stdout);
    const events = parseNdjson(live.stdout, "OpenCode live output");
    const skillIndex = events.findIndex(event => event.type === "tool_use" && event.part?.tool === "skill" && event.part?.state?.status === "completed" && event.part?.state?.input?.name === "teach");
    const readIndex = events.findIndex(event => event.type === "tool_use" && event.part?.tool === "read" && event.part?.state?.status === "completed" && event.part?.state?.input?.filePath?.endsWith("mission-format.md"));
    const textIndex = events.findIndex(event => event.type === "text" && event.part?.text === EXPECTED_FACT);
    assert(skillIndex >= 0 && readIndex > skillIndex && textIndex > readIndex, "OpenCode live events did not record the ordered completed skill/read/final sequence");
    assert(events[skillIndex].part.state.output.includes("[mission-format](resources/mission-format.md)"), "OpenCode's native skill result lost the resource link");
    const readPath = await realpath(events[readIndex].part.state.input.filePath);
    assert(samePath(readPath, join(checkout, "skills", "teach", "resources", "mission-format.md")), "OpenCode read did not resolve to the canonical resource");
    assert(events[readIndex].part.state.output.includes(EXPECTED_FACT), "OpenCode native read result omitted the resource fact");
    assert(events.filter(event => event.type === "text").map(event => event.part?.text).join("") === EXPECTED_FACT, "OpenCode live final response was not exact");
    const requests = parseNdjson(await readFile(requestsPath, "utf8"), "OpenCode provider requests");
    assert(requests.length === 3, `OpenCode made ${requests.length} provider requests instead of the expected three`);
    const firstRequest = JSON.stringify(requests[0].payload);
    for (const name of expectedNames) assert(firstRequest.includes(`<name>${name}</name>`), `OpenCode's first provider request omitted ${name} from the native skill inventory`);
    manifest.artifacts["opencode-provider-requests.ndjson"] = { sha256: await sha256(requestsPath), bytes: (await stat(requestsPath)).size };
    pass("Pinned OpenCode discovered canonical skills directly and completed the deterministic loopback skill/read proof");
  } finally {
    if (provider) {
      provider.child.kill("SIGTERM");
      await provider.done.catch(() => {});
    }
    await run("subst", [`${drive}:`, "/D"]).catch(() => {});
    const after = await fingerprintRoots(originalRoots);
    assert(JSON.stringify(after) === JSON.stringify(before), "OpenCode changed a bounded native user-product directory");
  }
}

async function startProvider(requestsPath, mode = "opencode", providerCandidate) {
  await writeFile(requestsPath, "");
  const providerScript = join(scriptDirectory, "openai-sse-provider.mjs");
  const providerArgs = [providerScript, "--requests", requestsPath, "--mode", mode];
  if (providerCandidate) providerArgs.push("--candidate", providerCandidate);
  const child = spawn(process.execPath, providerArgs, { stdio: ["ignore", "pipe", "pipe"], windowsHide: true });
  let stdout = "";
  let stderr = "";
  child.stdout.setEncoding("utf8").on("data", chunk => { stdout += chunk; });
  child.stderr.setEncoding("utf8").on("data", chunk => { stderr += chunk; });
  const done = new Promise((resolveDone, reject) => child.once("exit", code => code === 0 ? resolveDone() : reject(new Error(`provider exited ${code}: ${stderr}`))));
  done.catch(() => {});
  await waitFor(() => parseJsonLines(stdout)[0]?.port, "loopback provider port");
  return { child, done, port: parseJsonLines(stdout)[0].port };
}

async function resolveCodexExecutable() {
  const command = "(Get-Command codex -CommandType Application).Source";
  const result = await run("powershell.exe", ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", command]);
  const candidates = result.stdout.split(/\r?\n/).map(value => value.trim()).filter(value => value.toLowerCase().endsWith("codex.exe"));
  for (const executable of candidates) {
    try {
      await access(executable);
      return executable;
    } catch {}
  }
  throw new Error(`could not resolve codex.exe: ${result.stdout.trim()}`);
}

function codexEnv(isolation, home, executable, port) {
  const systemRoot = process.env.SystemRoot ?? "C:\\Windows";
  const denyProxy = `http://127.0.0.1:${port}`;
  return {
    SystemRoot: systemRoot,
    WINDIR: systemRoot,
    ComSpec: join(systemRoot, "System32", "cmd.exe"),
    PATHEXT: ".COM;.EXE;.BAT;.CMD",
    PATH: [dirname(executable), systemRoot, join(systemRoot, "System32"), join(systemRoot, "System32", "WindowsPowerShell", "v1.0")].join(";"),
    HOME: home,
    USERPROFILE: home,
    APPDATA: join(isolation, "appdata"),
    LOCALAPPDATA: join(isolation, "localappdata"),
    TEMP: join(isolation, "temp"),
    TMP: join(isolation, "temp"),
    CODEX_HOME: home,
    CODEX_APP_SERVER_DISABLE_MANAGED_CONFIG: "1",
    CODEX_INTERNAL_APP_SERVER_REMOTE_CONTROL_DISABLED: "1",
    CODEX_MANAGED_BY_NPM: "1",
    HTTP_PROXY: denyProxy,
    HTTPS_PROXY: denyProxy,
    ALL_PROXY: denyProxy,
    NO_PROXY: "127.0.0.1,localhost",
    no_proxy: "127.0.0.1,localhost"
  };
}

function sanitizeCodexEnv(env, isolation, executable) {
  const executableDirectory = dirname(executable);
  return Object.fromEntries(Object.entries(env).map(([key, value]) => [key, value
    .replaceAll(isolation, "<codex-isolation>")
    .replaceAll(executableDirectory, "<codex-bin>")
    .replace(/http:\/\/127\.0\.0\.1:\d+/g, "<loopback>")]));
}

function startJsonRpc(command, args, { cwd, env }) {
  const child = spawn(command, args, { cwd, env, stdio: ["pipe", "pipe", "pipe"], windowsHide: true });
  const transcript = [];
  const incoming = [];
  let stdoutBuffer = "";
  let stderr = "";
  let parseError;
  child.stdout.setEncoding("utf8").on("data", chunk => {
    stdoutBuffer += chunk;
    const lines = stdoutBuffer.split(/\r?\n/);
    stdoutBuffer = lines.pop() ?? "";
    for (const line of lines.filter(Boolean)) {
      try {
        const message = JSON.parse(line);
        incoming.push(message);
        transcript.push({ direction: "in", message });
      } catch (error) {
        parseError = new Error(`malformed Codex app-server JSON: ${line}: ${error.message}`);
      }
    }
  });
  child.stderr.setEncoding("utf8").on("data", chunk => { stderr += chunk; });
  const done = exitCode(child);
  function send(message) {
    assert(!parseError, parseError?.message);
    transcript.push({ direction: "out", message });
    child.stdin.write(`${JSON.stringify(message)}\n`);
  }
  async function wait(predicate, after = 0) {
    await waitFor(() => {
      if (parseError) throw parseError;
      return incoming.slice(after).some(predicate);
    }, "Codex app-server message");
    return incoming.slice(after).find(predicate);
  }
  async function request(message) {
    send(message);
    const response = await wait(item => item.id === message.id);
    assert(!response.error, `Codex app-server ${message.method} failed: ${JSON.stringify(response.error)}`);
    return response;
  }
  return {
    transcript,
    request,
    send,
    wait,
    mark: () => incoming.length,
    incomingSince: after => incoming.slice(after),
    stderr: () => stderr,
    stop: () => child.kill(),
    done
  };
}

function findSkillRecords(value, name, result = []) {
  if (Array.isArray(value)) value.forEach(item => findSkillRecords(item, name, result));
  else if (value && typeof value === "object") {
    if (value.name === name && typeof value.path === "string" && value.enabled !== false) result.push(value);
    else Object.values(value).forEach(item => findSkillRecords(item, name, result));
  }
  return result;
}

function portableEnv(paths, configPath, port) {
  const systemRoot = process.env.SystemRoot ?? "C:\\Windows";
  const env = {
    SystemRoot: systemRoot,
    WINDIR: systemRoot,
    ComSpec: join(systemRoot, "System32", "cmd.exe"),
    PATHEXT: ".COM;.EXE;.BAT;.CMD",
    PATH: [paths.bin, systemRoot, join(systemRoot, "System32"), join(systemRoot, "System32", "WindowsPowerShell", "v1.0")].join(";"),
    HOME: paths.home,
    USERPROFILE: paths.home,
    HOMEDRIVE: paths.home.slice(0, 2),
    HOMEPATH: paths.home.slice(2),
    APPDATA: paths.appdata,
    LOCALAPPDATA: paths.localappdata,
    TEMP: paths.temp,
    TMP: paths.temp,
    XDG_CONFIG_HOME: paths.config,
    XDG_DATA_HOME: paths.data,
    XDG_CACHE_HOME: paths.cache,
    XDG_STATE_HOME: paths.state,
    OPENCODE_DISABLE_AUTOUPDATE: "1",
    OPENCODE_DISABLE_DEFAULT_PLUGINS: "1",
    OPENCODE_DISABLE_LSP_DOWNLOAD: "1",
    OPENCODE_DISABLE_MODELS_FETCH: "1",
    OPENCODE_DISABLE_PRUNE: "1",
    OPENCODE_DISABLE_SHARE: "1",
    NO_PROXY: port ? `127.0.0.1,localhost` : "127.0.0.1,localhost",
    no_proxy: port ? `127.0.0.1,localhost` : "127.0.0.1,localhost"
  };
  if (configPath) {
    const denyProxy = `http://127.0.0.1:${port}`;
    env.OPENCODE_CONFIG = configPath;
    env.HTTP_PROXY = denyProxy;
    env.HTTPS_PROXY = denyProxy;
    env.ALL_PROXY = denyProxy;
  }
  return env;
}

async function archiveCommit(sha, destination) {
  const git = spawn("git", ["archive", "--format=tar", sha], { cwd: candidate, stdio: ["ignore", "pipe", "pipe"], windowsHide: true });
  const tar = spawn("tar", ["-xf", "-", "-C", destination], { stdio: ["pipe", "ignore", "pipe"], windowsHide: true });
  git.stdout.pipe(tar.stdin);
  const [gitCode, tarCode] = await Promise.all([exitCode(git), exitCode(tar)]);
  assert(gitCode === 0 && tarCode === 0, `could not materialize exact candidate ${sha}`);
}

async function run(command, args, { cwd, env } = {}) {
  const started = Date.now();
  const child = spawn(command, args, { cwd, env: env ?? process.env, stdio: ["ignore", "pipe", "pipe"], windowsHide: true });
  let stdout = "";
  let stderr = "";
  child.stdout.setEncoding("utf8").on("data", chunk => { stdout += chunk; });
  child.stderr.setEncoding("utf8").on("data", chunk => { stderr += chunk; });
  const timer = setTimeout(() => killTree(child.pid), TIMEOUT);
  const code = await exitCode(child);
  clearTimeout(timer);
  manifest.commands.push({ command, args, cwd, durationMs: Date.now() - started, exitCode: code, stderr: redact(stderr) });
  assert(code === 0, `${command} ${args.join(" ")} exited ${code}: ${redact(stderr)}`);
  return { stdout, stderr };
}

function killTree(pid) {
  if (!pid) return;
  spawn("taskkill", ["/pid", String(pid), "/t", "/f"], { stdio: "ignore", windowsHide: true });
}

function exitCode(child) {
  return new Promise((resolveExit, reject) => {
    child.once("error", reject);
    child.once("exit", code => resolveExit(code ?? -1));
  });
}

async function waitFor(predicate, label) {
  const deadline = Date.now() + TIMEOUT;
  while (Date.now() < deadline) {
    if (predicate()) return;
    await new Promise(resolveWait => setTimeout(resolveWait, 25));
  }
  throw new Error(`timed out waiting for ${label}`);
}

function parseArgs(argv) {
  const result = {};
  for (let index = 0; index < argv.length; index += 2) {
    const key = argv[index];
    const value = argv[index + 1];
    if (!value || !["--candidate", "--evidence", "--opencode-archive", "--ripgrep", "--only"].includes(key)) throw new Error(`unknown or incomplete argument: ${key}`);
    result[key.slice(2).replaceAll("-", "_")] = value;
  }
  assert(result.candidate && result.evidence, "usage: run-host-canaries.mjs --candidate <path> --evidence <path> --opencode-archive <zip> --ripgrep <exe>");
  if (result.only) assert(["claude", "codex", "opencode"].includes(result.only), "--only must be claude, codex, or opencode");
  return { candidate: result.candidate, evidence: result.evidence, opencodeArchive: result.opencode_archive, ripgrep: result.ripgrep, only: result.only };
}

async function writeArtifact(name, content) {
  const target = join(evidence, name);
  await writeFile(target, content, "utf8");
  manifest.artifacts[name] = { sha256: await sha256(target), bytes: (await stat(target)).size };
}

async function writeManifest() {
  await mkdir(evidence, { recursive: true });
  await writeFile(join(evidence, "manifest.json"), JSON.stringify(manifest, null, 2), "utf8");
}

function pass(assertion) { manifest.assertions.push({ assertion, result: "PASS" }); }
function assert(condition, message) { if (!condition) throw new Error(message); }
function redact(value) { return value.replaceAll(candidate, "<candidate>").replaceAll(evidence, "<evidence>"); }
function count(value, needle) { return value.split(needle).length - 1; }
function samePath(left, right) { return resolve(left).toLowerCase() === resolve(right).toLowerCase(); }
function isInside(parent, child) { const relative = resolve(child).slice(resolve(parent).length); return samePath(parent, child) || (relative.startsWith(sep) && !relative.includes(`..${sep}`)); }
function escapeRegex(value) { return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"); }
function parseJsonLines(value) { return value.split(/\r?\n/).filter(Boolean).flatMap(line => { try { return [JSON.parse(line)]; } catch { return []; } }); }
function parseNdjson(value, label) { const lines = value.split(/\r?\n/).filter(Boolean); const parsed = lines.map(line => JSON.parse(line)); assert(parsed.length > 0, `${label} was empty`); return parsed; }
function findString(value, predicate) { if (typeof value === "string") return predicate(value) ? value : undefined; if (Array.isArray(value)) { for (const item of value) { const found = findString(item, predicate); if (found) return found; } } else if (value && typeof value === "object") { for (const item of Object.values(value)) { const found = findString(item, predicate); if (found) return found; } } }
function collectSkillNames(value, projectRoot) {
  if (Array.isArray(value)) return value.flatMap(item => collectSkillNames(item, projectRoot));
  if (!value || typeof value !== "object") return [];
  if (typeof value.name === "string" && typeof value.location === "string") {
    return isInside(projectRoot, value.location) ? [value.name] : [];
  }
  return Object.values(value).flatMap(item => collectSkillNames(item, projectRoot));
}
function extractAbsolutePaths(value) { return [...value.matchAll(/[A-Za-z]:[\\/][^\r\n"]+/g)].map(match => match[0].trim()); }
function finalText(events) { const strings = []; walk(events, value => { if (typeof value === "string") strings.push(value); }); return strings.filter(value => value.includes(EXPECTED_FACT)).at(-1)?.trim(); }
function exactCodexFinal(messages) {
  const strings = [];
  for (const message of messages.filter(value => value.method === "item/completed")) walk(message, value => { if (typeof value === "string") strings.push(value); });
  return strings.filter(value => value === "DYDO_TEACH_HIDDEN").at(-1);
}
function isCodexToolItem(message) {
  if (message.method !== "item/started" && message.method !== "item/completed") return false;
  return /"type":"(?:commandExecution|mcpToolCall|dynamicToolCall|webSearch)"/.test(JSON.stringify(message));
}
function walk(value, visit) { visit(value); if (Array.isArray(value)) value.forEach(item => walk(item, visit)); else if (value && typeof value === "object") Object.values(value).forEach(item => walk(item, visit)); }
async function sha256(file) {
  const hash = createHash("sha256");
  await new Promise((resolveHash, reject) => {
    const input = createReadStream(file);
    input.on("data", chunk => hash.update(chunk));
    input.on("end", resolveHash);
    input.on("error", reject);
  });
  return hash.digest("hex");
}
async function findFile(root, name) { for (const entry of await readdir(root, { withFileTypes: true })) { const value = join(root, entry.name); if (entry.isDirectory()) { const found = await findFile(value, name).catch(() => undefined); if (found) return found; } else if (entry.name.toLowerCase() === name.toLowerCase()) return value; } throw new Error(`${name} was not found under ${root}`); }
async function fileExists(path) { try { await access(path); return true; } catch { return false; } }

function nativeOpenCodeRoots() {
  const user = process.env.USERPROFILE;
  assert(user && process.env.APPDATA && process.env.LOCALAPPDATA, "native Windows profile paths were unavailable");
  return [
    join(process.env.APPDATA, "opencode"),
    join(process.env.LOCALAPPDATA, "opencode"),
    join(user, ".config", "opencode"),
    join(user, ".local", "share", "opencode"),
    join(user, ".cache", "opencode"),
    join(user, ".local", "state", "opencode")
  ];
}

async function fingerprintRoots(roots) {
  const result = {};
  for (const root of roots) result[root] = await fingerprint(root);
  return result;
}

async function fingerprint(target) {
  try {
    const info = await stat(target);
    if (!info.isDirectory()) return { type: "file", size: info.size, sha256: await sha256(target) };
    const entries = [];
    for (const entry of (await readdir(target, { withFileTypes: true })).sort((a, b) => a.name.localeCompare(b.name))) {
      entries.push([entry.name, await fingerprint(join(target, entry.name))]);
    }
    return { type: "directory", entries };
  } catch (error) {
    if (error?.code === "ENOENT") return { type: "missing" };
    throw new Error(`cannot fingerprint native OpenCode path ${target}: ${error.message}`);
  }
}

async function unusedDrive() {
  for (const letter of "ZYXWVUTSRQPONMLKJIHGFED") {
    try {
      await access(`${letter}:\\`);
    } catch (error) {
      if (error?.code === "ENOENT") return letter;
      throw error;
    }
  }
  throw new Error("no unused drive letter was available for OpenCode isolation");
}
