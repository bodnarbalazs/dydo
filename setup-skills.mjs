#!/usr/bin/env node

import { lstat, mkdir, readdir, readlink, realpath, rm, symlink } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

function parseRoot(argv) {
  if (argv.length === 0) return path.dirname(fileURLToPath(import.meta.url));
  if (argv.length !== 2 || argv[0] !== "--root" || !argv[1]) throw new Error("usage: setup-skills.mjs [--root <existing-directory>]");
  return path.resolve(argv[1]);
}

const root = parseRoot(process.argv.slice(2));
const canonicalRoot = path.join(root, "skills");
const hostRoots = [path.join(root, ".claude", "skills"), path.join(root, ".agents", "skills")];

async function existing(pathname) {
  try {
    return await lstat(pathname);
  } catch (error) {
    if (error.code === "ENOENT") return null;
    throw error;
  }
}

async function sameDirectory(left, right) {
  try {
    return path.normalize(await realpath(left)) === path.normalize(await realpath(right));
  } catch (error) {
    if (error.code === "ENOENT") return false;
    throw error;
  }
}

// Resolves a symlink/junction's raw, unvalidated target: absolute targets (what Windows junctions
// store) are used as-is, relative ones resolve against the link's own directory. Unlike realpath,
// this never touches the filesystem past the link itself, so it works for a dangling target too.
async function rawLinkTarget(linkPath) {
  const raw = await readlink(linkPath);
  return path.isAbsolute(raw) ? raw : path.resolve(path.dirname(linkPath), raw);
}

// A link is "ours" when its raw target falls under this repository's canonical skills/ tree,
// whether or not that target still exists. That is exactly what a link from an earlier layout
// (flat, or an old category) looks like once its skill moved: dangling, but still ours to fix.
function underCanonicalRoot(candidate) {
  const relative = path.relative(canonicalRoot, candidate);
  return relative === "" || (!relative.startsWith("..") && !path.isAbsolute(relative));
}

// The walk rule: a folder holding SKILL.md is a skill (its own subfolders are never walked); any
// other folder under skills/ is a category to walk into, at any depth. A category with no
// subfolder at all is an error, and so is one skill name reached through two categories.
async function walkCategory(categoryRoot, skills, seen) {
  const entries = (await readdir(categoryRoot, { withFileTypes: true }))
    .filter((entry) => entry.isDirectory())
    .sort((left, right) => left.name.localeCompare(right.name));
  if (entries.length === 0) throw new Error(`No canonical skills found in ${categoryRoot}`);

  const category = path.relative(canonicalRoot, categoryRoot).split(path.sep).join("/");
  for (const entry of entries) {
    const source = path.join(categoryRoot, entry.name);
    if (!(await existing(path.join(source, "SKILL.md")))?.isFile()) {
      await walkCategory(source, skills, seen);
      continue;
    }
    if (seen.has(entry.name)) throw new Error(`Duplicate skill name across categories: ${entry.name} (${seen.get(entry.name)} and ${category})`);
    seen.set(entry.name, category);
    skills.push({ name: entry.name, source });
  }
}

async function canonicalSkills() {
  const rootEntry = await existing(canonicalRoot);
  if (!rootEntry?.isDirectory()) throw new Error(`Missing canonical skill directory: ${canonicalRoot}`);

  const skills = [];
  await walkCategory(canonicalRoot, skills, new Map());
  return skills.sort((left, right) => left.name.localeCompare(right.name));
}

async function planProjections(skills) {
  const planned = [];
  const stale = [];
  for (const hostRoot of hostRoots) {
    const hostEntry = await existing(hostRoot);
    if (hostEntry && !hostEntry.isDirectory()) throw new Error(`Host skill root is not a directory: ${hostRoot}`);

    for (const skill of skills) {
      const target = path.join(hostRoot, skill.name);
      const targetEntry = await existing(target);
      if (!targetEntry) {
        planned.push({ ...skill, hostRoot, target });
        continue;
      }
      if (targetEntry.isSymbolicLink()) {
        if (await sameDirectory(target, skill.source)) continue; // already correct: nothing to do
        if (underCanonicalRoot(await rawLinkTarget(target))) {
          // Ours, but stale or dangling (its skill's category likely changed): replace it.
          stale.push(target);
          planned.push({ ...skill, hostRoot, target });
          continue;
        }
      }
      throw new Error(`Refusing to replace human-owned or conflicting path: ${target}`);
    }
  }
  return { planned, stale };
}

if (!(await existing(root))?.isDirectory()) throw new Error(`Root is not a directory: ${root}`);

const skills = await canonicalSkills();
const { planned, stale } = await planProjections(skills);

for (const target of stale) {
  // Always a symlink/junction (the only case that reaches `stale`), so `force` alone unlinks it;
  // no `recursive`, since that implies deleting a real directory's contents, which we never do here.
  await rm(target, { force: true });
}

for (const projection of planned) {
  await mkdir(projection.hostRoot, { recursive: true });
  const linkTarget = process.platform === "win32"
    ? projection.source
    : path.relative(path.dirname(projection.target), projection.source);
  await symlink(linkTarget, projection.target, process.platform === "win32" ? "junction" : "dir");
}

console.log(`Canonical skills ready: ${skills.length} skills, ${planned.length} projections created.`);
