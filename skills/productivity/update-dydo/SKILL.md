---
name: update-dydo
description: Update this project's dydo to the latest release, keeping every project tweak, and prove the update took effect.
disable-model-invocation: true
---

# Update dydo

Take what the project already has, tweaks included, apply what upstream added and changed, and
resolve the conflicts. Update only. An update that skips activation silently changes nothing, so
steps 8 and 9 are the heart of it.

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
3. **New and CLI.**
   - New = the latest release tag: `gh release view -R bodnarbalazs/dydo --json tagName`. Without
     a signed-in `gh`: the newest `v*` release tag by version from
     `git ls-remote --tags https://github.com/bodnarbalazs/dydo`, skipping pre-release tags (a `-`
     after the version) and the peeled `^{}` refs.
   - CLI = `dydo version`. Older than new: tell the user, then update it with the tool that
     installed it: `npm install -g dydo` when `npm ls -g dydo` lists it, `dotnet tool update -g dydo`
     when `dotnet tool list -g` does. From here on `dydo` is the updated CLI.
4. **Reference install.** In an empty temp folder outside the project, with no `dydo.json` in any
   folder above it, run `dydo init all`. It asks nothing, and its files are what the new release
   writes. Like any `init`, it may also add dydo's shell completion block to the user's shell
   profile, once; tell the user.

   A tag's **sources** are the docs folder and entry-point template its own `init` code embeds or
   copies; find them there (`git grep` at the tag) for each tag you read. The reference shows
   where each lands in a project.
5. **Old** = the release `dydo.json` records as adopted. None recorded: guess it. For each release
   tag with a `skills/` folder, count the files that differ, or exist on one side only, across
   `skills/` and the tag's docs source against the project's `skills/` and docs root (`dydo.json`
   names it), line endings normalised. Fewest wins; a tie goes to the newer tag. Tell the user it
   is a guess. Old equals new: tell the user the project is current, then still run steps 8 and 9.
6. **Upgrade steps.** For each release after old, up to new, open its entry from the Releases list
   in `dydo/project/changelog/_changelog.md` at the new tag and read its `## Upgrade steps`; an
   entry without that section: take its steps from its Changed and Removed sections. Skip those
   this skill already performs: CLI install, merge, activation, adopt. Apply each other step where
   its own text puts it: one that acts on merge results right after step 7, one that acts on
   `init` output or the new wiring right after step 8, any other before step 7.
7. **Merge**, file by file, three ways: base = old tag, ours = the project, theirs = new tag. The
   set is every file at the old tag or the new tag that the tag's `README.md` tells a project to
   copy or that is among its sources, each against the project file where the README or the
   reference install puts it. Files upstream deleted are in the set, so the deletion rules fire.

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

   Protected files: the guard blocks writes to the paths under Protected Patterns in the
   `files-off-limits.md` in the project's docs root. For each protected file the merge changes,
   write the result to a temp file outside the project, show the user the diff, and ask them to
   apply it.

   Normalise line endings to LF before comparing or merging; a CRLF checkout against `git show`'s
   LF otherwise reads as an edit on every line. Write each result back with the project file's
   line endings.
   - Project never changed it: take the new upstream text.
   - Upstream never changed it: keep the project's.
   - Both changed: `git merge-file ours base theirs`, resolve keeping the project's intent, and
     list the file for the user.
   - Upstream renamed it (an Upgrade step names the rename, or its whole folder moved): merge it
     as a move. Merge the project's edits into the new path, delete the old path, and list the
     move for the user. A file the rename dropped follows the deletion rule. Git's rename
     detection also pairs identical files across unrelated skills; any pair it shows that neither
     test confirms is a deletion plus an addition.
   - New upstream file: add it.
   - Upstream deleted it: remove it only if the project never edited it; otherwise ask the user.
   - Project deleted a file upstream changed: ask the user.
   - Remove directories the deletions leave empty.
   - A skill, or a doc under the docs root, that only the project has: leave it alone.

   Then apply the Upgrade steps step 6 put after the merge.
8. **Activate.** For each host `dydo.json` wires, run `dydo init <host> --join`. Then run
   `node setup-skills.mjs` from the project root. Codex wired: tell the user to open Codex in the
   project and trust the changed hooks; an untrusted hook leaves every session unguarded. Then
   apply the Upgrade steps step 6 put after activation.
9. **Prove.** All of these hold, or the update is not done:
   - every link `setup-skills.mjs` makes into the project's `skills/` resolves to a
     `skills/**/<name>/SKILL.md`; read its host folders from the script; it leaves other entries
     alone;
   - every skill the new release added (rename targets included) and every project-only skill is
     linked in each of those folders;
   - every skill the entry file names, backticked or in prose, exists under `skills/`;
   - each protected file the user applied by hand equals its merge result, line endings aside;
   - every Upgrade step step 6 kept is applied, or listed for the user;
   - `dydo check` exits 0; warnings are fine;
   - every category of What to compare, below, is walked, each finding resolved or listed for the
     user.

   With every proof holding, run `dydo adopt <new>` to record the new version in `dydo.json`.
10. **Report** to the user: old, new and CLI versions; files taken from upstream; files merged with
   both sides' edits; protected files they applied by hand; renamed skills moved; files left for
   them to decide; upgrade steps applied; project tweaks kept; the step 9 output. Add two notes:
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
A profile value that contradicts what the reference writes in the project (a profile turning back
on what the project turns off, say) is a finding: show both and ask the user. List each host
memory folder that holds notes for this project, found from the host's settings and docs; the
release entries say what to do with them.

**Hooks.** Per host, read what the project and each profile wire, and compare it with what the
reference `init` wires. Name every other project hook with what it does. One a dydo command now
covers (see `dydo --help`): show both and ask the user whether to drop it.

**Tooling.**
- Adaptations of dydo's own tooling: project files, beyond the skills and docs it took, whose
  content derives from a dydo repo file at the old or the new tag (a copy of its coverage
  `gap_check.py` or `run_tests.py`, say), whatever their name. Follow that file across the tags
  (`git log --follow`, `git diff --find-renames <old> <new>`); list upstream's edits, rename or
  deletion for the user to carry over.
- CLI: `dydo version` equals new.
- Skill projection: step 9's link lines.
- Entry file: the entry point sits where the reference writes it; step 8's `init` warnings name
  any other entry file, for the user.
- Codex hook trust: the user confirms Codex trusted the changed hooks.

**Skills.** `git diff --no-renames --name-status <old> <new> -- skills/` in the clone lists the
added and removed files; step 7's rename rule says which are moves. `skills/` at the new tag
against the project's shows the project-local ones. `git status --short skills/` shows only the
changes step 7 listed.
