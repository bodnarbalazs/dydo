import { createHash } from "node:crypto";
import { createReadStream } from "node:fs";
import { access, cp, lstat, mkdir, mkdtemp, readFile, readdir, readlink, realpath, rm, rmdir, stat, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { basename, dirname, isAbsolute, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { spawn } from "node:child_process";
import { isInside, samePath } from "./path-containment.mjs";
import { parseArgs } from "./run-host-canaries-args.mjs";

const EXPECTED_FACT = "# Mission: {Topic}";
const CODEX_IMPLICIT_PROMPT = "If the project skill teach appears in the model-visible skill inventory, invoke it. Otherwise reply exactly DYDO_TEACH_HIDDEN. Do not use slash-command syntax.";
const PROMPT = "Load the teach skill. Follow its mission-format link from the installed skill base reported or exposed by the host. Reply exactly with the Markdown H1 template from that resource and no other text.";
const OPENCODE_ARCHIVE_SHA256 = "c8c0e0d05ac3dac544a0edfad8de9eb244bf46c6c7a131c38619d40fcf31bd1f";
const OPENCODE_EXE_SHA256 = "c1bdbb18767048e1853af4238311b3f7e16ff2f91b68fb4c7ced3c5175347eea";
const RIPGREP_SHA256 = "14231169855ec5205cf5a1b6f1db358ff4aed4247c86b69ce8aae647c77f6680";
const TIMEOUT = 120_000;
const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const options = parseArgs(process.argv.slice(2));
const candidate = resolve(options.candidate);
const evidence = resolve(options.evidence);
const scratchRoot = resolve(options.scratchRoot);
let checkout;
let runRoot;
let hostsRoot;
let scratchRootCreated = false;
let candidatePrepared = false;
let expectedSkills = [];
let candidateFingerprintBefore;
let rejectedAncestorPaths = [];
const manifest = { issue: "DYD-91 — Make one canonical skill tree easy to install for Claude, Codex, and OpenCode", startedAt: new Date().toISOString(), commands: [], assertions: [], environment: {}, artifacts: {} };
manifest.environment.networkEvidenceBoundary = "The deny proxies prove zero proxy-observed external attempts and exact loopback traffic. They do not provide OS-level process-network confinement or prove that a child incapable of honoring proxy variables made no direct connection.";

let failure;
try {
  await prepareCandidate();
  if (!options.only || options.only === "claude") await runClaudeCanary();
  if (!options.only || options.only === "codex") await runCodexCanary();
  if (!options.only || options.only === "opencode") await runOpenCodeCanary();
} catch (error) {
  failure = error;
} finally {
  try {
    await finalizeCandidate();
  } catch (error) {
    if (!failure) failure = error;
    else manifest.cleanupError = error instanceof Error ? error.stack : String(error);
  }
  manifest.finishedAt = new Date().toISOString();
  manifest.result = failure ? "FAIL" : "PASS";
  if (failure) manifest.error = failure instanceof Error ? failure.stack : String(failure);
  await mkdir(evidence, { recursive: true }).catch(() => {});
  await writeManifest().catch(() => {});
  if (failure) {
    process.stderr.write(`${manifest.error}\n`);
    process.exitCode = 1;
  } else {
    process.stdout.write(`Host canaries passed. Evidence: ${evidence}\n`);
  }
}

async function prepareCandidate() {
  assert(process.platform === "win32", "the pinned host-canary contract currently requires Windows");
  const status = await run("git", ["status", "--porcelain=v1", "--untracked-files=all"], { cwd: candidate });
  assert(status.stdout.trim() === "", "candidate must be Git-clean before a host gate is recorded");
  const sha = (await run("git", ["rev-parse", "HEAD"], { cwd: candidate })).stdout.trim();
  assert(/^[0-9a-f]{40}$/.test(sha), "candidate HEAD was not a commit SHA");
  manifest.candidateSha = sha;
  const sourceTree = (await run("git", ["rev-parse", `${sha}^{tree}`], { cwd: candidate })).stdout.trim();
  assert(/^[0-9a-f]{40}$/.test(sourceTree), "candidate tree was not a Git tree SHA");
  manifest.sourceTreeSha = sourceTree;

  await preflightScratchAndEvidence();
  assert(!(await fileExists(evidence)), `retained evidence path already exists: ${evidence}`);
  await mkdir(evidence, { recursive: true });
  await writeArtifact("scratch-preflight.json", JSON.stringify(manifest.scratchPreflight, null, 2));
  if (!(await fileExists(scratchRoot))) {
    await mkdir(scratchRoot, { recursive: true });
    scratchRootCreated = true;
    manifest.scratchLifecycle.push({ action: "create-scratch-root", path: scratchRoot, result: "created" });
  }
  runRoot = await mkdtemp(join(scratchRoot, "DYD-91-"));
  checkout = join(runRoot, "candidate");
  hostsRoot = join(runRoot, "hosts");
  await mkdir(checkout, { recursive: true });
  await mkdir(hostsRoot, { recursive: true });
  manifest.scratchLifecycle.push({ action: "create-run", path: runRoot, result: "created" });
  await archiveCommit(sha, checkout);
  await verifyMaterializedTree(sha, checkout);
  await run("git", ["init", "--quiet"], { cwd: checkout });
  await run("git", ["config", "core.autocrlf", "false"], { cwd: checkout });
  await run("git", ["config", "core.eol", "lf"], { cwd: checkout });
  await run("git", ["config", "core.filemode", "false"], { cwd: checkout });
  await run("git", ["-c", "protocol.file.allow=always", "fetch", "--quiet", "--no-tags", candidate, sha], { cwd: checkout });
  await run("git", ["read-tree", sourceTree], { cwd: checkout });
  const stagedTree = (await run("git", ["write-tree"], { cwd: checkout })).stdout.trim();
  assert(stagedTree === sourceTree, `materialized candidate index ${stagedTree} did not equal source tree ${sourceTree}`);
  await run("git", ["-c", "user.name=DYD-91 host canary", "-c", "user.email=dyd91@example.invalid", "commit", "--quiet", "-m", "Record exact source candidate"], { cwd: checkout });
  const evidenceCommit = (await run("git", ["rev-parse", "HEAD"], { cwd: checkout })).stdout.trim();
  const evidenceTree = (await run("git", ["rev-parse", "HEAD^{tree}"], { cwd: checkout })).stdout.trim();
  assert(evidenceTree === sourceTree, `evidence commit tree ${evidenceTree} did not equal source tree ${sourceTree}`);
  manifest.evidenceRepository = { commitSha: evidenceCommit, treeSha: evidenceTree, stagedTreeSha: stagedTree };
  await run(process.execPath, ["setup-skills.mjs"], { cwd: checkout });
  await run(process.execPath, ["setup-skills.mjs"], { cwd: checkout });
  const nestedStatus = await run("git", ["status", "--porcelain=v1", "--untracked-files=all"], { cwd: checkout });
  assert(nestedStatus.stdout.trim() === "", "setup changed the nested evidence repository");
  const isolatedStatus = await run("git", ["-C", candidate, "status", "--porcelain=v1"]);
  assert(isolatedStatus.stdout.trim() === "", "setup changed the source candidate");
  expectedSkills = await loadExpectedSkills(checkout);
  assert(expectedSkills.length === 31, `candidate exposed ${expectedSkills.length} canonical skills instead of 31`);
  candidateFingerprintBefore = await candidateFingerprint(checkout);
  manifest.candidateFingerprint = { before: candidateFingerprintBefore };
  manifest.environment.candidate = { physicalPath: checkout, sourceSha: sha, sourceTreeSha: sourceTree, evidenceCommitSha: evidenceCommit, evidenceTreeSha: evidenceTree };
  candidatePrepared = true;
}

async function finalizeCandidate() {
  let finalizeFailure;
  try {
    if (candidatePrepared && await fileExists(checkout)) {
      const after = await candidateFingerprint(checkout);
      manifest.candidateFingerprint.after = after;
      assert(after === candidateFingerprintBefore, "host canaries changed candidate bytes, entry types, or link targets");
      const nestedStatus = await run("git", ["status", "--porcelain=v1", "--untracked-files=all"], { cwd: checkout });
      manifest.evidenceRepository.statusAfterHosts = nestedStatus.stdout;
      assert(nestedStatus.stdout.trim() === "", "host canaries changed the nested evidence repository");
    }
  } catch (error) {
    finalizeFailure = error;
  }
  try {
    if (runRoot && await fileExists(runRoot)) {
      await rm(runRoot, { recursive: true, force: true });
      manifest.scratchLifecycle.push({ action: "remove-run", path: runRoot, result: "removed", absent: !(await fileExists(runRoot)) });
      assert(!(await fileExists(runRoot)), "disposable host-canary run directory remained after cleanup");
    }
    if (scratchRootCreated && await fileExists(scratchRoot)) {
      const remaining = await readdir(scratchRoot);
      assert(remaining.length === 0, "newly created scratch root was not empty after run cleanup");
      await rmdir(scratchRoot);
      manifest.scratchLifecycle.push({ action: "remove-owned-scratch-root", path: scratchRoot, result: "removed", absent: !(await fileExists(scratchRoot)) });
    } else if (manifest.scratchLifecycle) {
      manifest.scratchLifecycle.push({ action: "preserve-caller-scratch-root", path: scratchRoot, result: "preserved" });
    }
  } catch (error) {
    if (!finalizeFailure) finalizeFailure = error;
    else manifest.cleanupError = error instanceof Error ? error.stack : String(error);
  }
  if (finalizeFailure) throw finalizeFailure;
}

async function preflightScratchAndEvidence() {
  manifest.scratchLifecycle = [];
  const resolvedTemp = await realpath(tmpdir());
  const physicalScratch = await physicalPath(scratchRoot);
  const physicalEvidence = await physicalPath(evidence);
  const worktreeOutput = await run("git", ["worktree", "list", "--porcelain"], { cwd: candidate });
  const checkoutBoundaries = [];
  for (const line of worktreeOutput.stdout.split(/\r?\n/).filter(value => value.startsWith("worktree "))) {
    const path = line.slice("worktree ".length);
    checkoutBoundaries.push(await physicalPath(path));
  }
  assert(!isInside(resolvedTemp, physicalScratch), `scratch root must be outside the operating-system Temp tree: ${physicalScratch}`);
  assert(!isInside(resolvedTemp, physicalEvidence), `retained evidence must be outside the operating-system Temp tree: ${physicalEvidence}`);
  for (const boundary of checkoutBoundaries) {
    assert(!isInside(boundary, physicalScratch) && !isInside(physicalScratch, boundary), `scratch root must be physically separate from every DynaDocs checkout: ${boundary}`);
    assert(!isInside(boundary, physicalEvidence) && !isInside(physicalEvidence, boundary), `retained evidence must be physically separate from every DynaDocs checkout: ${boundary}`);
  }
  assert(!isInside(physicalScratch, physicalEvidence) && !isInside(physicalEvidence, physicalScratch), "scratch root and retained evidence must be physically separate");

  const suspicious = ["AGENTS.md", "CLAUDE.md", join(".agents", "skills"), join(".claude", "skills"), join(".opencode", "skills"), "opencode.json", "opencode.jsonc", ".opencode", ".git"];
  const ancestors = [];
  let current = physicalScratch;
  for (;;) {
    const entries = [];
    for (const name of suspicious) {
      const path = join(current, name);
      entries.push({ path, kind: await entryKind(path) });
    }
    ancestors.push({ path: current, kind: await entryKind(current), entries });
    const parent = dirname(current);
    if (parent === current) break;
    current = parent;
  }
  const contaminated = ancestors.flatMap(item => item.entries).filter(entry => entry.kind !== "absent");
  assert(contaminated.length === 0, `scratch-root ancestor contamination: ${JSON.stringify(contaminated)}`);

  let probeRoot = physicalScratch;
  while (!(await fileExists(probeRoot))) {
    const parent = dirname(probeRoot);
    assert(parent !== probeRoot, "could not find an existing scratch-root ancestor for Git preflight");
    probeRoot = parent;
  }
  const gitProbe = await capture("git", ["-C", probeRoot, "rev-parse", "--show-toplevel"]);
  assert(gitProbe.exitCode !== 0, `scratch root is inside an existing Git worktree: ${gitProbe.stdout.trim()}`);
  manifest.scratchPreflight = {
    requestedScratchRoot: options.scratchRoot,
    physicalScratchRoot: physicalScratch,
    requestedEvidence: options.evidence,
    physicalEvidence,
    tempBoundary: resolvedTemp,
    checkoutBoundaries,
    ancestors,
    gitProbe: { cwd: probeRoot, exitCode: gitProbe.exitCode, stdout: gitProbe.stdout.trim(), stderr: gitProbe.stderr.trim() },
    verdict: "PASS"
  };
  rejectedAncestorPaths = [...new Set([
    ...ancestors.flatMap(item => item.entries.map(entry => entry.path)),
    ...checkoutBoundaries,
    join(resolvedTemp, "AGENTS.md"),
    join(resolvedTemp, "CLAUDE.md")
  ])];
}

async function verifyMaterializedTree(sha, root) {
  const listing = await run("git", ["ls-tree", "-rz", "--full-tree", sha], { cwd: candidate });
  const sourceEntries = listing.stdout.split("\0").filter(Boolean).map(record => {
    const tab = record.indexOf("\t");
    const [mode, type, oid] = record.slice(0, tab).split(" ");
    return { mode, type, oid, path: record.slice(tab + 1) };
  });
  const materializedPaths = await filesystemLeaves(root);
  const sourcePaths = sourceEntries.map(entry => entry.path).sort(ordinal);
  assert(JSON.stringify(materializedPaths) === JSON.stringify(sourcePaths), "materialized archive paths did not exactly match git ls-tree");
  const verified = [];
  for (const entry of sourceEntries) {
    assert(entry.type === "blob" && ["100644", "100755", "120000"].includes(entry.mode), `unsupported source tree entry: ${JSON.stringify(entry)}`);
    const path = join(root, ...entry.path.split("/"));
    const info = await lstat(path);
    let actualType = "other";
    if (info.isSymbolicLink()) actualType = "link";
    else if (info.isFile()) actualType = "regular-file";
    const expectedType = entry.mode === "120000" ? "link" : "regular-file";
    assert(actualType === expectedType, `materialized entry type mismatch for ${entry.path}: ${actualType}`);
    const oid = await gitBlobOid(path, info);
    assert(oid === entry.oid, `materialized bytes differed from source blob for ${entry.path}: expected ${entry.oid}, observed ${oid}, size ${info.size}`);
    verified.push({ path: entry.path, sourceMode: entry.mode, entryType: actualType, blobOid: oid });
  }
  await writeArtifact("materialized-tree.json", JSON.stringify({ sourceSha: sha, sourceTreeSha: manifest.sourceTreeSha, entries: verified }, null, 2));
  pass("The materialized filesystem exactly matches the source candidate Git tree before setup");
}

async function filesystemLeaves(root, prefix = "") {
  const result = [];
  for (const entry of (await readdir(root, { withFileTypes: true })).sort((left, right) => ordinal(left.name, right.name))) {
    const relativePath = prefix ? `${prefix}/${entry.name}` : entry.name;
    const path = join(root, entry.name);
    const info = await lstat(path);
    if (info.isDirectory() && !info.isSymbolicLink()) result.push(...await filesystemLeaves(path, relativePath));
    else result.push(relativePath);
  }
  return result.sort(ordinal);
}

async function gitBlobOid(path, info) {
  info ??= await lstat(path);
  const bytes = info.isSymbolicLink() ? Buffer.from(await readlink(path), "utf8") : undefined;
  const size = bytes ? bytes.length : info.size;
  const hash = createHash("sha1");
  hash.update(Buffer.from(`blob ${size}\0`, "utf8"));
  if (bytes) hash.update(bytes);
  else await new Promise((resolveHash, reject) => {
    const input = createReadStream(path);
    input.on("data", chunk => hash.update(chunk));
    input.on("end", resolveHash);
    input.on("error", reject);
  });
  return hash.digest("hex");
}

async function loadExpectedSkills(root) {
  const skills = [];
  const categories = (await readdir(join(root, "skills"), { withFileTypes: true })).filter(value => value.isDirectory()).sort((left, right) => ordinal(left.name, right.name));
  for (const category of categories) {
    const entries = (await readdir(join(root, "skills", category.name), { withFileTypes: true })).filter(value => value.isDirectory()).sort((left, right) => ordinal(left.name, right.name));
    for (const entry of entries) {
      const path = join(root, "skills", category.name, entry.name, "SKILL.md");
      const body = await readFile(path, "utf8");
      const frontmatter = body.match(/^---\r?\n([\s\S]*?)\r?\n---/)?.[1];
      assert(frontmatter, `canonical skill frontmatter was missing: ${entry.name}`);
      const name = decodeYamlScalar(frontmatter.match(/^name:\s*(.+)$/m)?.[1]?.trim());
      const description = decodeYamlScalar(frontmatter.match(/^description:\s*(.+)$/m)?.[1]?.trim());
      assert(name === entry.name && description, `canonical skill metadata was invalid: ${entry.name}`);
      skills.push({ name, description, path, category: category.name, bodySha256: await sha256(path) });
    }
  }
  skills.sort((left, right) => ordinal(left.name, right.name));
  await writeArtifact("candidate-skill-inventory.json", JSON.stringify(skills, null, 2));
  return skills;
}

function skillPath(name) {
  const skill = expectedSkills.find(candidate => candidate.name === name);
  assert(skill, `canonical skill was not in the candidate inventory: ${name}`);
  return skill.path;
}

async function candidateFingerprint(root) {
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
      } else if (info.isFile()) entries.push({ path: relativePath, type: "regular-file", sha256: await sha256(path) });
      else entries.push({ path: relativePath, type: "other" });
    }
  }
  await visit(root);
  return objectSha256(entries);
}

