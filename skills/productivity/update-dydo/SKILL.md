---
name: update-dydo
description: Update this project's dydo to the latest release, keeping every project tweak, and prove the update took effect.
disable-model-invocation: true
---

# Update dydo

Take what the project already has, tweaks included, apply what upstream added and changed, and
resolve the conflicts. Update only. An update that skips activation silently changes nothing, so
steps 7 and 8 are the heart of it.

Every concrete thing you compare is found, never recalled: read the project, the tags and a fresh
reference install. Release-specific work lives in each release's changelog entry, not here.

Run this in the user's own top-level session, never a sub-agent: the guard denies sub-agents
`dydo adopt`, and the CLI install and protected-file merges need the user.

## Steps

1. **Installed?** No `dydo.json` at the project root: tell the user dydo is not installed here,
   point to https://github.com/bodnarbalazs/dydo#readme, and stop. No `skills/` folder at the root:
   the project predates the skill tree and the move is manual. Point to the changelog,
   https://github.com/bodnarbalazs/dydo/blob/master/dydo/project/changelog/_changelog.md, and stop.
2. **Fetch.** Clone https://github.com/bodnarbalazs/dydo into a temp directory outside the project.
   Read a file at a tag with `git show <tag>:<path>`.
3. **Versions.**
   - New = the latest release tag: `gh release view -R bodnarbalazs/dydo --json tagName`. Without
     a signed-in `gh`: the newest `v*` release tag by version from
     `git ls-remote --tags https://github.com/bodnarbalazs/dydo`, skipping pre-release tags (a `-`
     after the version) and the peeled `^{}` refs.
   - CLI = `dydo version`. Older than new: tell the user, then update it with the tool that
     installed it: `npm install -g dydo` when `npm ls -g dydo` lists it, `dotnet tool update -g dydo`
     when `dotnet tool list -g` does. From here on `dydo` is the updated CLI.
   - Old = the release `dydo.json` records as adopted. None recorded: guess it. For each release
     tag with a `skills/` folder, count the files that differ, or exist on one side only, across
     `skills/` and `Scaffold/dydo/` against the project's `skills/` and `dydo/`, line endings
     normalised. Fewest wins; a tie goes to the newer tag. Tell the user it is a guess.
   - Old equals new: tell the user the project is current, then still run steps 7 and 8.
4. **Reference install.** In an empty temp folder outside the project, with no `dydo.json` in any
   folder above it, run `dydo init all`. It writes only there and asks nothing. Its files are what
   the new release writes.
5. **Upgrade steps.** For each release after old, up to new, open its entry from the Releases list
   in `dydo/project/changelog/_changelog.md` at the new tag. Apply its `## Upgrade steps`, skipping
   those this skill already performs: CLI install, merge, activation, adopt. An entry without that
   section: take its steps from its Changed and Removed sections.
6. **Merge**, file by file, three ways: base = old tag, ours = the project, theirs = new tag. The
   set is found at the new tag: the files its `README.md` tells a project to copy, and what
   `dydo init` renders from its `Scaffold/`, each against the project file at the place the
   reference install puts it.

   The entry file: ours is the project file that derives from the old rendered entry point,
   keeping its first heading and most of its lines. More than one derives: merge the one at the
   reference's entry path first, then merge the others' edits into that result the same way.
   None derives: the result is the new entry point, rendered fresh. Write the result where the
   reference install writes the entry point. A file there that does not derive is the user's: put
   the result at its top, keep their text below it, and list the file for the user; when their
   text already covers the entry point, ask the user instead. Render both tags' entry points as
   `init` does, trailing newlines trimmed, with the project name from ours' first heading, or the
   folder name with no ours. dydo wrote the folder name there at init; the folder may have been
   renamed or cloned since.

   Protected files: the guard blocks writes to the paths under Protected Patterns in the project's
   `dydo/files-off-limits.md`. For each protected file the merge changes, write the result to a
   temp file outside the project, show the user the diff, and ask them to apply it.

   Normalise line endings to LF before comparing or merging; a CRLF checkout against `git show`'s
   LF otherwise reads as an edit on every line. Write each result back with the project file's
   line endings.
   - Project never changed it: take the new upstream text.
   - Upstream never changed it: keep the project's.
   - Both changed: `git merge-file ours base theirs`, resolve keeping the project's intent, and
     list the file for the user.
   - Upstream renamed it (an upgrade step or the Skills diff below shows it): merge it as a move.
     Merge the project's edits into the new path, delete the old path, and list the move for the
     user. A file the rename dropped follows the deletion rule.
   - New upstream file: add it.
   - Upstream deleted it: remove it only if the project never edited it; otherwise ask the user.
   - Project deleted a file upstream changed: ask the user.
   - Remove directories the deletions leave empty.
   - A skill, or a doc under `dydo/`, that only the project has: leave it alone.
