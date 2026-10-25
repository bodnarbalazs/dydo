---
name: update-dydo
description: Update this project's dydo to the latest release, keeping every project tweak, and prove the update took effect. Load only when the user asks.
---

# Update dydo

Take what the project already has, tweaks included, apply what upstream added and changed, and
resolve the conflicts. Update only. An update that skips activation silently changes nothing, so
steps 7 and 8 are the heart of it.

Run this in the user's own top-level session, never a sub-agent: the guard denies sub-agents
`dydo adopt`, and the CLI install and protected-file merges need the user.

## Steps

1. **Installed?** No `dydo.json` at the project root: tell the user dydo is not installed here,
   point to https://github.com/bodnarbalazs/dydo#readme, and stop. No `skills/` folder at the root
   means dydo 2.x: tell the user the move to 3.0 is manual, point to
   https://github.com/bodnarbalazs/dydo/blob/master/dydo/project/changelog/2026/2026-08-27/dydo-3-0-0-linear-pm-and-notion-runtime-removal.md,
   and stop.
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
   - Old = `dydoVersion` in `dydo.json`. Absent (every project before 3.5.0), guess it: for each
     v3 release tag before 3.5.0, count the files that differ, or exist on one side only, across
     `skills/` and `Scaffold/dydo/` against `dydo/`, line endings normalised. Fewest wins; a tie
     goes to the newer tag. Tell the user it is a guess.
   - Old equals new: tell the user the project is current, then still run steps 6 to 8.
4. **Upgrade steps.** For each release after old, up to new, open its entry from the Releases list
   in `dydo/project/changelog/_changelog.md` at the new tag. From 3.5.0 each entry has an
   `## Upgrade steps` section: apply the steps this skill does not already perform, and skip
   those it does (CLI install, merge, `AGENTS.md`, `CLAUDE.md`, activation, `dydoVersion`).
   Older entries have none; read their Changed and Removed sections for the steps instead.
