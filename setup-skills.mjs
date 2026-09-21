#!/usr/bin/env node

import { lstat, mkdir, readdir, realpath, symlink } from "node:fs/promises";
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

async function canonicalSkills() {
  const rootEntry = await existing(canonicalRoot);
  if (!rootEntry?.isDirectory()) throw new Error(`Missing canonical skill directory: ${canonicalRoot}`);

  const categories = (await readdir(canonicalRoot, { withFileTypes: true }))
    .filter((entry) => entry.isDirectory())
    .sort((left, right) => left.name.localeCompare(right.name));
  if (categories.length === 0) throw new Error(`No canonical skills found in ${canonicalRoot}`);

  const skills = [];
  const seen = new Map();
  for (const category of categories) {
    const categoryRoot = path.join(canonicalRoot, category.name);
    const entries = (await readdir(categoryRoot, { withFileTypes: true }))
      .filter((entry) => entry.isDirectory())
      .sort((left, right) => left.name.localeCompare(right.name));
    if (entries.length === 0) throw new Error(`No canonical skills found in ${categoryRoot}`);

    for (const entry of entries) {
      const source = path.join(categoryRoot, entry.name);
      const body = path.join(source, "SKILL.md");
      if (!(await existing(body))?.isFile()) throw new Error(`Canonical skill is missing SKILL.md: ${category.name}/${entry.name}`);
      if (seen.has(entry.name)) throw new Error(`Duplicate skill name across categories: ${entry.name} (${seen.get(entry.name)} and ${category.name})`);
      seen.set(entry.name, category.name);
      skills.push({ name: entry.name, source });
    }
  }
  return skills.sort((left, right) => left.name.localeCompare(right.name));
}

async function planProjections(skills) {
  const planned = [];
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
      if (!targetEntry.isSymbolicLink() || !(await sameDirectory(target, skill.source))) {
        throw new Error(`Refusing to replace human-owned or conflicting path: ${target}`);
      }
    }
  }
  return planned;
}

if (!(await existing(root))?.isDirectory()) throw new Error(`Root is not a directory: ${root}`);

const skills = await canonicalSkills();
const planned = await planProjections(skills);

for (const projection of planned) {
  await mkdir(projection.hostRoot, { recursive: true });
  const linkTarget = process.platform === "win32"
    ? projection.source
    : path.relative(path.dirname(projection.target), projection.source);
  await symlink(linkTarget, projection.target, process.platform === "win32" ? "junction" : "dir");
}

console.log(`Canonical skills ready: ${skills.length} skills, ${planned.length} projections created.`);