7. **Activate.** For each host `dydo.json` wires, run `dydo init <host> --join`. Then run
   `node setup-skills.mjs` from the project root. Codex wired: tell the user to open Codex in the
   project and trust the changed hooks; an untrusted hook leaves every session unguarded.
8. **Prove.** All of these hold, or the update is not done:
   - every link `setup-skills.mjs` makes into the project's `skills/` resolves to a
     `skills/**/<name>/SKILL.md`; read its host folders from the script; it leaves other entries
     alone;
   - every skill the new release added (rename targets included) and every project-only skill is
     linked in each of those folders;
   - every skill the entry file names, backticked or in prose, exists under `skills/`;
   - each protected file the user applied by hand equals its merge result, line endings aside;
   - `dydo check` exits 0; warnings are fine;
   - every category of What to compare, below, is walked, each finding resolved or listed for the
     user.

   With every proof holding, run `dydo adopt <new>` to record the new version in `dydo.json`.
9. **Report** to the user: old, new and CLI versions; files taken from upstream; files merged with
   both sides' edits; protected files they applied by hand; renamed skills moved; files left for
   them to decide; upgrade steps applied; project tweaks kept; the step 8 output. Add two notes:
   - Project text that names a renamed skill by its old name is now stale.
   - The machine-local files: the git-ignored files `init` and `setup-skills.mjs` write, found
     from `.gitignore` and the reference install. Name them. Each teammate, after pulling, needs a
     CLI at the new version or later, then runs `dydo init <host> --join` and
     `node setup-skills.mjs` to regenerate them.

   The commit is the user's.

## What to compare

The reference is step 4's install. For each category, find what is there, then compare it with
what the reference or the new tag has. A difference is upstream's, to take, or a project tweak, to
keep and list.

**Host configs**, memory settings included. Find every host config folder and profile: the
project's own (the folders the reference writes), the hosts' config-dir env vars
(`CLAUDE_CONFIG_DIR`, `CODEX_HOME`) and the home folders (`ls -d ~/.claude* ~/.codex*`). Ask the
user which are in use. Compare each setting the reference writes with the project's and each
profile's. A setting only the project or a profile has is a project tweak: keep it and list it.

**Hooks.** Per host, read what the project and each profile wire, and compare it with what the
reference `init` wires. Name every other project hook with what it does. One a dydo command now
covers (see `dydo --help`): show both and ask the user whether to drop it.

**Tooling.**
- Adaptations of dydo's own tooling: project files sharing a name or origin with a file in the
  dydo repo at the new tag, beyond the skills and docs it took (a copy of `gap_check.py` or
  `run_tests.py`, say). Diff each against old and new tag; list upstream's changes for the user to
  carry over.
- CLI: `dydo version` equals new.
- Skill projection: step 8's link lines.
- Entry file: the entry point sits where the reference writes it; step 7's `init` warnings name
  any other entry file, for the user.
- Codex hook trust: the user confirms Codex trusted the changed hooks.

**Skills.** `git diff --find-renames --stat <old> <new> -- skills/` in the clone lists the added,
renamed and removed skills; `skills/` at the new tag against the project's shows the project-local
ones. `git status --short skills/` shows only the changes step 6 listed.