async function runClaudeCanary() {
  const claudeConfig = join(hostsRoot, "claude-config");
  await mkdir(claudeConfig, { recursive: true });
  const env = { ...process.env, CLAUDE_CONFIG_DIR: claudeConfig };
  const claudeInstructionsPath = join(checkout, "CLAUDE.md");
  const claudeInstructions = await readFile(claudeInstructionsPath, "utf8");
  await writeArtifact("candidate-CLAUDE.md", claudeInstructions);
  const version = (await run("claude", ["--version"], { env })).stdout.trim();
  manifest.environment.claude = { version, configDir: claudeConfig, settingSources: ["project"], strictMcpConfig: true, chrome: false, instructions: { path: claudeInstructionsPath, sha256: await sha256(claudeInstructionsPath), bytes: (await stat(claudeInstructionsPath)).size } };
  const common = ["--output-format", "stream-json", "--verbose", "--no-session-persistence", "--setting-sources", "project", "--strict-mcp-config", "--no-chrome", "--permission-mode", "dontAsk", "--allowedTools", "Skill,Read"];
  await writeArtifact("claude-session-settings.json", JSON.stringify({ executable: "claude", cwd: checkout, promptPlacement: "immediately after --print", commonArgv: common }, null, 2));
  const implicitPrompt = "If the project skill teach appears in the model-visible skill inventory, invoke it. Otherwise reply exactly DYDO_TEACH_HIDDEN. Do not use slash-command syntax.";
  const implicit = await capture("claude", ["--print", implicitPrompt, ...common], { cwd: checkout, env });
  await writeArtifact("claude-implicit.ndjson", implicit.stdout);
  await writeArtifact("claude-implicit.stderr.txt", implicit.stderr);
  assert(implicit.exitCode === 0, `Claude implicit canary exited ${implicit.exitCode}: ${redact(implicit.stderr || implicit.stdout)}`);
  const explicit = await capture("claude", ["--print", `/teach ${PROMPT}`, ...common], { cwd: checkout, env });
  await writeArtifact("claude-explicit.ndjson", explicit.stdout);
  await writeArtifact("claude-explicit.stderr.txt", explicit.stderr);
  assert(explicit.exitCode === 0, `Claude explicit canary exited ${explicit.exitCode}: ${redact(explicit.stderr || explicit.stdout)}`);

  const implicitEvents = parseNdjson(implicit.stdout, "Claude implicit output");
  const explicitEvents = parseNdjson(explicit.stdout, "Claude explicit output");
  await assertClaudeInit(implicitEvents, "implicit");
  await assertClaudeInit(explicitEvents, "explicit");
  assertNoOuterState(implicitEvents, "Claude implicit native stream");
  assertNoOuterState(explicitEvents, "Claude explicit native stream");
  const implicitText = JSON.stringify(implicitEvents);
  const explicitText = JSON.stringify(explicitEvents);
  assert(implicitText.includes("DYDO_TEACH_HIDDEN"), "Claude implicit canary did not return the hidden marker");
  assert(!implicitText.includes("mission-format.md") && !implicitText.includes(EXPECTED_FACT), "Claude implicitly exposed teach");
  assert(explicitText.includes("teach") && explicitText.includes("mission-format.md"), "Claude explicit canary did not load teach");
  assert(explicitText.includes(EXPECTED_FACT), "Claude explicit canary did not read the resource fact");
  assert(finalText(explicitEvents) === EXPECTED_FACT, "Claude explicit final response was not exact");
  pass("Claude keeps teach explicit-only and resolves its linked resource through the installed base");
}

