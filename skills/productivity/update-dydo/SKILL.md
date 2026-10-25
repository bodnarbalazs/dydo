---
name: update-dydo
description: Update this project's dydo to the latest release, keeping every project tweak, and prove the update took effect. Load only when the user asks.
---

# Update dydo

Take what the project already has, tweaks included, apply what upstream added and changed, and
resolve the conflicts. Update only. An update that skips activation silently changes nothing, so
steps 7 and 8 are the heart of it.

## Steps

1. **Installed?** No `dydo.json` at the project root: tell the user dydo is not installed here,
   point to https://github.com/bodnarbalazs/dydo#readme, and stop.
2. **Versions.**
   - New = the latest release tag: `gh release view -R bodnarbalazs/dydo --json tagName`.
   - CLI = `dydo version`. Older than new: update it first with whichever tool installed it,
     `npm install -g dydo` or `dotnet tool update -g dydo`.
   - Old = `dydoVersion` in `dydo.json`. Absent (every project before 3.5.0): take the release tag
     whose `skills/` tree is closest to the project's, and tell the user it is a guess.
   - Old equals new: tell the user the project is current, then still run steps 6 to 8.
3. **Fetch.** Clone https://github.com/bodnarbalazs/dydo into a temp directory outside the project.
   The clone carries both tags; read each file at either one with `git show <tag>:<path>`.
4. **Upgrade steps.** For each release after old, up to new, open its entry from the Releases list
   in `dydo/project/changelog/_changelog.md` at the new tag. From 3.5.0 each entry has an
   `## Upgrade steps` section: apply every step. Older entries have none; read their Changed and
   Removed sections for the steps instead.
5. **Merge**, file by file, three ways: base = old tag, ours = the project, theirs = new tag. The
   files are `skills/**`, `setup-skills.mjs`, `THIRD-PARTY-NOTICES.md`, and upstream
   `Scaffold/dydo/**` against the project's `dydo/**` (`Scaffold/entry-point.md` is step 6's).
   - Project never changed it: take the new upstream text.
   - Upstream never changed it: keep the project's.
   - Both changed: `git merge-file ours base theirs`, resolve keeping the project's intent, and
     list the file for the user.
   - New upstream file: add it. Upstream deleted it: remove it only if the project never edited
     it; otherwise ask the user.
   - A skill only the project has (its own `verify-<name>`, say): leave it alone.
6. **Entry file.** `AGENTS.md` is the only entry file. Step 7's `dydo init claude --join` removes a
   `CLAUDE.md` identical to `AGENTS.md` or to the new template, and warns about any other. Remove
   now the dydo copies it cannot recognise: a `CLAUDE.md` matching the old tag's
   `Scaffold/entry-point.md`, or any dydo copy when `claude` is not wired. One the user wrote: keep
   it, and pass the warning on to the user.
7. **Activate.** For each host set `true` under `integrations` in `dydo.json`, run
   `dydo init <host> --join`. Then run `node setup-skills.mjs` from the project root. Codex wired:
   tell the user to open Codex in the project and trust the changed hooks; an untrusted hook leaves
   every session unguarded.
8. **Prove.** All of these hold, or the update is not done:
   - every entry in `.claude/skills/` and `.agents/skills/` resolves to a `skills/**/<name>/SKILL.md`;
   - every skill the new release added is listed in both;
   - every project-only skill is still listed in both;
   - `dydo check` passes.

   Then set `dydoVersion` in `dydo.json` to the new tag without its `v`.
9. **Report** to the user: old, new and CLI versions; files taken from upstream; files merged with
   both sides' edits; files left for them to decide; upgrade steps applied; the step 8 output.
   The commit is the user's.
