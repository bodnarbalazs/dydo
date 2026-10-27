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
its own tools. The PR-ready guard checks the tree a PR shows, a named branch's worktree or Codex's
`workdir` included, and catches the plain `gh` forms it missed. `dydo init --join` and `dydo adopt`
edit a project's JSON files in place instead of rewriting them, `--join` reads a BOM-led
`.codex/config.toml`, and the Claude guard moves into the committed `.claude/settings.json`.
Existing projects take the new `update-dydo` skill, rerun `dydo init <host> --join` and commit the
moved Claude guard; see Upgrade steps.

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
- PR-ready guard (DYD-391, LC update claims 1 and 2;
  [Guard System](../../../../understand/guard-system.md)):
  - It checked only the hook payload's `cwd`, so `gh pr create --head topic` from a dirty main was
    denied for main, and a dirty Codex `workdir` passed from a clean session. The tree is now the
    tool input's `cwd`, else Codex's `workdir` (a relative one under the payload's `cwd`), else
    the payload's `cwd`, else the project root. A call naming its branch (`--head [OWNER:]BRANCH`,
    `gh pr ready BRANCH`, a REST `head` field) is checked in every worktree holding that branch,
    and refused when none does. The refusal names every tree it checked.
  - It now catches the plain forms that went unchecked: `gh` under any path, case or `.exe`;
    `-R`/`--repo` before `pr`; `VAR=value`, `env`, `timeout`, `nice`, `sudo` and similar runner
    prefixes; `$(...)` and bash backtick substitutions; a `gh api` POST to
    `repos/OWNER/REPO/pulls`; and `gh api graphql` naming `createPullRequest` or
    `markPullRequestReadyForReview`. The PowerShell tool and Codex on Windows are read as
    PowerShell. A Codex argv-array `command` no longer fails the payload: a shell envelope such as
    `["bash", "-lc", script]` is checked as its script.
  - A call acting on a repository other than the tree's `origin`, through `-R`, `GH_REPO`, a PR
    URL or a `gh api` endpoint, is refused: run it from that repository's checkout. A
    `bash -c`, `pwsh -Command`, `-EncodedCommand`, `cmd /c`, `eval` or `env -S` holding a PR call
    is refused unread, asking to run `gh` directly. Both passed unchecked before.
  - `dydo pr-ready --body` accepts a hyphen, an en dash or an em dash after `PASS`.
- `dydo init codex --join` stopped with exit 2 on a `.codex/config.toml` that starts with a BOM. It
  now reads it, keeps the BOM and sets `memories = false` in its one `[features]` table (DYD-392).
- `dydo init`, `--join` and `dydo adopt` rewrote `dydo.json`, `.claude/settings.json`,
  `.claude/settings.local.json` and `.codex/hooks.json` from scratch: values re-escaped, CRLF on
  Windows, the final newline dropped, and `dydo.json`'s unknown keys lost. They now edit the
  file's text: unchanged values keep their bytes, key order and unknown keys survive, the file
  keeps its BOM, encoding, line endings and trailing newline, and a file with nothing to change is
  not written. A `--join` that changes nothing in `.codex/hooks.json` no longer changes the hash
  Codex trusts (DYD-392, LC update claims 3 to 5).
- `DynaDocs.Tests/Integration/DownstreamConfigTests.cs` runs `init claude --join`,
  `init codex --join` and `adopt` on byte-exact copies of LC's config in
  `DynaDocs.Tests/TestData/downstream-lc/`, which `.gitattributes` keeps out of line-ending
  normalisation. Only dydo's own lines may change, and a rerun must leave every file
  byte-identical. It runs in `ci.yml`'s `dotnet test` (DYD-392).

## Changed

- The Claude guard is committed ([DR 056](../../../decisions/056-claude-guard-in-committed-settings.md),
  DYD-392, reversing issue 0303). `dydo init claude` and `--join` write one `PreToolUse` entry
  running `dydo guard --host claude` into `.claude/settings.json`, where dydo's earlier entry
  stood. They remove any other dydo guard entry and a Stop entry running `dydo guard --stop`, and
  leave hooks dydo did not write alone. `.claude/settings.local.json` keeps only the allow list
  and loses the guard and Stop hooks an older dydo wired there. A fresh clone is now guarded on
  Claude Code as on Codex; a teammate without the dydo CLI on the path gets a non-blocking hook
  error on each matched tool call.
- `.github/workflows/release.yml` now opens with the pre-tag release checklist. It lists every
  version pin to bump and requires an `update-dydo` dry run, with the release-candidate CLI, on a
  scratch copy of a real downstream project, with that project's hook tests replayed through
  `dydo guard`. The LC fixture in `DownstreamConfigTests.cs` must be green in `ci.yml`. The
  workflow's `workflow_dispatch` run on master publishes nothing. The tag is pushed only after the
  maintainer's go.
- Align runtime and npm versions to 3.5.1, point the package release notes at this entry, and
  permit publication only on the exact v3.5.1 tag.

## Upgrade steps

1. Install the 3.5.1 CLI: `npm install -g dydo` or `dotnet tool update -g dydo`.
2. Take `skills/productivity/update-dydo/SKILL.md` from the v3.5.1 tag before running it, so the
   update runs with the new procedure rather than the 3.5.0 skill.