async function assertClaudeInit(events, label) {
  const init = events.find(event => event?.type === "system" && event?.subtype === "init");
  assert(init, `Claude ${label} stream omitted system/init`);
  assert(samePath(await realpath(init.cwd), await realpath(checkout)), `Claude ${label} init cwd was outside the candidate`);
  const skills = Array.isArray(init.skills) ? init.skills : [];
  const names = skills.map(skill => typeof skill === "string" ? skill : skill?.name).filter(Boolean).sort(ordinal);
  const expectedNames = expectedSkills.map(skill => skill.name).sort(ordinal);
  assert(JSON.stringify(names) === JSON.stringify(expectedNames), `Claude ${label} init skills were not exactly the 31 candidate skills`);
  const reportedPaths = [];
  walk(skills, value => { if (typeof value === "string" && isAbsolute(value)) reportedPaths.push(value); });
  for (const path of reportedPaths) {
    const actual = await realpath(path);
    const canonical = await realpath(join(checkout, "skills"));
    const projected = await realpath(join(checkout, ".claude", "skills"));
    assert(isInside(canonical, actual) || isInside(projected, actual), `Claude ${label} reported an outer skill path: ${path}`);
  }
  pass(`Claude ${label} system/init is bound to the candidate cwd and exact project skill inventory`);
}

