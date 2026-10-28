---
name: update-dydo
description: Update this project's dydo to the latest release, keeping every project tweak, and prove the update took effect.
disable-model-invocation: true
---

# Update dydo

Take the project as it is, tweaks included, apply what upstream added and changed, and resolve the
conflicts. Update only. Skipping activation changes nothing, so steps 7 and 8 are the heart.

Find every concrete thing you compare in the project, the tags and a fresh reference install.
Release-specific work lives in each release's changelog entry, not here. Exclude off-limits
destinations from every comparison and merge, the search for bases included; list them for the
user to handle, like protected files, and continue.

Run this in the user's own top-level session, never a sub-agent: the guard denies sub-agents
`dydo adopt`, and the CLI install and protected-file merges need the user.

## Steps

1. **Fetch.** Clone https://github.com/bodnarbalazs/dydo into a temp directory outside the project.
   Read a file at a tag with `git show <tag>:<path>`.
2. **New and CLI.**
   - New = the latest release tag: `gh release view -R bodnarbalazs/dydo --json tagName`. No
     signed-in `gh`: the newest `v*` tag by version in the clone's `git ls-remote --tags origin`,
     skipping pre-releases (a `-` after the version) and peeled `^{}` refs.
   - CLI = `dydo version`. Older than new: update it with the tool that installed it and tell the
     user: `npm install -g dydo` when `npm ls -g dydo` lists it, `dotnet tool update -g dydo` when
     `dotnet tool list -g` does. From here on `dydo` is the updated CLI.
3. **Reference install.** Make an empty temp folder outside the project, with no `dydo.json` in
   any folder above it, and run `cd <temp> && dydo init all` as one command: init installs into
   its working directory and in the project refuses as already initialized; the guard passes a
   dydo command after `cd`. It asks nothing; its files are what the new release writes. It may add
   dydo's shell completion block to the user's shell profile once; tell the user.

   A tag's **sources** are the docs folder and entry-point template its `init` code embeds or
   copies; `git grep` at each tag finds them. The reference shows where each lands.
4. **Bases.** The merge set is every file a tag up to new tells a project to copy in its
   `README.md`, or holds among its sources, paired with the project file where the README or the
   reference puts it. Each file's base is the upstream text it derives from: match each revision in
   its project history (`git log --follow`), line endings normalised, against that upstream file
   at each tag, moves followed, templates rendered as `init` does. An exact match proves its tag;
   similarity is not ancestry. A revision in that history a recorded update to tag T left at the
   path proves T: the update's commits set `dydoVersion` to T, and its record (commit messages, PR)
   shows the file, or a folder holding it, merged, not skipped, base unknown or left to the user.
   `dydoVersion` alone proves no file; a record contradicting itself or the history proves
   nothing. Base = the newest proven tag, not after new. Nothing at the path, at a rename's source
   or in their history: new upstream if new holds it; else skip it. A project file with no proven
   ancestor: list it for manual reconciliation, base unknown, leave it. Every base new, nothing to
   add: tell the user the project is current; go to step 7.
   **Old** (reported only) = `dydo.json`'s adopted release, else the commonest base, a guess.
5. **Upgrade steps.** For each release after the oldest base, up to new, read the
   `## Upgrade steps` of its entry in the Releases list of `dydo/project/changelog/_changelog.md`
   at the new tag, else its Changed and Removed sections. A step about files applies to those
   whose base predates its release. Skip what this skill does: CLI install, merge, normalisations,
   activation, adopt. Run each other step where its text puts it: acting on merge results, after
   step 6; on `init` output or the new wiring, after step 7; else before step 6.
6. **Merge** file by file, three ways: base = the file's step 4 base, ours = the project, theirs =
   new tag. Upstream deletions are in the set, so the deletion rules fire.

   Normalise line endings to LF first; a CRLF checkout against `git show`'s LF otherwise reads as
   an edit on every line. Write each result back with the project file's line endings.
   Then apply the `### Merge normalisations` of each release after the file's base, up to new, in
   release order, to temporary copies of base, ours and theirs, and merge the copies. Keep the
   originals. Review each copy's diff against its original: restore any exception it rewrote.
   - Project never changed it: take upstream's.
   - Upstream never changed it: keep the project's.
   - Both changed: `git merge-file ours base theirs`, resolve keeping the project's intent, list it.
   - Upstream renamed it (an Upgrade step names the rename, or its whole folder moved): merge the
     project's edits into the new path, delete the old one, list the move. A file the rename
     dropped follows the deletion rule. Git's rename detection also pairs identical files across
     unrelated skills; a pair neither test confirms is a deletion plus an addition. Rename each
     live reference (instructions, skill text, entry file); keep historical records and quotations.
   - New upstream file: add it.
   - Upstream deleted it: remove it if the project never edited it; otherwise ask the user.
   - Project deleted a file upstream changed: ask the user.
   - Either deletion an Upgrade step of a release after the file's base settles (it retires, moves
     or deletes the file): follow the step instead of asking, and list it.
   - Remove directories the deletions leave empty.
   - A skill, or a doc under the docs root, that only the project has: leave it alone.

   **Entry file.** Ours is the project file derived from a rendered entry point: it keeps its
   first heading and most of its lines. Several derive: merge the one at the reference's entry
   path first, then the others' edits into that. None derives: render the new one fresh. Write the
   result where the reference writes the entry point. A non-derived file there is the user's: put
   the result at its top, keep their text below and list it; when their text already covers the
   entry point, ask the user instead. Render the base and new entry points as `init` does, trailing
   newlines trimmed, naming the project from ours' first heading (init wrote the folder name
   there; the folder may since be renamed or cloned), or the folder name with no ours.

   **Protected files.** The guard blocks writes to the Protected Patterns in the docs root's
   `files-off-limits.md`. For each one the merge changes, write the result to a temp file outside
   the project, show the user the diff, ask them to apply it, and carry on.

   Then run the Upgrade steps step 5 put after the merge.