3. Merge the release's `skills/` and `Scaffold/dydo/` changes into the project, keeping its edits.
   Those are `project/future-features/_future-features.md`, `index.md`, `project/_project.md`,
   `reference/dydo-commands.md` and `_system/types.json`. The guard blocks agent writes to
   `dydo/index.md`, so a person applies that merge. New FutureFeatures go to Linear from now on.
4. In every checkout `git worktree list` shows, run `dydo init <host> --join` for each wired host,
   then `node setup-skills.mjs` from its root. For Claude Code, `--join` moves the guard into the
   committed `.claude/settings.json` and removes dydo's hooks from `.claude/settings.local.json`.
   Check that `git diff -- .claude/settings.json` shows one `PreToolUse` entry running
   `dydo guard --host claude` with the current matcher, no `dydo guard --stop`, and the project's
   other hooks unchanged, then commit it. `settings.local.json` stays uncommitted and keeps only
   the allow entries. Everyone who opens the project in Claude Code needs the dydo CLI on the
   path from now on.
5. `--join` replaces or removes the exact dydo commands `dydo guard`, `dydo guard --host claude`
   and `dydo guard --stop` in `.claude/settings.json`, `.claude/settings.local.json` and
   `.codex/hooks.json`. Remove by hand any other copy of a dydo hook, such as one spelled
   differently or wired in a file `--join` does not edit; a leftover `dydo guard --stop` no longer
   parses and fails at every turn end.
6. Delete a local `dydo/_system/audit/` folder if one is left; 3.5.0 removed it, and an untracked
   leftover makes `dydo pr-ready` report the tree not ready.
7. Codex hooks: no 3.5.1 change alters the Codex hook entry `dydo init` writes, its matcher or
   its `dydo guard` command, nor this repository's `.codex/hooks.json`. `--join` now writes
   `.codex/hooks.json` only when dydo's entries in it need a change: an older matcher or a
   `dydo guard --stop` Stop entry, both in LC's file, or a second dydo guard entry. After steps 4 and 5, run
   `git diff --stat -- .codex/hooks.json`. If it lists the file, commit it and re-trust the hooks
   in Codex; Codex pins hook trust by hash, and an untrusted hook leaves Codex sessions unguarded.
8. Run `dydo adopt 3.5.1` last, once `dydo check` exits 0 and the update is proven.

No 3.5.1 commit renames a term mechanically, so this release has no merge normalisations.

## Files Changed

`Commands/GuardCommand.cs`, `Commands/GuardCommand.PrReady.cs`, `Commands/InitCommand.cs`,
`Models/HookInputExtensions.cs`, `Models/ToolInputData.cs`, `Utils/PathUtils.cs`, `Services/`
(`CommandPosition.cs`, `PrCall.cs`, `PrCallScanner.cs`, `DydoInvocationScanner.cs`,
`PrReadyCheck.cs`, `ConfigService.cs`), `Serialization/` (`JsonText.cs`, `StoredText.cs`),
`.gitattributes`, `Scaffold/dydo/` (`index.md`, `project/_project.md`,
`project/future-features/_future-features.md`, `reference/dydo-commands.md`,
`_system/types.json`), `dydo/index.md`, `dydo/project/_project.md`,
`dydo/project/future-features/` (the guide and five legacy records), `dydo/_system/types.json`,
`dydo/project/decisions/056-claude-guard-in-committed-settings.md`,
`dydo/guides/getting-started.md`, `dydo/reference/configuration.md`,
`dydo/reference/dydo-commands.md`, `dydo/understand/documentation-model.md`,
`dydo/understand/guard-system.md`, `README.md`, `npm/README.md`,
`skills/productivity/update-dydo/SKILL.md`, the 3.5.0 and 3.2.0 changelog entries,
`DynaDocs.csproj`, `npm/package.json`, `.github/workflows/release.yml`, `DynaDocs.Tests/`
(`Commands/GuardCommandTests.cs`, `Integration/GuardCodexApplyPatchTests.cs`,
`Integration/GuardNewFileNudgeIntegrationTests.cs`, `Integration/GuardWorkerLaneTests.cs`,
`Integration/GuardIntegrationTests.cs`, `Integration/GuardModeSetTests.cs`,
`Integration/GuardPrReadyTests.cs`, `Integration/PrReadyCommandTests.cs`,
`Integration/DocumentationTests.cs`, `Integration/InitCommandTests.cs`,
`Integration/IntegrationTestBase.cs`, `Integration/DownstreamConfigTests.cs`,
`TestData/downstream-lc/` (`claude-settings.json`, `codex-config.toml`, `codex-hooks.json`,
`dydo.json`), `PreservedText.cs`, `PreservedTextTests.cs`, `Serialization/JsonTextTests.cs`,
`Models/HookInputExtensionsTests.cs`, `Services/PathUtilsTests.cs`,
`Services/CommandPositionTests.cs`, `Services/DydoInvocationScannerTests.cs`,
`Services/PrCallScannerTests.cs`, `Services/ConfigServiceTests.cs`,
`coverage/test-associations.json`, `EndToEnd/CliEndToEndTests.cs`,
`Workflow/ReleaseWorkflowTests.cs`), this entry and its changelog hub.

## Related

- [dydo 3.5.0 - Dispatch rule, decision skill and overnight mode](../2026-10-23/dydo-3-5-0-dispatch-decisions-overnight-mode.md)
- [Future Features](../../../future-features/_future-features.md) - Where FutureFeatures live now
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
