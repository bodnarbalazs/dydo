---
name: update-dydo
description: Update this project's dydo to the latest release, keeping every project tweak, and prove the update took effect.
disable-model-invocation: true
---

# Update dydo

Take the project as it is, tweaks included, apply what upstream added and changed, and resolve the
conflicts. Update only. Skipping activation changes nothing, so steps 7 and 8 are the heart.

Find every concrete thing you compare in the project, the tags and a fresh reference install.
Release-specific work lives in each release's changelog entry, not here.

Exclude off-limits destinations from every comparison and merge, including the old-version guess.
List them for the user to handle, like protected files, and continue.

Run this in the user's own top-level session, never a sub-agent: the guard denies sub-agents
`dydo adopt`, and the CLI install and protected-file merges need the user.

## Steps

1. **Fetch.** Clone https://github.com/bodnarbalazs/dydo into a temp directory outside the project.
   Read a file at a tag with `git show <tag>:<path>`.
2. **New and CLI.**
   - New = the latest release tag: `gh release view -R bodnarbalazs/dydo --json tagName`. Without
     a signed-in `gh`: the newest `v*` tag by version from
     `git ls-remote --tags https://github.com/bodnarbalazs/dydo`, skipping pre-releases (a `-`
     after the version) and peeled `^{}` refs.
   - CLI = `dydo version`. Older than new: update it with the tool that installed it and tell the
     user: `npm install -g dydo` when `npm ls -g dydo` lists it, `dotnet tool update -g dydo` when
     `dotnet tool list -g` does. From here on `dydo` is the updated CLI.
3. **Reference install.** In an empty temp folder outside the project, with no `dydo.json` in any
   folder above it, run `dydo init all`. It asks nothing; its files are what the new release
   writes. It may add dydo's shell completion block to the user's shell profile once; tell the user.

   A tag's **sources** are the docs folder and entry-point template its `init` code embeds or
   copies; `git grep` at each tag finds them. The reference shows where each lands.
4. **Old** = the release `dydo.json` records as adopted. None recorded: guess it. For every release
   tag, count the files that differ or exist on one side only, across `skills/` and the tag's docs
   source against the project's `skills/` and docs root (`dydo.json` names it), line endings
   normalised. Fewest wins; a tie goes to the newer tag. Tell the user it is a guess. Old equals
   new: tell the user the project is current and go on to step 7.
5. **Upgrade steps.** For each release after old, up to new, read the `## Upgrade steps` of its
   entry in the Releases list of `dydo/project/changelog/_changelog.md` at the new tag, else its
   Changed and Removed sections. Skip what this skill does: CLI install, merge, activation, adopt.
   Run each other step where its text puts it: acting on merge results, after step 6; on `init`
   output or the new wiring, after step 7; else before step 6.
6. **Merge** file by file, three ways: base = old tag, ours = the project, theirs = new tag. The
   set is every file at the old or new tag that the tag's `README.md` tells a project to copy, or
   that is among its sources, against the project file where the README or the reference puts it.
   Upstream deletions are in the set, so the deletion rules fire.

   Normalise line endings to LF first; a CRLF checkout against `git show`'s LF otherwise reads as
   an edit on every line. Write each result back with the project file's line endings.
   - Project never changed it: take upstream's.
   - Upstream never changed it: keep the project's.
   - Both changed: `git merge-file ours base theirs`, resolve keeping the project's intent, list it.
   - Upstream renamed it (an Upgrade step names the rename, or its whole folder moved): merge the
     project's edits into the new path, delete the old one, list the move. A file the rename
     dropped follows the deletion rule. Git's rename detection also pairs identical files across
     unrelated skills; a pair neither test confirms is a deletion plus an addition.
   - New upstream file: add it.
   - Upstream deleted it: remove it if the project never edited it; otherwise ask the user.
   - Project deleted a file upstream changed: ask the user.
   - Remove directories the deletions leave empty.
   - A skill, or a doc under the docs root, that only the project has: leave it alone.

   **Entry file.** Ours is the project file derived from the old rendered entry point: it keeps its
   first heading and most of its lines. Several derive: merge the one at the reference's entry
   path first, then the others' edits into that. None derives: render the new one fresh. Write the
   result where the reference writes the entry point. A non-derived file there is the user's: put
   the result at its top, keep their text below and list it; when their text already covers the
   entry point, ask the user instead. Render both tags' entry points as `init` does, trailing
   newlines trimmed, naming the project from ours' first heading (init wrote the folder name
   there; the folder may since be renamed or cloned), or the folder name with no ours.

   **Protected files.** The guard blocks writes to the Protected Patterns in the docs root's
   `files-off-limits.md`. For each one the merge changes, write the result to a temp file outside
   the project, show the user the diff, ask them to apply it, and carry on.

   Then run the Upgrade steps step 5 put after the merge.