7. **Activate** the update's own checkout: for each host `dydo.json` wires, run
   `dydo init <host> --join`, then `node setup-skills.mjs` from its root. Another checkout
   `git worktree list` shows waits until its own HEAD contains the update (`git -C <checkout>
   merge-base --is-ancestor <update commit> HEAD`), or until the user says go naming it: `--join`
   writes the committed `.claude/settings.json` (DR 056), so on a branch lacking the update it
   edits that branch. List each deferred checkout with what to run there. Codex wired: ask the
   user to open Codex in the project and trust the changed hooks; an untrusted hook leaves every
   session unguarded. Then run the Upgrade steps step 5 put after activation.
8. **Prove.** The update is done only when all of these hold:
   - every link `setup-skills.mjs` makes into `skills/` resolves to a `skills/**/<name>/SKILL.md`
     (read its host folders from the script; it leaves other entries alone);
   - every skill the new release added (rename targets included) and every project-only skill is
     linked in each of those folders;
   - every skill the entry file names, backticked or in prose, exists under `skills/`;
   - each protected file the user applied equals its merge result, line endings aside;
   - every Upgrade step step 5 kept is applied or listed for the user;
   - `dydo check` exits 0; warnings are fine;
   - every What to compare category is walked, each finding resolved or listed for the user;
   - in each checkout step 7 activated, `git status --short` lists only the update's changes;
     each untracked leftover of a path upstream removed is listed for the user to delete.

   With every proof holding, run `dydo adopt <new>` to record the new version in `dydo.json`.
9. **Report** to the user: old, new and CLI versions; files taken from upstream; files merged with
   both sides' edits; files with base unknown, to reconcile; protected files they applied;
   protected and off-limits files for them to handle; renamed skills moved and references renamed;
   files left for them to decide; leftovers to delete; upgrade steps applied; project tweaks kept;
   checkouts step 7 deferred, each with what to run there; dydo defects found; the step 8 output.
   Name the machine-local files, the git-ignored ones `init` and `setup-skills.mjs` write
   (`.gitignore`, the reference install): after pulling, each teammate needs a CLI at new or
   later, then runs `dydo init <host> --join` and `node setup-skills.mjs`.

   The commit is the user's; its message keeps the file lists above, the record the next update's
   step 4 reads. Once he has committed and pushed it, run
   `dydo pr-ready <the tree the PR comes from>` and fix what it lists until it prints `ready:`.

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

**Hooks.** Per host, list every hook in every file it reads hooks from, in the project and each
profile, found from the host's docs and those folders (committed and local settings, say). Each host
runs each dydo hook once, where the reference `init` wires it. A copy runs a command the reference
or a base's tag wires, elsewhere: remove and list it, or in a shared profile, show the user and ask.
Any other hook running a dydo command is a tweak: keep and list it. Remove only one whose subcommand
`dydo --help` does not list, or passing an option `dydo <cmd> --help` does not list, an option
without its value, or more positional arguments than that help documents, or missing a required one.

Name every other project hook or tool and what it does. One overlapping a dydo command (see
`dydo --help`): run both on the project tool's own tests or recorded inputs. dydo covers every
case: ask the user whether to drop it. dydo covers less or behaves differently: keep it without
asking, and report each such case as a dydo defect with its repro.

**Tooling.**
- Adaptations of dydo's own tooling: project files, beyond the skills and docs it took, derived
  from a dydo repo file at a tag up to new (a copy of `gap_check.py` or `run_tests.py`, say),
  whatever their name. Follow that file from each one's base (step 4) to new (`git log --follow`,
  `git diff --find-renames <base> <new>`); list upstream's edits, rename or deletion to carry over.
- CLI: `dydo version` equals new.
- Skill projection: step 8's link lines.
- Entry file: where the reference writes it; step 7's `init` warnings name others for the user.
- Codex hook trust: the user confirms Codex trusted the changed hooks.

**Skills.** In the clone, `git diff --no-renames --name-status <oldest base> <new> -- skills/`
lists added and removed files; step 6's rename rule says which are moves. `skills/` at new
against the project's shows the project-local ones.
