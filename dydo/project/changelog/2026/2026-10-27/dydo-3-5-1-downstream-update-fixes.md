---
area: general
type: changelog
date: 2026-10-27
---

# dydo 3.5.1 - Downstream update fixes

## Summary

Patch release fixing what a real downstream project (LC) hit while updating to 3.5.0. The guard now
reads the Update File and Delete File targets of a Codex `apply_patch`. The host-memory refusal
cites a Decision Record a project can open. FutureFeatures go to Linear in every shipped doc. The
`update-dydo` skill now handles a project whose files come from several releases, its own hooks and
its own tools.
<!-- SLOT DYD-391: one sentence on the PR-ready guard checking the right tree and covering the plain forms (LC claims 1, 2), witnessed by its merged PR. -->
<!-- SLOT DYD-392: one sentence on `dydo init --join` and `dydo adopt` respecting real downstream files and the committed Claude guard (LC claims 3, 4, 5), witnessed by its merged PR. -->
Existing projects take the new `update-dydo` skill and rerun `dydo init <host> --join`; see
Upgrade steps.

## Fixed

- A Codex `apply_patch` carries its paths only in the patch text, and the guard read only its
  `*** Add File:` and `*** Move to:` headers. The host-memory, off-limits and protected tiers now
  judge every target, `*** Update File:` and `*** Delete File:` included, in the main and worker
  lanes. Editing or deleting a guarded file through Codex was allowed before; it is now refused
  (DYD-396). A relative target resolves against the hook's `cwd`, or the process directory without
  one, with `..` collapsed; a `~` path is left for the host-memory check to expand. Only header
  lines count: a hunk or added-file line quoting patch syntax names no target. New-file nudges
  still fire only on Add File and Move to. On Windows a `..` no longer climbs above a UNC share or
  device root.
- The host-memory write refusal cited `dydo/project/decisions/055-native-agent-memory-off-and-guarded.md`,
  which only the dydo repository has. It now cites that record on GitHub at the running CLI's
  release tag (DYD-393, LC claim 7).
- The shipped FutureFeatures guide said FutureFeatures stay in the repository and out of Linear. A
  new FutureFeature is now a Linear ticket in status `FutureFeature`. The `future-features/` folder
  holds only legacy records, each removed once its migration to Linear is verified. `index.md`,
  `project/_project.md`, the READMEs, the getting-started and configuration docs and the
  documentation model say the same (DYD-393, LC claim 6). The shipped `_system/types.json` comment
  pointed at `dydo/reference/configuration.md`, which projects do not get; it now points at
  `dydo/reference/writing-docs.md`.
- `update-dydo` (DYD-394, LC update claims 8 to 13):
  - It no longer guesses one old tag for the whole project. Each file is merged against the newest
    tag holding a revision proven to be its ancestor. A file with no proven ancestor is listed for
    manual reconciliation. A path the project never had is added only if the new tag holds it.
  - It applies each release's `### Merge normalisations` to temporary copies of base, ours and
    theirs before the three-way merge, and renames live references to renamed skills instead of
    reporting them as stale.
  - It lists every hook in every file each host reads. It removes copies of dydo's hooks wired
    outside where `init` wires them, and dydo hooks whose subcommand, options or arguments the CLI
    does not accept, such as `dydo guard --stop`.
  - It keeps a project tool that overlaps a dydo command and covers more, and reports each case
    dydo misses as a dydo defect.
  - It checks `git status` in every checkout `git worktree list` shows and lists untracked
    leftovers of removed paths for deletion. Once the user has committed and pushed, it runs
    `dydo pr-ready`.
- The 3.5.0 entry's step 6 said `--join` removes dydo's Stop hook. The 3.5.0 CLI removes it only
  from `.claude/settings.local.json` and `.codex/hooks.json`, and the step now says so. Step 3 now
  renames live references to the old skill names. The 3.5.0 and 3.2.0 entries gained the
  `### Merge normalisations` their mechanical renames needed, and the changelog hub defines that
  section (DYD-394).
<!-- SLOT DYD-391: Fixed bullet(s) for the PR-ready guard checking the tree the PR is opened from and covering the plain `gh pr create` forms (LC claims 1, 2); every claim witnessed by the merged PR diff. -->
<!-- SLOT DYD-392: Fixed bullet(s) for `dydo init --join` and `dydo adopt` respecting real downstream files, the committed Claude guard, and the LC-shaped fixture added to CI (LC claims 3, 4, 5); every claim witnessed by the merged PR diff. -->

## Changed