7. **Activate.** For each host `dydo.json` wires, run `dydo init <host> --join`, then
   `node setup-skills.mjs` from the project root. Codex wired: ask the user to open Codex in the
   project and trust the changed hooks; an untrusted hook leaves every session unguarded. Then run
   the Upgrade steps step 5 put after activation.
8. **Prove.** The update is done only when all of these hold:
   - every link `setup-skills.mjs` makes into `skills/` resolves to a `skills/**/<name>/SKILL.md`
     (read its host folders from the script; it leaves other entries alone);
   - every skill the new release added (rename targets included) and every project-only skill is
     linked in each of those folders;
   - every skill the entry file names, backticked or in prose, exists under `skills/`;
   - each protected file the user applied equals its merge result, line endings aside;
   - every Upgrade step step 5 kept is applied or listed for the user;
   - `dydo check` exits 0; warnings are fine;
   - every What to compare category is walked, each finding resolved or listed for the user.

   With every proof holding, run `dydo adopt <new>` to record the new version in `dydo.json`.
9. **Report** to the user: old, new and CLI versions; files taken from upstream; files merged with
   both sides' edits; protected files they applied; protected and off-limits files for them to
   handle; renamed skills moved; files left for them to decide; upgrade steps applied; project
   tweaks kept; the step 8 output. Add two notes:
   - Project text naming a renamed skill by its old name is now stale.
   - The machine-local files: the git-ignored files `init` and `setup-skills.mjs` write, found
     from `.gitignore` and the reference install; name them. After pulling, each teammate needs a
     CLI at new or later, then runs `dydo init <host> --join` and `node setup-skills.mjs`.

   The commit is the user's.

## What to compare

The reference is step 3's install. Per category, find what is there and compare it with the
reference or the new tag. A difference is upstream's, to take, or a project tweak, to keep and list.

**Host configs**, memory settings included. Find every host config folder and profile: the
project's own (the folders the reference writes), the hosts' config-dir env vars
(`CLAUDE_CONFIG_DIR`, `CODEX_HOME`) and the home folders (`ls -d ~/.claude* ~/.codex*`); ask the
user which are in use. Compare each setting the reference writes with the project's and each
profile's. A setting only the project or a profile has is a tweak: keep and list it. A profile
value contradicting what the reference writes in the project (turning back on what the project
turns off, say): show both and ask the user. List each host memory folder holding notes for this
project, found from the host's settings and docs; the release entries say what to do with them.

**Hooks.** Per host, compare what the project and each profile wire with what the reference
`init` wires. Name every other project hook and what it does; one a dydo command now covers (see
`dydo --help`): show both and ask the user whether to drop it.

**Tooling.**
- Adaptations of dydo's own tooling: project files, beyond the skills and docs it took, derived
  from a dydo repo file at the old or new tag (a copy of `gap_check.py` or `run_tests.py`, say),
  whatever their name. Follow that file across the tags (`git log --follow`,
  `git diff --find-renames <old> <new>`); list upstream's edits, rename or deletion to carry over.
- CLI: `dydo version` equals new.
- Skill projection: step 8's link lines.
- Entry file: the entry point sits where the reference writes it; step 7's `init` warnings name
  any other entry file, for the user.
- Codex hook trust: the user confirms Codex trusted the changed hooks.

**Skills.** In the clone, `git diff --no-renames --name-status <old> <new> -- skills/` lists added
and removed files; step 6's rename rule says which are moves. `skills/` at the new tag against the
project's shows the project-local ones. `git status --short skills/` shows only step 6's changes.