async function runCodexCanary() {
  const resolvedCodex = await resolveCodexExecutable();
  const version = (await run(resolvedCodex, ["--version"])).stdout.trim();
  manifest.environment.codex = version;
  const yaml = await readFile(join(dirname(skillPath("teach")), "agents", "openai.yaml"), "utf8");
  assert(yaml.includes("allow_implicit_invocation: false"), "canonical teach lost its Codex explicit-only control");
  await recordCodexProjection();

  const isolation = join(hostsRoot, "codex-isolation");
  const home = join(isolation, "home");
  for (const name of ["home", "appdata", "localappdata", "temp"]) await mkdir(join(isolation, name), { recursive: true });
  const requestsPath = join(evidence, "codex-provider-requests.ndjson");
  const provider = await startProvider(requestsPath, "codex", checkout);
  const config = `model = "skill-canary"\nmodel_provider = "dyd91_loopback"\napproval_policy = "on-request"\nsandbox_mode = "read-only"\ndisable_response_storage = true\n\n[features]\nplugins = false\nremote_plugin = false\n\n[model_providers.dyd91_loopback]\nname = "DYD-91 loopback Responses mock"\nbase_url = "http://127.0.0.1:${provider.port}/v1"\nwire_api = "responses"\nrequires_openai_auth = false\nsupports_websockets = false\nrequest_max_retries = 0\nstream_max_retries = 0\n`;
  const configPath = join(home, "config.toml");
  await writeFile(configPath, config, "utf8");
  await writeArtifact("codex-config.toml", config);
  const env = codexEnv(isolation, home, resolvedCodex, provider.port);
  manifest.environment.codexIsolation = { executable: resolvedCodex, argv: [resolvedCodex, "app-server", "--stdio"], env: sanitizeCodexEnv(env, isolation, resolvedCodex) };

  const rpc = startJsonRpc(resolvedCodex, ["app-server", "--stdio"], { cwd: checkout, env });
  let systemBaseline;
  try {
    await rpc.request({ method: "initialize", id: 1, params: { clientInfo: { name: "dyd91-skill-canary", title: "DYD-91 skill canary", version: "1.0.0" }, capabilities: {} } });
    rpc.send({ method: "initialized", params: {} });
    systemBaseline = await codexSystemSkillInventory(home);
    await writeArtifact("codex-system-skills-before.json", JSON.stringify(systemBaseline, null, 2));
    const inventoryResponse = await rpc.request({ method: "skills/list", id: 2, params: { cwds: [checkout], forceReload: true } });
    await writeArtifact("codex-skills-list.json", JSON.stringify(inventoryResponse.result, null, 2));
    const inventoryGroup = inventoryResponse.result?.data?.find(group => samePath(group.cwd, checkout));
    assert(inventoryGroup && Array.isArray(inventoryGroup.skills) && (inventoryGroup.errors ?? []).length === 0, "Codex skills/list did not return one error-free candidate inventory");
    const repoRecords = inventoryGroup.skills.filter(record => record.scope === "repo" && record.enabled !== false);
    assertSkillRecords(repoRecords, expectedSkills, "Codex repository inventory");
    for (const record of repoRecords) {
      assert(samePath(await realpath(record.path), await realpath(skillPath(record.name))), `Codex repo skill path was not canonical: ${record.name}`);
    }
    const systemRecords = inventoryGroup.skills.filter(record => record.scope === "system" && record.enabled !== false);
    const allowedSystemNames = ["imagegen", "openai-docs", "plugin-creator", "review-agent", "skill-creator", "skill-installer"].sort(ordinal);
    assert(JSON.stringify(systemRecords.map(record => record.name).sort(ordinal)) === JSON.stringify(allowedSystemNames), "Codex system inventory differed from the six allowed isolated built-ins");
    for (const record of systemRecords) assert(isInside(await realpath(join(home, "skills", ".system")), await realpath(record.path)), `Codex system skill escaped isolated CODEX_HOME: ${record.name}`);
    const records = repoRecords.filter(record => record.name === "teach");
    assert(records.length === 1, `Codex inventory returned ${records.length} enabled repository teach skills`);
    const record = records[0];
    const selectedPath = record.path;
    await writeArtifact("codex-explicit.json", JSON.stringify({ selectedSkill: record }, null, 2));
    assert(typeof selectedPath === "string" && samePath(await realpath(selectedPath), await realpath(skillPath("teach"))), `Codex inventory did not supply teach's canonical SKILL.md path: ${selectedPath}`);

    const implicitThread = await rpc.request({ method: "thread/start", id: 3, params: { cwd: checkout, model: "skill-canary", modelProvider: "dyd91_loopback", approvalPolicy: "on-request", sandbox: "read-only", ephemeral: true } });
    const implicitThreadId = implicitThread.result?.thread?.id;
    assert(typeof implicitThreadId === "string", "Codex implicit thread/start did not return result.thread.id");
    const implicitStart = rpc.mark();
    rpc.send({ method: "turn/start", id: 4, params: { threadId: implicitThreadId, cwd: checkout, sandboxPolicy: { type: "readOnly", networkAccess: false }, input: [{ type: "text", text: CODEX_IMPLICIT_PROMPT }] } });
    await rpc.wait(message => message.id === 4, implicitStart);
    await rpc.wait(message => message.method === "turn/completed", implicitStart);
    const implicitIncoming = rpc.incomingSince(implicitStart);
    assert(!implicitIncoming.some(isCodexToolItem), "Codex implicit control emitted a tool item");
    assert(implicitIncoming.filter(isServerRequest).length === 0, "Codex implicit control requested approval or another server-side action");
    assert(exactCodexFinal(implicitIncoming) === "DYDO_TEACH_HIDDEN", "Codex implicit control final was not exactly DYDO_TEACH_HIDDEN");
    await writeArtifact("codex-implicit.ndjson", implicitIncoming.map(message => JSON.stringify(message)).join("\n") + "\n");
    const implicitProviderEntries = parseNdjson(await readFile(requestsPath, "utf8"), "Codex provider requests after implicit turn");
    const implicitPost = implicitProviderEntries.find(entry => entry.method === "POST");
    assert(implicitPost, "Codex implicit turn did not reach the loopback provider");
    const candidateAgents = await readFile(join(checkout, "AGENTS.md"), "utf8");
    assert(countAcross(collectStrings(implicitPost.payload), candidateAgents) === 1, "Codex implicit request did not contain the exact candidate AGENTS.md bytes once");
    assertNoOuterState(implicitPost.payload, "Codex implicit provider request");

    const explicitThread = await rpc.request({ method: "thread/start", id: 5, params: { cwd: checkout, model: "skill-canary", modelProvider: "dyd91_loopback", approvalPolicy: "on-request", sandbox: "read-only", ephemeral: true } });
    const explicitThreadId = explicitThread.result?.thread?.id;
    assert(typeof explicitThreadId === "string", "Codex explicit thread/start did not return result.thread.id");
    const teachBody = await readFile(selectedPath, "utf8");
    const resourceLink = teachBody.match(/\[mission-format\]\(([^)]+)\)/)?.[1];
    assert(resourceLink, "selected teach body did not expose the mission-format link");
    const derivedResourcePath = resolve(dirname(selectedPath), resourceLink);
    assert(samePath(await realpath(derivedResourcePath), await realpath(join(dirname(skillPath("teach")), "resources", "mission-format.md"))), "Codex approval resource did not derive from the selected canonical skill");
    const readArgv = ["Get-Content", "-Raw", "-LiteralPath", derivedResourcePath];
    const readCommand = `Get-Content -Raw -LiteralPath '${derivedResourcePath}'`;
    const powershell = join(process.env.SystemRoot ?? "C:\\Windows", "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
    const renderedCommand = `"${powershell.replaceAll("\\", "\\\\")}" -NoProfile -Command "${readCommand.replaceAll("\\", "\\\\")}"`;
    const explicitStart = rpc.mark();
    rpc.send({ method: "turn/start", id: 6, params: { threadId: explicitThreadId, cwd: checkout, sandboxPolicy: { type: "readOnly", networkAccess: false }, input: [{ type: "skill", name: "teach", path: selectedPath }, { type: "text", text: PROMPT }] } });
    const explicitTurnResponse = await rpc.wait(message => message.id === 6, explicitStart);
    assert(!explicitTurnResponse.error, `Codex explicit turn/start failed: ${JSON.stringify(explicitTurnResponse.error)}`);
    const explicitTurnId = explicitTurnResponse.result?.turn?.id;
    assert(typeof explicitTurnId === "string", "Codex explicit turn/start did not return result.turn.id");
    const commandStarted = await rpc.wait(message => message.method === "item/started" && message.params?.item?.type === "commandExecution", explicitStart);
    const commandItemId = commandStarted.params?.item?.id;
    assert(typeof commandItemId === "string", "Codex command item did not have an id");
    assert(commandStarted.params?.threadId === explicitThreadId && commandStarted.params?.turnId === explicitTurnId, "Codex command item did not belong to the active explicit turn");
    assert(commandStarted.params?.item?.command === renderedCommand && samePath(commandStarted.params?.item?.cwd, checkout), "Codex command item did not exactly match the derived read-only PowerShell command");
    const approval = await rpc.wait(message => message.method === "item/commandExecution/requestApproval", explicitStart);
    const response = { id: approval.id, result: { decision: "accept" } };
    try {
      validateCodexApproval(approval, { explicitThreadId, explicitTurnId, commandItemId, renderedCommand, readArgv });
      rpc.send(response);
    } catch (error) {
      rpc.send({ id: approval.id, result: { decision: "cancel" } });
      throw error;
    }
    await writeArtifact("codex-approval.json", JSON.stringify({ request: approval, normalizedReadArgv: readArgv, renderedCommand, response }, null, 2));
    await rpc.wait(message => message.method === "turn/completed", explicitStart);
    const incoming = rpc.incomingSince(explicitStart);
    const serverRequests = incoming.filter(isServerRequest);
    assert(serverRequests.length === 1 && serverRequests[0] === approval, `Codex emitted ${serverRequests.length} server requests instead of the one approved read`);
    const started = incoming.findIndex(message => message.method === "turn/started");
    const command = incoming.findIndex(message => message.method === "item/completed" && JSON.stringify(message).includes("Get-Content -Raw -LiteralPath") && JSON.stringify(message).includes("mission-format.md") && JSON.stringify(message).includes('"exitCode":0'));
    const message = incoming.findIndex(item => item.method === "item/completed" && findString(item, value => value === EXPECTED_FACT));
    const completed = incoming.findIndex(item => item.method === "turn/completed" && !JSON.stringify(item).includes('"status":"failed"'));
    assert(started >= 0 && command > started && message > command && completed > message, "Codex transcript lacked the ordered successful turn/read/agent-message/completion sequence");

    const providerEntries = parseNdjson(await readFile(requestsPath, "utf8"), "Codex provider requests");
    const providerPosts = providerEntries.filter(entry => entry.method === "POST");
    assert(providerPosts.length === 3, `Codex made ${providerPosts.length} provider POSTs instead of the expected three`);
    assert(!providerEntries.some(entry => entry.unexpected), "Codex made an unexpected provider or outbound request");
    const proxyAttempts = providerEntries.filter(entry => entry.method === "CONNECT");
    await writeArtifact("codex-deny-proxy.ndjson", proxyAttempts.map(entry => JSON.stringify(entry)).join("\n") + (proxyAttempts.length ? "\n" : ""));
    assert(proxyAttempts.length === 0, "Codex deny-proxy transcript was not empty");
    assertNoOuterState(providerPosts[1].payload, "Codex explicit provider request");
    manifest.artifacts["codex-provider-requests.ndjson"] = { sha256: await sha256(requestsPath), bytes: (await stat(requestsPath)).size };
    pass("Codex app-server inventory, implicit omission, one-shot approved read, structured explicit resource proof, zero proxy-observed external attempts, and exactly three loopback POSTs passed");
  } finally {
    await writeArtifact("codex-live.ndjson", rpc.transcript.map(entry => JSON.stringify(entry)).join("\n") + "\n");
    await writeArtifact("codex-app-server.stderr.txt", rpc.stderr());
    if (await fileExists(requestsPath)) {
      manifest.artifacts["codex-provider-requests.ndjson"] = { sha256: await sha256(requestsPath), bytes: (await stat(requestsPath)).size };
    }
    rpc.stop();
    await rpc.done.catch(() => {});
    if (systemBaseline) {
      const systemAfter = await codexSystemSkillInventory(home);
      await writeArtifact("codex-system-skills-after.json", JSON.stringify(systemAfter, null, 2));
      assert(JSON.stringify(systemAfter) === JSON.stringify(systemBaseline), "Codex isolated system skill inventory changed during the app-server run");
    }
    provider.child.kill("SIGTERM");
    await provider.done.catch(() => {});
    manifest.providerLifecycle.push({ mode: "codex", action: "shutdown", endpoint: `http://127.0.0.1:${provider.port}`, result: "stopped" });
  }
}