- `.github/workflows/release.yml` now opens with the pre-tag release checklist. It lists every
  version pin to bump and requires an `update-dydo` dry run, with the release-candidate CLI, on a
  scratch copy of a real downstream project, with that project's hook tests replayed through
  `dydo guard`. The DYD-392 LC-shaped fixture must be green in `ci.yml`. The workflow's
  `workflow_dispatch` run on master publishes nothing. The tag is pushed only after the
  maintainer's go.
- Align runtime and npm versions to 3.5.1, point the package release notes at this entry, and
  permit publication only on the exact v3.5.1 tag.

## Upgrade steps

1. Install the 3.5.1 CLI: `npm install -g dydo` or `dotnet tool update -g dydo`.
2. Take `skills/productivity/update-dydo/SKILL.md` from the v3.5.1 tag before running it, so the
   update runs with the new procedure rather than the 3.5.0 skill.
3. Merge the release's `skills/` and `Scaffold/dydo/` changes into the project, keeping its edits.
   Those are `project/future-features/_future-features.md`, `index.md`, `project/_project.md` and
   `_system/types.json`. The guard blocks agent writes to `dydo/index.md`, so a person applies that
   merge. New FutureFeatures go to Linear from now on.
4. In every checkout `git worktree list` shows, run `dydo init <host> --join` for each wired host,
   then `node setup-skills.mjs` from its root.
   <!-- SLOT DYD-392: what `--join` now does to an existing project's committed Claude guard and real downstream files, and any hand step that leaves, witnessed by its merged PR. -->
5. Remove by hand any copy of a dydo hook outside where `--join` wires it, such as `dydo guard`
   or `dydo guard --stop` committed in `.claude/settings.json`; `--stop` no longer parses and fails
   at every turn end.
6. Delete a local `dydo/_system/audit/` folder if one is left; 3.5.0 removed it, and an untracked
   leftover makes `dydo pr-ready` report the tree not ready.
7. Codex hooks: no 3.5.1 change touches the Codex hook template `dydo init` writes, nor this
   repository's `.codex/hooks.json`. Re-trust the hooks in Codex only if step 4 or 5 changed
   `.codex/hooks.json`; Codex pins hook trust by hash, and an untrusted hook leaves Codex sessions
   unguarded.
   <!-- SLOT DYD-392: if its merged PR changes the Codex hooks template, say so here and make the re-trust unconditional. -->
   <!-- SLOT DYD-391: any upgrade step the merged PR-ready guard change requires of an existing project, or remove this slot if none. -->
8. Run `dydo adopt 3.5.1` last, once `dydo check` exits 0 and the update is proven.

No 3.5.1 commit renames a term mechanically, so this release has no merge normalisations.

## Files Changed

`Commands/GuardCommand.cs`, `Models/HookInputExtensions.cs`, `Utils/PathUtils.cs`,
`Scaffold/dydo/` (`index.md`, `project/_project.md`, `project/future-features/_future-features.md`,
`_system/types.json`), `dydo/index.md`, `dydo/project/_project.md`,
`dydo/project/future-features/` (the guide and five legacy records), `dydo/_system/types.json`,
`dydo/guides/getting-started.md`, `dydo/reference/configuration.md`,
`dydo/understand/documentation-model.md`, `README.md`, `npm/README.md`,
`skills/productivity/update-dydo/SKILL.md`, the 3.5.0 and 3.2.0 changelog entries,
`DynaDocs.csproj`, `npm/package.json`, `.github/workflows/release.yml`, `DynaDocs.Tests/`
(`Commands/GuardCommandTests.cs`, `Integration/GuardCodexApplyPatchTests.cs`,
`Integration/GuardNewFileNudgeIntegrationTests.cs`, `Integration/GuardWorkerLaneTests.cs`,
`Integration/GuardIntegrationTests.cs`, `Integration/DocumentationTests.cs`,
`Integration/InitCommandTests.cs`, `Integration/IntegrationTestBase.cs`,
`Models/HookInputExtensionsTests.cs`, `Services/PathUtilsTests.cs`,
`EndToEnd/CliEndToEndTests.cs`, `Workflow/ReleaseWorkflowTests.cs`), this entry and its changelog
hub.
<!-- SLOT DYD-391: the files its merged PR changes. -->
<!-- SLOT DYD-392: the files its merged PR changes, the LC-shaped CI fixture and DR 056 included if they land. -->

## Related

- [dydo 3.5.0 - Dispatch rule, decision skill and overnight mode](../2026-10-23/dydo-3-5-0-dispatch-decisions-overnight-mode.md)
- [Future Features](../../../future-features/_future-features.md) - Where FutureFeatures live now
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
