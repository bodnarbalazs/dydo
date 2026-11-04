import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";

const DIGEST = "936a262061c914694dfd669a543be24573c45d5aa0ff20a8b96b23d01e050e88";
export type CheckerData = { source: string; jar: string };

const digest = (file: string) => createHash("sha256").update(fs.readFileSync(file)).digest("hex");

function rejectCompanion(jar: string): void {
  const companion = path.join(path.dirname(jar), "CommunityModules.jar");
  try { fs.lstatSync(companion); }
  catch (error) {
    if ((error as NodeJS.ErrnoException).code === "ENOENT") return;
    throw error;
  }
  throw new Error(`Unsupported unpinned dependency CommunityModules.jar beside checker input: ${jar}`);
}

export function pinnedTlc(jar: string): string {
  const full = path.resolve(jar);
  rejectCompanion(full);
  let actual: string;
  try {
    actual = fs.realpathSync.native(full);
    if (!fs.statSync(actual).isFile() || digest(actual) !== DIGEST) throw new Error("Digest mismatch");
  } catch (cause) {
    throw new Error(`Statechart requires the pinned TLC1.7.4 jar: ${full}`, { cause });
  }
  rejectCompanion(actual);
  return actual;
}

function plainDirectory(directory: string): void {
  if (!path.isAbsolute(directory)) throw new Error(`Owned checker root must be absolute: ${directory}`);
  for (let current = directory;; current = path.dirname(current)) {
    const entry = fs.lstatSync(current);
    if (entry.isSymbolicLink() || !entry.isDirectory())
      throw new Error(`Owned checker root has a linked or non-directory ancestor: ${current}`);
    if (path.dirname(current) === current) return;
  }
}

export function prepareChecker(source: string, owned: string): CheckerData {
  const input = pinnedTlc(source);
  plainDirectory(owned);
  const directory = path.join(owned, "checker");
  fs.mkdirSync(directory, { mode: 0o700 });
  const jar = path.join(directory, "tla2tools.jar");
  fs.copyFileSync(input, jar, fs.constants.COPYFILE_EXCL);
  const checker = { source: path.resolve(source), jar };
  checkerJar(checker);
  return checker;
}

export function checkerJar(checker: CheckerData): string {
  pinnedTlc(checker.source);
  const directory = path.dirname(checker.jar);
  plainDirectory(directory);
  const members = fs.readdirSync(directory);
  if (members.length !== 1 || members[0] !== "tla2tools.jar" || path.basename(checker.jar) !== "tla2tools.jar")
    throw new Error(`Isolated checker directory must contain only tla2tools.jar: ${directory}`);
  const entry = fs.lstatSync(checker.jar);
  if (entry.isSymbolicLink() || !entry.isFile() || digest(checker.jar) !== DIGEST)
    throw new Error(`Isolated checker bytes changed: ${checker.jar}`);
  return checker.jar;
}

export function tlcInvocation(checker: CheckerData): string[] {
  const jar = checkerJar(checker);
  const root = path.join(import.meta.dirname, "java");
  plainDirectory(root);
  const helper = path.join(root, "statechart-tlc-bootstrap.jar");
  const lockPath = path.join(root, "build-lock.json");
  const lockEntry = fs.lstatSync(lockPath);
  if (lockEntry.isSymbolicLink() || !lockEntry.isFile())
    throw new Error("Linked or non-file statechart bootstrap build lock; no raw TLC fallback.");
  const lock = JSON.parse(fs.readFileSync(lockPath, "utf8"));
  if (lock.schema !== 1 || lock.artifact?.state !== "sealed-build" || !/^[a-f0-9]{64}$/.test(lock.artifact.sha256 ?? ""))
    throw new Error("Statechart TLC bootstrap has no sealed build identity; no raw TLC fallback.");
  const entry = fs.lstatSync(helper);
  if (entry.isSymbolicLink() || !entry.isFile() || digest(helper) !== lock.artifact.sha256)
    throw new Error("Statechart TLC bootstrap bytes differ from the sealed build; no raw TLC fallback.");
  return ["-cp", [helper, jar].join(path.delimiter), "dydo.statechart.LoopbackTlc"];
}
