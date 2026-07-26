---
title: v2.2.3 Upgrade Compatibility
campaign: dydo-2-0
end:
gate-result: plan-review PASS (2026-07-26, 5 rounds)
seq: 9
start: 2026-07-26
status: active
area: project
type: context
---

# v2.2.3 Upgrade Compatibility

Land the focused fixes accumulated after v2.2.2 and close the framework-side gaps exposed by the LC upgrade audit, then publish v2.2.3 from a fully green tree.

## Specification

Done means:

1. Issues 0300-0305 are represented by coherent production changes and regression coverage: integration state is recorded and consumed compatibly, `init all` works, Claude's machine-local settings are ignored, manual guard commands are analyzed, the obsolete diagram retires safely, and Notion page deletion uses the current wire field.
2. A fresh or updated project can run the shipped `inquisition` workflow without relying on hand-authored inquisitor artifacts. The canonical inquisitor role template compiles for both Claude and Codex.
3. `dydo sync` removes only known retired framework-role files when that role is absent from the discovered template set. It must not become a generic output-directory cleaner, must preserve user-added sibling files, and must preserve an explicit project-local `mode-sprint-auditor.template.md`.
4. Shipped writing guidance no longer claims that the removed `must-read: true` frontmatter convention is enforced.
5. The full test and coverage ratchet passes, `dydo check` reports no errors, both package manifests say 2.2.3, and tag `v2.2.3` is pushed so the existing release workflow can publish GitHub, NuGet, and npm artifacts.

Locked decisions:

- The legacy `integrations` compatibility rule remains: if neither Claude nor Codex is recorded, `dydo sync` emits both formats.
- Retired-role cleanup is allowlisted to `sprint-auditor` and runs only when no current role with that name is discovered.
- The compiler-owned generated filenames are deleted regardless of content: `.claude/agents/sprint-auditor.md`, `.claude/skills/sprint-auditor/SKILL.md`, `.codex/agents/sprint-auditor.toml`, and `.agents/skills/sprint-auditor/SKILL.md`. These are the same files an active role overwrites on every sync. Parent directories are removed only when empty; user-added sibling files and directories survive.
- Issue 0307 (spine hardening) is explicitly outside this patch release.
- LC is explicitly outside this sprint. Its agents finish and commit before the separate template-update/sync/migration pass.
- Release publication uses the existing tag-triggered GitHub Actions workflow; no local registry publication is added.

## Slice Map

| Slice | Blocked by | Files touched | Exact gate |
|---|---|---|---|
| [v223-1-pending-patch](../slices/v223-1-pending-patch.md) | — | Non-sync production/tests for 0300-0305, command docs, diagram retirement, config | Pending-patch focused `run_tests.py --filter` command |
| [v223-2-upgrade-compatibility](../slices/v223-2-upgrade-compatibility.md) | v223-1 | `SyncCommand`, sync/role tests, inquisitor template/artifacts, writing docs | Compatibility-focused `run_tests.py --filter` + source-built sync twice |
| [v223-3-release](../slices/v223-3-release.md) | v223-2 | Package manifests, issue/sprint records, indexes | Full runner + forced gap check + source-built check/build/package smoke |

## Ordering and Ownership

Execute serially in the main working tree. The pending patch is already present there and includes staged asset deletions, so creating parallel worktrees would separate related state and make ownership ambiguous. Preserve all pre-existing edits; do not reset, re-stage wholesale, or rewrite unrelated records.

### Release-owned paths

Only these paths may be staged for v2.2.3:

- Production: `Commands/{GuardCommand,HelpCommand,InitCommand,SyncCommand,TemplateCommand}.cs`, `Services/{CompletionProvider,TemplateGenerator}.cs`, `Sync/Notion/Dtos/NotionPageUpdateRequestConverter.cs`
- Tests: `DynaDocs.Tests/Commands/{CompleteCommandTests,SyncCommandTests}.cs`, `DynaDocs.Tests/Integration/{GuardIntegrationTests,InitCheckIntegrationTests,InitCommandTests,TemplateCommandTests,TemplateOverrideTests}.cs`, `DynaDocs.Tests/Services/{FolderScaffolderTests,RoleDefinitionServiceTests,TemplateGeneratorTests}.cs`, `DynaDocs.Tests/Sync/Notion/Live/NotionLiveTestBase.cs`, and `DynaDocs.Tests/Sync/Notion/NotionClientTests.cs`
- Shipped sources/docs: `Templates/dydo-commands.template.md`, `Templates/writing-docs.template.md`, `Templates/mode-inquisitor.template.md`, the deleted `Templates/Assets/dydo-diagram.svg`, `dydo/reference/dydo-commands.md`, `dydo/reference/writing-docs.md`, `dydo/understand/templates-and-customization.md`, the deleted `dydo/_assets/dydo-diagram.svg`, `.claude/agents/inquisitor.md`, `.claude/skills/inquisitor/SKILL.md`, `.codex/agents/inquisitor.toml`, and `.agents/skills/inquisitor/SKILL.md`
- Configuration/package: `dydo.json`, `DynaDocs.csproj`, `npm/package.json`
- Project records: issues 0300-0307, `dydo/project/issues/{_index.md,resolved/_index.md}`, `dydo/project/slices/_index.md`, `dydo/project/sprints/_index.md`, this sprint root, and its three slices: `v223-1-pending-patch.md`, `v223-2-upgrade-compatibility.md`, and `v223-3-release.md`

The existing changes to `dydo/project/tasks/*.md` are unrelated Notion round-trip normalization and remain untouched and unstaged. No `git add -A`, `git add .`, blanket formatter, reset, checkout, or stash operation is permitted.

## Prior Art

- Issues 0300-0305 contain the reproduction and intended fixes already present in the dirty tree.
- Issue 0306 records the recovered stale-spine incident; issue 0307 is its explicitly deferred hardening follow-up.
- Decision 041/native-runtime work retired sprint-auditor and originally removed inquisitor; the current `Templates/workflow-inquisition.js` now dispatches `agentType: 'inquisitor'`, while the repository carries hand-authored inquisitor artifacts that the compiler cannot reproduce.
- `RoleDefinitionService.DiscoverRoles` treats a project-local mode template as authoritative, which provides the ownership test for preserving a customized sprint-auditor.
- `.github/workflows/release.yml` is the only publication path: a `v*` tag builds five binaries, creates a GitHub release, pushes NuGet, then publishes npm.

## Design

The compatibility change stays at the compiler boundary. Inquisitor becomes a normal shipped read-only worker role, sourced from one mode template. Retired-role cleanup is a small reconciliation step after role discovery: if `sprint-auditor` is absent, delete only its four compiler-owned filenames and prune only newly empty parent directories. If a project-local template reintroduces the role, normal compilation runs and cleanup does nothing.

The existing 0300-0305 patch remains separate in intent even though it lands in the same patch release. Its backward-compatibility rules are pinned by tests, and no implementation from issue 0307 is admitted.

## Watch-outs and Rollback

- The working tree is intentionally dirty with unrelated task-record changes. Release validation uses `git diff --cached --check`, `git diff --cached --name-only`, and an explicit comparison to the allowlist; a dirty unstaged tree is acceptable.
- `DynaDocs.Tests/coverage/run_tests.py` creates a detached worktree and copies dirty/untracked files into it, so pre-commit runs do exercise the current patch.
- Running installed `dydo` would exercise 2.2.2. All compiler/check validation before tagging uses `dotnet run --project DynaDocs.csproj -- ...`.
- Pushing `v2.2.3` is irreversible once any registry accepts it. Before tag creation/push, verify branch/commit, remote tag absence, GitHub authentication, and all local gates. If the workflow fails without source changes, diagnose and rerun the failed jobs against the same tag. If a source correction is needed after any artifact publishes, leave v2.2.3 immutable and prepare v2.2.4; never move or replace the tag.

## Plan Review

Round 1 FAIL (2026-07-26): six findings — missing plan sections/status, underspecified commands/files, unsafe directory cleanup, dirty-tree staging ambiguity, inquisitor underspecification, and release preflight/rollback gaps.

Round 2 FAIL (2026-07-26): lifecycle states were wrong and the initial implementation slices collided on sync production/tests.

Round 3 FAIL (2026-07-26): the combined implementation slice was too broad and still overlapped release bookkeeping on issue records. Remediated with three file-disjoint slices: non-sync pending fixes, compiler compatibility, then records/package/release.

Round 4 FAIL (2026-07-26): staging allowlist still said two slices after the split. Corrected by enumerating all three slice paths.

Round 5 PASS (2026-07-26): mechanically executable, file-disjoint, migration-safe, preserves unrelated changes, and defines complete validation, staging, publication, and immutable-release handling.