async function recordCodexProjection() {
  const projectedDirectory = join(checkout, ".agents", "skills", "teach");
  const projectedBody = join(projectedDirectory, "SKILL.md");
  const projectedResource = join(projectedDirectory, "resources", "mission-format.md");
  const canonicalBody = skillPath("teach");
  const canonicalResource = join(dirname(skillPath("teach")), "resources", "mission-format.md");
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
  assert(isAbsolute(options.opencodeArchive) && isAbsolute(options.ripgrep), "OpenCode archive and Ripgrep paths must be absolute");
  const archive = resolve(options.opencodeArchive);
  const suppliedRg = resolve(options.ripgrep);
  assert(await sha256(archive) === OPENCODE_ARCHIVE_SHA256, "OpenCode archive hash mismatch");
  assert(await sha256(suppliedRg) === RIPGREP_SHA256, "supplied Ripgrep hash mismatch");

  const originalRoots = nativeOpenCodeRoots();
  const before = await fingerprintRoots(originalRoots);
  const isolation = join(hostsRoot, "opencode-isolation");
  const physicalPaths = Object.fromEntries(["home", "config", "data", "cache", "state", "temp", "appdata", "localappdata", "custom-config", "bin"].map(name => [name, join(isolation, name)]));
  for (const value of Object.values(physicalPaths)) await mkdir(value, { recursive: true });
  await run("tar", ["-xf", archive, "-C", physicalPaths.bin]);
  const physicalExe = await findFile(physicalPaths.bin, "opencode.exe");
  assert(await sha256(physicalExe) === OPENCODE_EXE_SHA256, "extracted OpenCode executable hash mismatch");
  await cp(suppliedRg, join(physicalPaths.bin, "rg.exe"));

  const drive = await unusedDrive();
  await run("subst", [`${drive}:`, isolation]);
  manifest.scratchLifecycle.push({ action: "subst-create", drive: `${drive}:`, target: isolation, result: "created" });
  const substQuery = await run("subst", []);
  manifest.scratchLifecycle.push({ action: "subst-query", drive: `${drive}:`, output: substQuery.stdout });
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
    const config = JSON.stringify({
      $schema: "https://opencode.ai/config.json",
      share: "disabled",
      autoupdate: false,
      provider: { openai: { options: { baseURL: `http://127.0.0.1:${provider.port}/v1`, apiKey: "dyd91-loopback" }, models: { "skill-canary": { name: "skill-canary" } } } }
    }, null, 2);
    await writeFile(configPath, config);
    await writeArtifact("opencode-config.json", config);
    const env = portableEnv(paths, configPath, provider.port);
    const originalValues = Object.values(process.env).filter(value => typeof value === "string" && /\\Users\\/i.test(value));
    assert(!Object.values(env).some(value => originalValues.includes(value)), "isolated child environment leaked an original user path value");
    manifest.environment.opencodeIsolation = {
      paths: Object.fromEntries(Object.entries(paths).map(([key, value]) => [key, value])),
      argv: [exe, "--pure", "run", "--dir", ".", "--title", "DYD-91-skill-canary", "--model", "openai/skill-canary", "--format", "json", PROMPT],
      environment: sanitizePortableEnv(env, isolation, drive)
    };

    const debugPaths = await run(exe, ["--pure", "debug", "paths"], { cwd: checkout, env });
    const debugPathValues = extractAbsolutePaths(debugPaths.stdout);
    const resolvedDebugPaths = await Promise.all(debugPathValues.map(value => realpath(value)));
    assert(resolvedDebugPaths.length > 0 && resolvedDebugPaths.every(value => isInside(isolation, value)), `OpenCode reported a mutable path outside isolation: ${JSON.stringify(debugPathValues)}`);
    const inventory = await run(exe, ["--pure", "debug", "skill"], { cwd: checkout, env });
    await writeArtifact("opencode-inventory.json", inventory.stdout);
    const parsedInventory = JSON.parse(inventory.stdout);
    assert(Array.isArray(parsedInventory), "OpenCode inventory was not an array");
    const builtIns = parsedInventory.filter(skill => skill.location === "<built-in>");
    assert(builtIns.length === 1 && builtIns[0].name === "customize-opencode", "OpenCode inventory did not contain exactly the allowed built-in skill");
    const projectSkills = parsedInventory.filter(skill => skill.location !== "<built-in>");
    assertSkillRecords(projectSkills, expectedSkills, "OpenCode native inventory", "location");
    for (const skill of projectSkills) {
      const actual = await realpath(skill.location);
      const canonical = await realpath(skillPath(skill.name));
      assert(samePath(actual, canonical), `OpenCode inventory location for ${skill.name} did not real-resolve canonical`);
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
    assert(samePath(readPath, await realpath(join(dirname(skillPath("teach")), "resources", "mission-format.md"))), "OpenCode read did not resolve to the canonical resource");
    assert(events[readIndex].part.state.output.includes(EXPECTED_FACT), "OpenCode native read result omitted the resource fact");
    assert(events.filter(event => event.type === "text").map(event => event.part?.text).join("") === EXPECTED_FACT, "OpenCode live final response was not exact");
    const requests = parseNdjson(await readFile(requestsPath, "utf8"), "OpenCode provider requests");
    const providerPosts = requests.filter(entry => entry.method === "POST");
    const proxyAttempts = requests.filter(entry => entry.method === "CONNECT");
    await writeArtifact("opencode-deny-proxy.ndjson", proxyAttempts.map(entry => JSON.stringify(entry)).join("\n") + (proxyAttempts.length ? "\n" : ""));
    assert(providerPosts.length === 3, `OpenCode made ${providerPosts.length} loopback provider POSTs instead of the expected three`);
    assert(proxyAttempts.length === 0, `OpenCode made ${proxyAttempts.length} proxy-observed external attempts`);
    assert(!requests.some(entry => entry.unexpected), "OpenCode provider/proxy transcript contained an unexpected request");
    await assertOpenCodeFirstRequest(parsedInventory, providerPosts[0].payload);
    manifest.artifacts["opencode-provider-requests.ndjson"] = { sha256: await sha256(requestsPath), bytes: (await stat(requestsPath)).size };
    pass("Pinned OpenCode discovered the exact candidate skill set, completed the deterministic loopback skill/read proof, made zero proxy-observed external attempts, and made exactly three loopback POSTs");
  } finally {
    if (provider) {
      provider.child.kill("SIGTERM");
      await provider.done.catch(() => {});
      manifest.providerLifecycle.push({ mode: "opencode", action: "shutdown", endpoint: `http://127.0.0.1:${provider.port}`, result: "stopped" });
    }
    await run("subst", [`${drive}:`, "/D"]);
    manifest.scratchLifecycle.push({ action: "subst-delete", drive: `${drive}:`, result: "removed" });
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
  const port = parseJsonLines(stdout)[0].port;
  manifest.providerLifecycle ??= [];
  manifest.providerLifecycle.push({ mode, action: "start", endpoint: `http://127.0.0.1:${port}`, requestsPath, result: "listening" });
  return { child, done, port };
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

function assertSkillRecords(actual, expected, label, pathProperty = "path") {
  assert(actual.length === expected.length, `${label} returned ${actual.length} project skills instead of ${expected.length}`);
  const actualByName = new Map(actual.map(record => [record.name, record]));
  assert(actualByName.size === actual.length, `${label} contained duplicate names`);
  for (const skill of expected) {
    const record = actualByName.get(skill.name);
    assert(record, `${label} omitted ${skill.name}`);
    assert(record.description === skill.description, `${label} changed ${skill.name}'s description`);
    assert(typeof record[pathProperty] === "string", `${label} omitted ${skill.name}'s location`);
  }
  assert(actual.every(record => expected.some(skill => skill.name === record.name)), `${label} contained an unexpected project skill`);
}

async function codexSystemSkillInventory(home) {
  const root = join(home, "skills", ".system");
  const allowed = ["imagegen", "openai-docs", "plugin-creator", "review-agent", "skill-creator", "skill-installer"].sort(ordinal);
  const names = (await readdir(root, { withFileTypes: true })).filter(entry => entry.isDirectory()).map(entry => entry.name).sort(ordinal);
  assert(JSON.stringify(names) === JSON.stringify(allowed), `isolated Codex system skills were not exactly ${allowed.join(", ")}`);
  const entries = [];
  for (const name of names) {
    for (const relativePath of await filesystemLeaves(join(root, name), name)) {
      const path = join(root, ...relativePath.split("/"));
      const info = await lstat(path);
      entries.push(info.isSymbolicLink()
        ? { path: relativePath, type: "link", target: await readlink(path) }
        : { path: relativePath, type: "regular-file", sha256: await sha256(path) });
    }
  }
  return { root, names, entries };
}

function validateCodexApproval(message, expected) {
  const params = message.params ?? {};
  assert(params.kind === "command", "Codex approval kind was not command");
  assert(params.threadId === expected.explicitThreadId, "Codex approval thread did not match the explicit thread");
  assert(params.turnId === expected.explicitTurnId, "Codex approval turn did not match the explicit turn");
  assert(params.itemId === expected.commandItemId, "Codex approval item did not match the one command item");
  assert(params.approvalId == null, "Codex approval supplied an unexpected persistent approval id");
  assert(samePath(params.cwd, checkout), "Codex approval cwd was not the candidate root");
  assert(params.command === expected.renderedCommand, "Codex approval command was not the exact derived PowerShell read");
  assert(JSON.stringify(params.proposedExecpolicyAmendment) === JSON.stringify(expected.readArgv), "Codex proposed exec-policy amendment was not the exact derived Get-Content argv");
  assert(Array.isArray(params.availableDecisions) && params.availableDecisions.some(decision => decision === "accept"), "Codex approval did not offer the one-shot accept decision");
  for (const name of ["networkApprovalContext", "proposedNetworkPolicyAmendments", "additionalPermissions", "additionalFilesystemPermissions", "additionalNetworkPermissions"])
    assert(emptyish(params[name]), `Codex approval requested extra permission through ${name}`);
}

function isServerRequest(message) {
  return typeof message?.method === "string" && message?.id != null;
}

async function assertOpenCodeFirstRequest(nativeInventory, payload) {
  const prompt = findString(payload, value => value.includes("<available_skills>") && value.includes("Instructions from:"));
  assert(prompt, "OpenCode first provider request omitted the native instruction and skill inventory prompt");
  const agentsPath = join(checkout, "AGENTS.md");
  const agents = await readFile(agentsPath, "utf8");
  assert(prompt.includes(`Instructions from: ${agentsPath}`), "OpenCode first provider request did not name the candidate AGENTS.md path");
  assert(count(prompt, agents) === 1, "OpenCode first provider request did not contain the exact candidate AGENTS.md bytes once");
  const block = prompt.match(/<available_skills>([\s\S]*?)<\/available_skills>/)?.[1];
  assert(block, "OpenCode first provider request omitted the available_skills block");
  const reported = [...block.matchAll(/<skill>\s*<name>([\s\S]*?)<\/name>\s*<description>([\s\S]*?)<\/description>\s*<location>([\s\S]*?)<\/location>\s*<\/skill>/g)]
    .map(match => ({ name: decodeXml(match[1].trim()), description: decodeXml(match[2].trim()), location: decodeXml(match[3].trim()) }))
    .sort((left, right) => ordinal(left.name, right.name));
  const builtIns = reported.filter(skill => skill.location === "<built-in>");
  assert(builtIns.length === 1 && builtIns[0].name === "customize-opencode", "OpenCode first provider request did not contain exactly the allowed built-in skill");
  const projectSkills = reported.filter(skill => skill.location !== "<built-in>");
  assertSkillRecords(projectSkills, expectedSkills, "OpenCode first provider request skill inventory", "location");
  for (const skill of projectSkills) {
    assert(samePath(await realpath(skill.location), await realpath(skillPath(skill.name))), `OpenCode first provider location for ${skill.name} did not real-resolve canonical`);
  }
  assert(nativeInventory.length === reported.length, "OpenCode first provider request changed the native inventory cardinality");
  assertNoOuterState(payload, "OpenCode first provider request");
  pass("OpenCode first provider request contains only the candidate instructions, exact candidate skill inventory, and one allowed built-in");
}

function assertNoOuterState(value, label) {
  const strings = collectStrings(value);
  for (const path of rejectedAncestorPaths) {
    const needle = path.toLowerCase();
    assert(!strings.some(text => text.toLowerCase().includes(needle)), `${label} exposed rejected outer path ${path}`);
  }
  const staleMarker = "Shared agent methods are authored in dydo and compiled into platform-native skills and agents.";
  assert(!strings.some(text => text.includes(staleMarker)), `${label} exposed the known stale outer instruction marker`);
}

function collectStrings(value, result = []) {
  if (typeof value === "string") result.push(value);
  else if (Array.isArray(value)) value.forEach(item => collectStrings(item, result));
  else if (value && typeof value === "object") Object.values(value).forEach(item => collectStrings(item, result));
  return result;
}

function countAcross(strings, needle) {
  return strings.reduce((total, value) => total + count(value, needle), 0);
}

function emptyish(value) {
  return value == null || (Array.isArray(value) && value.length === 0) || (typeof value === "object" && !Array.isArray(value) && Object.keys(value).length === 0);
}

function decodeXml(value) {
  return value.replaceAll("&lt;", "<").replaceAll("&gt;", ">").replaceAll("&quot;", '"').replaceAll("&apos;", "'").replaceAll("&amp;", "&");
}

function decodeYamlScalar(value) {
  if (typeof value !== "string") return value;
  if (value.startsWith('"') && value.endsWith('"')) return JSON.parse(value);
  if (value.startsWith("'") && value.endsWith("'")) return value.slice(1, -1).replaceAll("''", "'");
  return value;
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
    OPENCODE_PURE: "1",
    OPENCODE_DISABLE_DEFAULT_PLUGINS: "1",
    OPENCODE_DISABLE_LSP_DOWNLOAD: "1",
    OPENCODE_DISABLE_MODELS_FETCH: "1",
    OPENCODE_DISABLE_PRUNE: "1",
    OPENCODE_DISABLE_SHARE: "1",
    npm_config_offline: "true",
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

function sanitizePortableEnv(env, isolation, drive) {
  return Object.fromEntries(Object.entries(env).map(([key, value]) => [key, value
    .replaceAll(isolation, "<opencode-isolation>")
    .replaceAll(`${drive}:\\`, "<opencode-drive>\\")
    .replace(/http:\/\/127\.0\.0\.1:\d+/g, "<loopback>")]));
}

async function archiveCommit(sha, destination) {
  const attributesRoot = join(runRoot, "archive-attributes");
  await mkdir(attributesRoot, { recursive: true });
  await writeFile(join(attributesRoot, ".gitattributes"), "* -text -filter -working-tree-encoding -export-ignore -export-subst\n** -text -filter -working-tree-encoding -export-ignore -export-subst\n", "utf8");
  const gitDirectory = (await run("git", ["rev-parse", "--absolute-git-dir"], { cwd: candidate })).stdout.trim();
  const gitArgs = [`--git-dir=${gitDirectory}`, `--work-tree=${attributesRoot}`, "-c", "core.autocrlf=false", "archive", "--worktree-attributes", "--format=tar", sha];
  const git = spawn("git", gitArgs, { cwd: attributesRoot, stdio: ["ignore", "pipe", "pipe"], windowsHide: true });
  const tar = spawn("tar", ["-xf", "-", "-C", destination], { stdio: ["pipe", "ignore", "pipe"], windowsHide: true });
  git.stdout.pipe(tar.stdin);
  const [gitCode, tarCode] = await Promise.all([exitCode(git), exitCode(tar)]);
  manifest.commands.push({ command: "git", args: gitArgs, cwd: attributesRoot, exitCode: gitCode });
  manifest.commands.push({ command: "tar", args: ["-xf", "-", "-C", destination], exitCode: tarCode });
  assert(gitCode === 0 && tarCode === 0, `could not materialize exact candidate ${sha}`);
}

async function run(command, args, { cwd, env } = {}) {
  const result = await capture(command, args, { cwd, env });
  assert(result.exitCode === 0, `${command} ${args.join(" ")} exited ${result.exitCode}: ${redact(result.stderr)}`);
  return result;
}

async function capture(command, args, { cwd, env } = {}) {
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
  return { stdout, stderr, exitCode: code };
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
function redact(value) { return value.replaceAll(candidate, "<candidate>").replaceAll(evidence, "<evidence>").replaceAll(scratchRoot, "<scratch-root>"); }
function count(value, needle) { return value.split(needle).length - 1; }
function ordinal(left, right) { if (left < right) return -1; if (left > right) return 1; return 0; }
function objectSha256(value) { return createHash("sha256").update(JSON.stringify(value)).digest("hex"); }
function parseJsonLines(value) { return value.split(/\r?\n/).filter(Boolean).flatMap(line => { try { return [JSON.parse(line)]; } catch { return []; } }); }
function parseNdjson(value, label) { const lines = value.split(/\r?\n/).filter(Boolean); const parsed = lines.map(line => JSON.parse(line)); assert(parsed.length > 0, `${label} was empty`); return parsed; }
function findString(value, predicate) { if (typeof value === "string") return predicate(value) ? value : undefined; if (Array.isArray(value)) { for (const item of value) { const found = findString(item, predicate); if (found) return found; } } else if (value && typeof value === "object") { for (const item of Object.values(value)) { const found = findString(item, predicate); if (found) return found; } } }
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
async function entryKind(path) {
  try {
    const info = await lstat(path);
    if (info.isSymbolicLink()) return "link";
    if (info.isDirectory()) return "directory";
    if (info.isFile()) return "regular-file";
    return "other";
  } catch (error) {
    if (error?.code === "ENOENT") return "absent";
    throw error;
  }
}
async function physicalPath(path) {
  const missing = [];
  let current = resolve(path);
  for (;;) {
    try {
      return join(await realpath(current), ...missing.reverse());
    } catch (error) {
      if (error?.code !== "ENOENT") throw error;
      const parent = dirname(current);
      assert(parent !== current, `could not resolve physical path for ${path}`);
      missing.push(basename(current));
      current = parent;
    }
  }
}

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