5. **Merge**, file by file, three ways: base = old tag, ours = the project, theirs = new tag. The
   files are `skills/**`, `setup-skills.mjs`, `THIRD-PARTY-NOTICES.md`, upstream `Scaffold/dydo/**`
   against the project's `dydo/**`, and upstream `Scaffold/entry-point.md` against the project's
   entry file.

   The entry file: ours is the one of `AGENTS.md` and `CLAUDE.md` that derives from the old
   rendered entry point, keeping its first heading and most of its lines. Before 3.5.0 a
   Claude-only project had only `CLAUDE.md`. Both derive: merge `AGENTS.md`, then merge
   `CLAUDE.md`'s edits into that result the same way. Neither derives: the result is the new entry
   point, rendered fresh. Write the result to `AGENTS.md`. An `AGENTS.md` that does not derive is
   the user's: put the result at its top, keep their text below it, and list the file for the
   user; when their text already covers the entry point, ask the user instead. `dydo init --join`
   leaves an existing `AGENTS.md` as it is, without a word. A `CLAUDE.md` that does not derive is
   the user's, for step 6. Render both tags' entry points as dydo does, trailing newlines trimmed, with
   `{{PROJECT_NAME}}` set to the name in ours' first heading, or the folder name with no ours. dydo
   wrote the folder name there at init; the folder may have been renamed or cloned since.

   Protected files: the guard blocks writes to the paths under Protected Patterns in the project's
   `dydo/files-off-limits.md`, `dydo/index.md` among them. For each protected file the merge
   changes, write the result to a temp file outside the project, show the user the diff, and ask
   them to apply it.

   Normalise line endings to LF before comparing or merging; a CRLF checkout against `git show`'s
   LF otherwise reads as an edit on every line. Write each result back with the project file's
   line endings.
   - Project never changed it: take the new upstream text.
   - Upstream never changed it: keep the project's.
   - Both changed: `git merge-file ours base theirs`, resolve keeping the project's intent, and
     list the file for the user.
   - Upstream renamed it (the release's upgrade steps list the rename): merge it as a move. Merge
     the project's edits into the new path, delete the old path, and list the move for the user.
     A file the rename dropped follows the deletion rule.
   - New upstream file: add it.
   - Upstream deleted it: remove it only if the project never edited it; otherwise ask the user.
   - Project deleted a file upstream changed: ask the user.
   - Remove directories the deletions leave empty.
   - A skill only the project has (its own `verify-<name>`, say): leave it alone.
   - A doc under `dydo/` that upstream never had: leave it alone.
6. **CLAUDE.md.** `AGENTS.md` is the only entry file. Step 7's `dydo init claude --join` removes
   `CLAUDE.md` only when it equals the new `AGENTS.md` or the new template, and warns about any
   other. Remove now a `CLAUDE.md` step 5 merged into `AGENTS.md`: `--join` cannot recognise it,
   and does not run when `claude` is not wired. A file the user wrote, one that does not derive
   from the old entry point, stays, with an import line at its top so Claude Code still reads
   `AGENTS.md`: `@AGENTS.md` in `CLAUDE.md` or `CLAUDE.local.md`, `@../AGENTS.md` in
   `.claude/CLAUDE.md`. Tell the user you added it; step 7 still warns about the file.
7. **Activate.** For each host set `true` under `integrations` in `dydo.json`, run
   `dydo init <host> --join`. Then run `node setup-skills.mjs` from the project root. Codex wired:
   tell the user to open Codex in the project and trust the changed hooks; an untrusted hook leaves
   every session unguarded.
8. **Prove.** All of these hold, or the update is not done:
   - every link in `.claude/skills/` and `.agents/skills/` that points into the project's `skills/`
     resolves to a `skills/**/<name>/SKILL.md`; setup leaves other entries alone;
   - every skill the new release added is listed in both (a skill folder at the new tag and not
     at the old, rename targets included);
   - every project-only skill is still listed in both;
   - every skill `AGENTS.md` names, backticked or in prose, exists under `skills/` (3.4.1's text
     names `issue-captain`);
   - each protected file the user applied by hand equals its merge result, line endings aside;
   - `dydo check` exits 0; warnings are fine;
   - every line of What to compare, below, is in order, or its finding is listed for the user.

   With every proof holding, run `dydo adopt <new>` to record the new version as `dydoVersion` in
   `dydo.json`.
9. **Report** to the user: old, new and CLI versions; files taken from upstream; files merged with
   both sides' edits; protected files they applied by hand; renamed skills moved; files left for
   them to decide; upgrade steps applied; the step 8 output. Add two notes:
   - Project text that names a renamed skill by its old name is now stale.
   - The skill projections and `.claude/settings.local.json` are git-ignored, so each teammate,
     after pulling, needs a CLI at the new version or later, then runs `dydo init <host> --join`
     and `node setup-skills.mjs`. An older CLI fails on the hook's `dydo guard --host`.

   The commit is the user's.

## What to compare

Each line: what in order looks like, then how to check it, from the project root.

**Host configs**
- `.claude/settings.json`: `env.CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH` is `"3"` or more.
  `cat .claude/settings.json`
- `.codex/config.toml`: `[agents]` has `max_depth` 3 or more and
  `max_concurrent_threads_per_session` 16 or more. `grep -A3 '^\[agents\]' .codex/config.toml`.
  A lower value in either file stops step 7's `init` with an error.
- Profile folders: every Claude config folder the user runs, `~/.claude` and any other set as
  `CLAUDE_CONFIG_DIR` (`~/.claude-work`, say). `echo "$CLAUDE_CONFIG_DIR"; ls -d ~/.claude*/`,
  then ask the user which are in use. Each has its own `settings.json` and memory store; walk
  the memory and hook lines for each.
- Memory: each switch and rule below reported, the user decides.
  - Claude: `autoMemoryEnabled` and `autoMemoryDirectory` in `.claude/settings.json`,
    `.claude/settings.local.json` and each profile's `settings.json`; env
    `CLAUDE_CODE_DISABLE_AUTO_MEMORY`. In the same files, each `permissions.deny` entry covering a
    memory folder (`Edit(//**/.claude*/projects/*/memory/**)`, say); deny rules apply without
    workspace trust.
    `grep -Hni -e memory -e '"deny"' .claude/settings*.json ~/.claude*/settings.json; echo "$CLAUDE_CODE_DISABLE_AUTO_MEMORY"`
  - Codex: `[features] memories`, off unless `true`, and `memories.generate_memories`, in
    `.codex/config.toml` and the user's `$CODEX_HOME/config.toml` (default `~/.codex/config.toml`).
    `grep -n -e '^\[' -e memories .codex/config.toml "${CODEX_HOME:-$HOME/.codex}/config.toml"`
  - Folders: every memory folder with files in it: `<profile>/projects/<this project>/memory/`,
    the `autoMemoryDirectory` folder when set, `.claude/agent-memory*/`, `$CODEX_HOME/memories/`
    (default `~/.codex/memories/`).

**Hooks**
- Claude guard: `.claude/settings.local.json` `hooks.PreToolUse` has one dydo entry, matcher
  `Edit|Write|Read|Bash|Glob|Grep|Agent|EnterPlanMode|ExitPlanMode|PowerShell|NotebookEdit`,
  command `dydo guard --host claude`, plus a `permissions.allow` list. MultiEdit is left out on
  purpose; the guard still reads one that arrives.
  `cat .claude/settings.local.json`
- Codex guard: `.codex/hooks.json` `hooks.PreToolUse` has one dydo entry, matcher
  `Bash|apply_patch|Edit|Write|Agent|shell_command|exec|local_shell|unified_exec`, command
  `dydo guard`; `.codex/rules/dydo.rules` exists. `cat .codex/hooks.json; ls .codex/rules`
- No `dydo guard --stop` Stop hook: step 7's `init` removes it. `grep -rn "guard --stop" .claude .codex`
- Project hooks: every other hook in those files and in each profile's `settings.json`, named
  with what it does. One a dydo command now covers (a PR-ready hook beside `dydo pr-ready`, say):
  show both and ask the user whether to drop it.

**Tooling**
- CLI: `dydo version` equals the new release (step 3).
- Skill links on both hosts: step 8's first three lines.
- `AGENTS.md` the only entry file: no `CLAUDE.md`, or a user's one carrying the import line
  (step 6). `ls AGENTS.md CLAUDE.md CLAUDE.local.md .claude/CLAUDE.md`
- Codex hook trust: the user confirms Codex trusted the changed `.codex/` layer (step 7).

**Skills**
- Renamed, added, removed, project-only: steps 5 and 8. `git status --short skills/` shows only
  the changes step 5 listed.
