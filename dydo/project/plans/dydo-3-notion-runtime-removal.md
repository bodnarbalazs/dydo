---
title: dydo 3.0 Notion Runtime Removal and Release
status: active
area: project
type: context
linear-project: https://linear.app/bodnar-balazs/project/dydo-30-remove-notion-runtime-and-release-54b8939d748e
---

# dydo 3.0 Notion Runtime Removal and Release

Delivery plan for removing the local Notion projection and its now-consumerless sync machinery,
closing every command/config/template/test/release seam, and integrating the result into dydo 3.0.0.

## 1. Specification

### Intent

Delete the Notion product boundary rather than adapting it to Linear. Linear remains the official live
PM surface and Git remains the durable knowledge/proof surface under [DR 044](../decisions/044-linear-canonical-pm-and-dydo-knowledge-boundary.md).
The removal must leave no executable, configurable, packaged, documented, generated, or tested route
back into the old projection while preserving remote Notion data and exact recoverable Git evidence.

The human explicitly authorized immediate local Notion deletion on 2026-08-27: “You can freely delete
the notion stuff as well.” This waives the earlier Project 4 dogfood-before-deletion sequencing policy
for source removal only. Project 4's explicit human acceptance remains a hard prerequisite for the
`v3.0.0` tag and publication. The authorization does not permit deleting or archiving remote Notion
content, reading/revealing secrets, absorbing unrelated working-tree edits, or publishing before the
integrated gates pass.

### In scope

- Stop and permanently remove the Notion watchdog, all start paths, and its Notion-only logging.
- Remove `dydo notion`, its token/vault/connect/reveal/reset/provision/sync behavior, and all Notion
  transport/DTO/provider code.
- Remove the generic reconciliation/projection/model machinery after proving that Notion was its last
  production consumer.
- Remove Notion config and source-generated serialization contracts, the committed sync-model/template,
  the NSec dependency, and every test whose subject disappears.
- Decouple surviving model-cap restoration, guard, template, completion, help, folder-scaffold, and
  configuration behavior from the deleted runtime.
- Remove or rewrite active docs, built-in templates, installed framework docs, package README content,
  release workflow gates, and tests that advertise or require Notion/watchdog/sync-model behavior.
- Preserve the remote Notion workspace unchanged and preserve the exact pre-deletion source/corpus in
  Git plus the bounded local rollback stores without reading their contents.
- Integrate with the Linear-native ontology/corpus work, update version/package/release guidance, run an
  integrated audit, and release `3.0.0` only from the accepted exact commit/tag.

### Out of scope

- Deleting, archiving, modifying, exporting, or provisioning any remote Notion object.
- Building a Linear adapter, sync daemon, local Linear schema, token store, or Markdown mirror.
- Importing PM history into Linear or deciding FutureFeature promotion. FutureFeature remains repo-native.
- Deleting accepted historical Decisions, changelog, migration evidence, or the pre-deletion Git tag
  merely because they mention Notion.
- Cleaning `_system/.local/notion*`, `_system/notion.vault`, `_system/notion_sync_spine/`, or
  `_system/notion_sync/` during implementation. They are rollback evidence, potentially secret-bearing,
  and outside the source-removal lanes.
- Opportunistically changing unrelated dirty files or completing the broader ontology/corpus migration
  in this plan.

### Acceptance criteria

1. The remote Notion workspace remains unchanged. This plan runs only
   `dydo notion sync --docs --dry-run`; it never runs live reconcile, reset, provision, archive, delete,
   or mass-delete. Every proposed remote write is recorded as an unresolved freeze exception and deferred
   to separately planned, instrumented work rather than authorized inside this removal delivery.
2. Before the first source deletion, the watchdog is stopped, the final v2 state is recorded at one
   pushed exact commit, and annotated `pm-v2-final` resolves locally and remotely to that commit. The
   freeze artifact records the read-only two-projection dry-run, every proposed operation, and every
   deliberate exception. Every main-checkout tracked/untracked dirty row is included or excluded by exact
   path and blob hash; the isolated freeze worktree is clean. Active tag ruleset
   `protect-pm-v2-final` exactly targets `refs/tags/pm-v2-final`, blocks update and deletion, and has no
   bypass actor, proven by stored API read-back.
3. `Program.cs`, command help, completion data, generated command reference, and smoke/consistency tests
   expose neither `notion` nor `watchdog`. `dydo sync` remains the native role/skill compiler; it is not
   confused with or removed as part of the external-data sync engine.
4. No guard or surviving service can start a background Notion process. Expired model-cap restoration
   remains functional through the guard and has no dependency on `WatchdogLogger`.
5. All 97 tracked files under `Sync/Notion/**`, all 45 tracked files under `Sync/**` outside that folder,
   and their orphaned models/serialization contexts are gone. A post-removal symbol/reference scan proves
   no production consumer of `ISyncAdapter`, `SyncRunner`, `ReconcileEngine`, `SyncDoc`, `SyncField`,
   `SyncModel`, or the projection types remains.
6. `Models/NotionConfig.cs`, the `DydoConfig.Notion` property, all Notion/sync snapshot AOT registrations,
   `Templates/sync-model.template.json`, `dydo/_system/sync-model.json`, and the template-update special
   case/hash are gone. Existing 2.x configs containing unknown `notion` data are safely ignored, with a
   migration note telling users when they may remove it manually.
7. `NSec.Cryptography` is absent from `DynaDocs.csproj` and the restore dependency graph. No replacement
   cryptography or secret-storage code is introduced.
8. All 52 tracked tests under `DynaDocs.Tests/Sync/Notion/**`, all generic-sync-only tests, and the Notion/
   watchdog command/service tests are removed. Surviving cross-cutting tests are rewritten to assert the
   3.0 command, config, template, scaffold, help, completion, and release contracts rather than merely
   weakened or deleted.
9. Active source docs, installed docs, built-in templates, README, and npm README describe Linear-native
   live PM and contain no instruction to configure or run Notion. Historical DR/changelog/freeze/migration
   evidence may name Notion only as explicitly historical material.
10. `.github/workflows/release.yml` has no Notion fixture or fidelity job/filter and retains a valid build
    dependency graph. Release workflow tests prove the replacement graph.
11. Focused tests, full tests, coverage gap verification, `dydo check`, template/install consistency,
    Native AOT publish, NuGet pack, npm dry-run pack, and the integrated audit all pass.
12. Product/package versions and release notes are `3.0.0`; the release commit and protected annotated
    `v3.0.0` tag are exact and pushed only after all other criteria pass and Project 4 records explicit
    human acceptance of the dogfooded operating model. Active ruleset `protect-v3.0.0` targets exactly
    `refs/tags/v3.0.0`, blocks update and deletion, has no bypass actor, and is proven by full API read-back
    before the tag is pushed.

### Questions and answers

- **Does the authorization allow us to skip the old Project 4 dependency?** Only for local source
  deletion. The human explicitly accelerated removal, but Project 4's recorded human acceptance still
  blocks the `v3.0.0` tag and every publication channel.
- **Can we delete remote Notion data now?** No. DR 044 and this contract preserve it as external rollback
  evidence; this plan never needs to mutate it.
- **Must the last reconcile happen?** No live reconcile runs in this plan. Run
  `dydo notion sync --docs --dry-run` with the frozen 2.2.9 implementation, record every proposed local
  and remote operation, and freeze Git. The old command cannot prove ordered remote writes or before/after
  digests strongly enough to make a live mutation safe. Any desired remote reconciliation becomes
  separate instrumented work with its own reviewed scope and authorization.
- **Does `dydo sync` disappear?** No. `Commands/SyncCommand.cs` compiles native roles, skills, hooks, and
  workflows. It shares a noun with the deleted engine but is an unrelated surviving product command.
- **Does `dydo model` disappear with the watchdog?** No. Model cap/status/uncap remains. Its already-present
  guard-throttled expiry path survives; only the WatchdogLogger call and watchdog wording are removed.
- **Do we erase every textual occurrence of Notion?** No. Historical Decisions, changelog, the freeze,
  the disposition manifest, and 3.0 migration/release guidance retain necessary provenance. Active
  product claims, instructions, templates, tests, and release gates do not.
- **What happens to local token/snapshot state?** Nothing in this delivery. It is not read, migrated, or
  deleted. Once 3.0 is accepted, a separate human-authorized cleanup may remove it using exact paths.

## 2. Prior art and discovery evidence

- [DR 044](../decisions/044-linear-canonical-pm-and-dydo-knowledge-boundary.md) requires official Linear
  surfaces and explicitly rejects rebuilding the Notion adapter, polling watchdog, Markdown-body mirror,
  or permanent migration machinery.
- [dydo 3.0 Linear PM Migration](./dydo-3-linear-migration.md) defines the five-Project boundary, remote
  Notion preservation, `pm-v2-final`, and 3.0 release outcome. Its Project-4-before-source-deletion rule
  is waived by the acceleration above; its Project-4 human-acceptance release boundary remains binding.
- The completed read-only surface audit and local verification found 97 production files under
  `Sync/Notion/**` (including 62 DTOs), 45 generic files under `Sync/**`, and 52 tests under
  `DynaDocs.Tests/Sync/Notion/**`. The generic engine has no production provider other than Notion.
- `Program.cs` registers `NotionCommand` and `WatchdogCommand`; `GuardCommand` calls watchdog auto-start;
  `ModelCapService` uses `WatchdogLogger` for one otherwise-independent expiry trace.
- Notion configuration/AOT/package roots are `Models/NotionConfig.cs`, `Models/DydoConfig.cs`,
  `Serialization/NotionJsonContext.cs`, `Serialization/SyncModelJsonContext.cs`,
  `Serialization/DydoJsonContext.cs`, and `DynaDocs.csproj` (`NSec.Cryptography`).
- Template/config roots are `Templates/sync-model.template.json`, `dydo/_system/sync-model.json`,
  `Commands/TemplateCommand.cs`, `Services/TemplateGenerator.cs`, and `dydo.json`.
- The release workflow has a `newline-fidelity` job tied to a Notion fixture and Notion/projection tests;
  the `build` job currently depends on it. Removal must repair the job graph, not only delete test names.
- The repository is already dirty. In particular `dydo.json` and several legacy PM records have
  pre-existing edits, and new test-runner files are untracked. They are neither migration input nor
  disposable state for this Project.

## 3. Design and exact deletion boundary

### Provider, daemon, and command surface

Delete:

- `Sync/Notion/**` (all 97 tracked provider, provisioning, token/vault, docs-mirror, transport, DTO, and
  state files).
- `Commands/NotionCommand.cs`, `Commands/WatchdogCommand.cs`.
- `Services/WatchdogService.cs`, `Services/WatchdogLogger.cs`.
- `Models/NotionConfig.cs`.
- `Serialization/NotionJsonContext.cs`.

Edit surviving hot files:

- `Program.cs`: remove only the Notion and watchdog command registrations.
- `Commands/GuardCommand.cs`: remove `AutoStartWatchdogIfDue`, its throttle/method, and the call from the
  decision path; retain `RestoreExpiredModelCapsIfDue`.
- `Commands/ModelCommand.cs`: replace watchdog-specific status/help wording with guard-driven expiry
  wording; retain cap/status/uncap behavior.
- `Commands/TaskCommand.cs`: remove the obsolete distinction from the Notion-synced Slice board. Its
  surviving 3.0 command disposition comes from Project 2; this lane makes no independent ontology choice.
- `Services/ModelCapService.cs`: remove watchdog terminology and replace the logging dependency with the
  smallest command-neutral behavior (no new daemon/logger subsystem). Preserve expiry, config save,
  marker cleanup, and one recompile.
- `Commands/HelpCommand.cs`, `Services/CompletionProvider.cs`, `Services/ConfigFactory.cs`: remove stale
  command/help/nudge vocabulary while preserving unrelated commands and safety rules.
- `Utils/PathUtils.Discovery.cs` and `Services/OffLimitsService.cs`: remove watchdog-specific comments
  without changing main-worktree discovery or the `_system/**` security boundary.
- `Models/DydoConfig.cs`, `Serialization/DydoJsonContext.cs`: this same provider-removal lane owns the
  `DydoConfig.Notion` removal and serial removal of provider-specific registrations. It must leave the
  still-compiling generic snapshot registrations intact until the generic-engine lane owns their removal.

### Consumerless generic sync surface

After the provider/command and sync-model-decoupling lanes both compile and prove no surviving production
reference, delete:

- all 45 tracked files under `Sync/**` other than `Sync/Notion/**`, including `Sync/Model/**` and
  `Sync/Projection/**`;
- `Models/SyncDoc.cs`, `Models/SyncField.cs`;
- `Serialization/SyncModelJsonContext.cs`;
- the sync-snapshot registrations/context block in `Serialization/DydoJsonContext.cs`.

Do not delete `Commands/SyncCommand.cs`; it is the native artifact compiler. `Utils/FrontmatterParser.cs`
survives; the generic-engine lane owns its comment cleanup and the matching
`DynaDocs.Tests/Utils/FrontmatterParserTests.cs` comment cleanup so deleted `SyncDocFile`/merge behavior is
no longer named as an authority. The same lane edits `DynaDocs.Tests/coverage/tier_registry.json` to
remove the five deleted Sync module entries while preserving the surviving FrontmatterParser and
TitlePrettifier tiers. Local search found no other tracked coverage registry/cache companion naming those
paths.

### Templates, config, and packaging

Delete:

- `Templates/sync-model.template.json`;
- `dydo/_system/sync-model.json`;
- the `NSec.Cryptography` package reference and vault-only comment from `DynaDocs.csproj`.

Edit:

- `Commands/TemplateCommand.cs`: remove `_system/sync-model.json` from framework-generated files,
  special lookup/materialization, and hash flow.
- `Services/TemplateGenerator.cs`, `Templates/about-dynadocs.template.md`,
  `Templates/dydo-commands.template.md`: remove generated Notion/watchdog claims in the
  sync-model-decoupling lane; the documentation lane later applies the reviewed Linear/Git prose.
- `dydo.json`: remove only the `notion` object, watchdog-only nudge alternatives, and the
  `_system/sync-model.json` framework hash. This is a dirty serial-integration file; preserve all
  unrelated current changes byte-for-byte.
- `Services/FolderScaffolder.cs`: remove Notion-spine descriptions and its sync-model dependency before
  `Sync/Model/**` is deleted. Apply the Project-2-approved 3.0 folder contract rather than inventing an
  ontology in this lane.

### Test deletion and retained contract coverage

Delete disappearing-subject tests:

- `DynaDocs.Tests/Sync/Notion/**` (all 52 tracked files and fixture content);
- all tracked tests directly under `DynaDocs.Tests/Sync/**`, including `Sync/Model/**` and
  `Sync/Projection/**`;
- `DynaDocs.Tests/Commands/NotionCommandTests.cs`;
- `DynaDocs.Tests/Commands/WatchdogCommandTests.cs`;
- `DynaDocs.Tests/Services/WatchdogServiceTests.cs`;
- `DynaDocs.Tests/Services/WatchdogLoggerTests.cs`;
- `DynaDocs.Tests/Services/WatchdogAutoStartTests.cs`;
- `DynaDocs.Tests/Models/SyncDocTests.cs`;
- the Notion fixture `<Content Include>` in `DynaDocs.Tests/DynaDocs.Tests.csproj`.

Edit, do not delete, cross-cutting tests that still guard surviving behavior:

- `DynaDocs.Tests/Commands/CommandSmokeTests.cs` and
  `DynaDocs.Tests/Commands/CommandDocConsistencyTests.cs` assert the 3.0 command set and absence of
  `notion`/`watchdog`.
- `DynaDocs.Tests/Services/ModelCapServiceTests.cs` proves expiry still works without watchdog logging.
- `DynaDocs.Tests/Integration/IntegrationTestBase.cs` removes the watchdog spawn override and its remote
  mutation warning after the start path is gone.
- `DynaDocs.Tests/Integration/GuardIntegrationTests.cs` removes the watchdog command case while retaining
  shell/guard coverage. `DynaDocs.Tests/Integration/IssueTests.cs` and
  `DynaDocs.Tests/Integration/TaskTests.cs` retain their title/frontmatter assertions but rename them and
  remove Notion-specific rationale.
- `DynaDocs.Tests/Services/TemplateUpdateTests.cs`, `FolderScaffolderTests.cs`,
  `CompletionProviderTests.cs`, and config/template generator/integration tests prove the removed
  sync-model/config/command entries do not regenerate.
- `DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs` proves a valid workflow graph without the
  Notion newline/fidelity gate.
- `DynaDocs.Tests/Utils/FrontmatterParserTests.cs` remains; only stale explanatory coupling is removed.

### Documentation and release surface

Delete obsolete active references:

- `dydo/reference/notion-sync.md`;
- `dydo/reference/notion-oss-survey.md`; remove both deleted references from
  `dydo/reference/_index.md`. Historical recovery remains available at `pm-v2-final`.

Rewrite active sources and their generated/template counterparts together:

- `README.md` and `npm/README.md`;
- `dydo/reference/about-dynadocs.md`, `dydo/reference/dydo-commands.md`;
- `dydo/understand/about.md`, `dydo/understand/architecture.md`, `dydo/understand/work-model.md`;
- `dydo/guides/orchestration-pitfalls.md`;
- `Templates/about-dynadocs.template.md` and `Templates/dydo-commands.template.md`.

Those are the complete Project-5 template paths. If the reviewed Project 2 plan assigns another template
that must change for Notion removal, Issue 5 stays blocked until this plan receives a reviewed amendment
naming that exact path and its landing order; there is no “any Project-2-owned template” catch-all.

Retain `dydo/guides/migrating-dydo-1x-to-2x.md` with `status: historical`, and create the exact
Project-5-owned `dydo/guides/migrating-dydo-2x-to-3x.md` and
`dydo/project/migrations/dydo-3-main-project-adoption.md`. Do not rewrite accepted historical Decisions
or changelog to pretend Notion never existed.

Edit `.github/workflows/release.yml`: remove the Notion fixture checkout and fidelity-filter job,
remove/replace `build.needs: newline-fidelity`, retain the five-runtime AOT build matrix, and keep release,
NuGet, and npm dependencies valid. Update `DynaDocs.csproj` (`Version` and `PackageReleaseNotes`),
`npm/package.json`, new
`dydo/project/changelog/2026/2026-08-27/dydo-3-0-0-linear-pm-and-notion-runtime-removal.md`,
`dydo/guides/migrating-dydo-2x-to-3x.md`, and
`dydo/project/migrations/dydo-3-main-project-adoption.md` to 3.0.0 only in the final serial integration
Issue.

### Preserved rollback boundary

The following are not implementation inputs and must not be opened, echoed, deleted, reset, or added to
Git by a removal lane:

- `dydo/_system/.local/notion*` and other Notion/watchdog local marker/token files;
- `dydo/_system/notion.vault`;
- `dydo/_system/notion_sync_spine/`;
- `dydo/_system/notion_sync/`.

Remote Notion data remains unchanged; the dry-run transcript records divergence without applying it. The
authoritative rollback is the exact pushed commit recorded in
`dydo/project/migrations/3.0-notion-freeze.md`; annotated `pm-v2-final` is its navigable alias. Local stores
are secondary forensic evidence and remain off-limits even after their readers are deleted.

## 4. Linear Issue and lane map

Each row becomes one reviewed Linear Issue under this plan's Project. Paths are disjoint except the
explicit serial integration Issue; workers must use isolated worktrees from the recorded governing
commit and never stage unowned files.

| # | Issue / lane | Exact ownership | Depends on | Gate |
|---|---|---|---|---|
| 1 | Freeze and seal the pre-deletion baseline | `dydo/project/migrations/3.0-notion-freeze.md`; `dydo/project/migrations/3.0-pm-records.json` record-row `target`/`evidence` freeze values only; `dydo/project/migrations/3.0-pm-records.md` `Freeze evidence` section only; `dydo/project/migrations/3.0-pm-v2-final-ruleset.request.json`; `dydo/project/migrations/3.0-pm-v2-final-ruleset.readback.json` | published reviewed plan | read-only dry-run boundary, stopped daemon, bounded/no live mutation, clean/excluded-path ledger, pushed exact SHA, protected `pm-v2-final` match |
| 2 | Remove provider, daemon, config, and command seams | `Sync/Notion/**`; `DynaDocs.Tests/Sync/Notion/**`; `Commands/NotionCommand.cs`; `Commands/WatchdogCommand.cs`; `Services/WatchdogService.cs`; `Services/WatchdogLogger.cs`; `Models/NotionConfig.cs`; `Models/DydoConfig.cs`; first serial edit of `Serialization/DydoJsonContext.cs`; `Serialization/NotionJsonContext.cs`; `DynaDocs.csproj`; `Program.cs`; `Commands/GuardCommand.cs`; `Commands/HelpCommand.cs`; `Commands/ModelCommand.cs`; `Commands/TaskCommand.cs`; `Services/ModelCapService.cs`; `Services/CompletionProvider.cs`; `Services/ConfigFactory.cs`; `Utils/PathUtils.Discovery.cs`; `Services/OffLimitsService.cs`; `DynaDocs.Tests/DynaDocs.Tests.csproj`; `DynaDocs.Tests/Commands/NotionCommandTests.cs`; `DynaDocs.Tests/Commands/WatchdogCommandTests.cs`; `DynaDocs.Tests/Commands/CommandSmokeTests.cs`; `DynaDocs.Tests/Commands/CommandDocConsistencyTests.cs`; `DynaDocs.Tests/Services/WatchdogServiceTests.cs`; `DynaDocs.Tests/Services/WatchdogLoggerTests.cs`; `DynaDocs.Tests/Services/WatchdogAutoStartTests.cs`; `DynaDocs.Tests/Services/CompletionProviderTests.cs`; `DynaDocs.Tests/Services/ConfigFactoryTests.cs`; `DynaDocs.Tests/Services/ModelCapServiceTests.cs`; `DynaDocs.Tests/Commands/ModelCommandTests.cs`; `DynaDocs.Tests/Integration/IntegrationTestBase.cs`; `DynaDocs.Tests/Integration/GuardIntegrationTests.cs`; `DynaDocs.Tests/Integration/IssueTests.cs`; `DynaDocs.Tests/Integration/TaskTests.cs` | 1 | exact lane-2 build/filter and no executable start/config route |
| 3 | Decouple template, scaffold, config file, and sync model | `Commands/TemplateCommand.cs`; first serial edit of `Services/TemplateGenerator.cs`; `Services/FolderScaffolder.cs`; `Templates/sync-model.template.json`; `dydo/_system/sync-model.json`; serial `dydo.json` merge; `DynaDocs.Tests/Services/TemplateUpdateTests.cs`; `DynaDocs.Tests/Services/TemplateGeneratorTests.cs`; `DynaDocs.Tests/Services/FolderScaffolderTests.cs`; `DynaDocs.Tests/Integration/TemplateCommandTests.cs`; `DynaDocs.Tests/Integration/TemplateOverrideTests.cs`; `DynaDocs.Tests/Services/ConfigServiceTests.cs` | 2; reviewed `dydo/project/plans/dydo-3-linear-native-work-model.md` at its published PASS commit and [Linear Project 2](https://linear.app/bodnar-balazs/project/dydo-30-adopt-linear-native-work-model-8145ca3f78ad) resource | exact lane-3 build/filter, `dydo template update --diff`, no sync-model regeneration |
| 4 | Remove consumerless generic engine and its tests | non-Notion `Sync/**`; all `DynaDocs.Tests/Sync/**`; `Models/SyncDoc.cs`; `Models/SyncField.cs`; `DynaDocs.Tests/Models/SyncDocTests.cs`; `Serialization/SyncModelJsonContext.cs`; second serial edit of `Serialization/DydoJsonContext.cs`; `Utils/FrontmatterParser.cs`; `DynaDocs.Tests/Utils/FrontmatterParserTests.cs`; `DynaDocs.Tests/coverage/tier_registry.json` | 3 consumer scan | exact lane-4 build/filter, zero generic-engine production/test/coverage consumers |
| 5 | Rewrite active docs and built-in templates | `README.md`; `npm/README.md`; `dydo/reference/_index.md`; `dydo/reference/about-dynadocs.md`; `dydo/reference/dydo-commands.md`; deletion of `dydo/reference/notion-sync.md`; deletion of `dydo/reference/notion-oss-survey.md`; `dydo/understand/about.md`; `dydo/understand/architecture.md`; `dydo/understand/work-model.md`; `dydo/guides/orchestration-pitfalls.md`; `dydo/guides/migrating-dydo-1x-to-2x.md`; new `dydo/guides/migrating-dydo-2x-to-3x.md`; new `dydo/project/migrations/dydo-3-main-project-adoption.md`; `Templates/about-dynadocs.template.md`; `Templates/dydo-commands.template.md`; second serial edit of `Services/TemplateGenerator.cs`; `DynaDocs.Tests/Commands/CommandDocConsistencyTests.cs`; `DynaDocs.Tests/Services/TemplateGeneratorTests.cs`; `DynaDocs.Tests/Services/TemplateUpdateTests.cs` | 3; reviewed `dydo/project/plans/dydo-3-linear-native-work-model.md` at its published PASS commit and [Linear Project 2](https://linear.app/bodnar-balazs/project/dydo-30-adopt-linear-native-work-model-8145ca3f78ad) resource | exact lane-5 docs/template checks, links, `dydo check`, executable active-surface scan |
| 6 | Repair release gates and integrate 3.0.0 | `.github/workflows/release.yml`; `DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`; final version/`PackageReleaseNotes` edit of `DynaDocs.csproj`; `npm/package.json`; new `dydo/project/changelog/2026/2026-08-27/dydo-3-0-0-linear-pm-and-notion-runtime-removal.md`; final release section in `dydo/guides/migrating-dydo-2x-to-3x.md`; final release section in `dydo/project/migrations/dydo-3-main-project-adoption.md`; new `dydo/project/migrations/3.0-v3-release-ruleset.request.json`; new post-tag `dydo/project/migrations/3.0-v3-release-ruleset.readback.json` | 2–5; Projects 2–3 integrated; Project 4 explicit human acceptance before ruleset/tag/publication | exact lane-6 filter, full/AOT/pack/docs/audit gates, protected exact `v3.0.0` tag |

Issue 2 begins after Issue 1. Issue 3 remains blocked until Issue 2 and the exact reviewed Project 2 plan
gate pass. Issue 2 owns the complete provider deletion, matching provider tests,
`Models/DydoConfig.cs`, and the first serial `Serialization/DydoJsonContext.cs` edit, so its commit
compiles. Issue 3 then removes every runtime and template consumer of `Sync/Model/**` while that model
still exists, and its commit compiles. Only then does Issue 4 delete the generic engine, its tests, its
AOT registrations, and its coverage registry rows. Issue 5 follows Issue 3 and its Project 2 plan gate.
`Serialization/DydoJsonContext.cs`, `Services/TemplateGenerator.cs`, `DynaDocs.csproj`, and `dydo.json`
are named serial merge points; no two worktrees edit the same revision of those files concurrently.

“Reviewed Project 2 shape” is an executable prerequisite, not a future placeholder. Before Issues 3 or
5 enters `Todo`, `dydo/project/plans/dydo-3-linear-native-work-model.md` must exist with `status: active`
and a recorded plan-review PASS, carry the Linear Project 2 URL above, be pushed at an exact commit, and
be attached as a Project 2 resource. Each dependent Issue records that commit SHA and permalink in
`Governing context`; a missing file, active status, PASS, SHA, or Linear resource leaves the Issue blocked.

### Shared hot-file landing order

No shared path is resolved ad hoc. The integrator lands each exact sequence below and reruns the later
Issue's focused gate after its edit:

| Shared path | First landing | Second landing |
|---|---|---|
| `Serialization/DydoJsonContext.cs` | Issue 2 removes Notion/provider registrations | Issue 4 removes generic sync snapshot registrations/context |
| `DynaDocs.csproj` | Issue 2 removes `NSec.Cryptography` | Issue 6 sets 3.0.0 and `PackageReleaseNotes` |
| `Services/TemplateGenerator.cs` | Issue 3 removes sync-model/Notion/watchdog generated literals | Issue 5 writes final reviewed Linear/Git product prose |
| `DynaDocs.Tests/Commands/CommandDocConsistencyTests.cs` | Issue 2 removes deleted command expectations | Issue 5 proves final generated command docs |
| `DynaDocs.Tests/Services/TemplateGeneratorTests.cs` | Issue 3 removes sync-model/Notion expectations | Issue 5 updates final product-prose expectations |
| `DynaDocs.Tests/Services/TemplateUpdateTests.cs` | Issue 3 removes sync-model generation/hash cases | Issue 5 proves final source/generated template parity |
| `dydo/guides/migrating-dydo-2x-to-3x.md` | Issue 5 creates the migration guide | Issue 6 adds final 3.0.0 release/tag/package observables |
| `dydo/project/migrations/dydo-3-main-project-adoption.md` | Issue 5 creates the adoption playbook | Issue 6 adds final released-version observables |
| `dydo.json` | its current dirty-tree owner lands or records the existing edit | Issue 3 removes only `notion`, watchdog nudge alternatives, and the sync-model hash |
| `dydo/project/migrations/3.0-pm-records.json` | Project 1 disposition owner lands the ratified manifest | Issue 1 adds only exact freeze targets/evidence after tagging |
| `dydo/project/migrations/3.0-pm-records.md` | Project 1 disposition owner lands the ratified review surface | Issue 1 adds only `Freeze evidence` after tagging |

Project 2 must land before Issues 3 and 5. Its reviewed plan must contain an `Owned path overlap with
Project 5` table. Every overlap with this plan's exact Issue-3/5 paths must say `Project 2 first, Project
5 second`; any new path or different order requires reviewed amendments to both plans before either Issue
enters `Todo`.

## 5. Gates

### Pre-deletion freeze gate

1. Stop the installed/current 2.2.9 daemon with `dydo watchdog stop` and verify no tracked PID remains
   active. Do not run `reset` or `--allow-mass-delete`.
2. Run `dydo notion sync --docs --dry-run` first under the existing credential resolution and capture
   the complete planned-operation summary. Record every proposed local and remote create, update, archive,
   delete, provision, relation/schema, or page-body operation as a freeze exception. Do not run
   `dydo notion sync --docs` live even when the dry-run reports zero writes: 2.2.9 lacks the instrumentation
   needed to prove absence of race-time remote mutations. Never reveal or print a token/passphrase.
3. Freeze Git from the attributed local tree. A credential failure, dry-run failure, or non-empty proposed
   operation set is recorded verbatim as an exception and does not broaden scope. Any future remote
   reconciliation is blocked on a separate reviewed plan/Issue that specifies instrumented operation IDs,
   before/after digests, authorization, and rollback; it is not a continuation of this Issue.
4. Capture pending-write and shadow-state counts in the Linear Issue/session transcript by safe
   metadata/path inventory only; do not print secret or body contents. Exact-SHA evidence is written to
   the freeze artifact only after the freeze commit exists; do not create a self-referential placeholder
   commit.
5. Before inventory, write the ruleset request JSON exactly as specified in step 6. Then run
   `git status --porcelain=v1 --untracked-files=all` in the main checkout and capture every row plus the
   following inclusion decision/hash in the Linear Issue/session transcript. Every dirty tracked or
   untracked status row is individually exactly one of:

   - **included** — named explicitly, staged into the freeze commit, with its index blob from
     `git rev-parse :<path>` captured before commit and verified equal to
     `git rev-parse <freeze-sha>:<path>` afterward; or
   - **excluded** — named explicitly with status code, owner/reason, the current working-copy blob from
     `git hash-object --no-filters -- <path>`, and the current HEAD blob from
     `git rev-parse HEAD:<path>` when tracked. A working-tree deletion records literal `<absent>` plus the
     HEAD blob. No wildcard/group-only exclusion is valid.

   Expand untracked directories with `--untracked-files=all`. Run
   `git ls-files --others --exclude-standard -- dydo/project/migrations` and explicitly include or exclude
   every migration artifact. At minimum both current PM-record manifests and the ruleset request JSON are
   included. Do not create `3.0-notion-freeze.md` or the ruleset read-back JSON before tagging; record
   both exact paths as `<not-created-yet: post-freeze-evidence>` in the transcript so no self-hash cycle
   exists. Create the freeze commit in an isolated worktree and require
   `git status --porcelain=v1 --untracked-files=all` there to return zero rows before tagging. This makes
   the clean freeze tree the proof for all unchanged tracked source/config/PM paths; they are not
   individually ledgered. Each main-checkout dirty/untracked row is preserved by its exact path and hash.
6. Commit and push the attributed baseline and all files marked included. Retain the captured transcript
   to materialize as the `Working-tree inclusion ledger` in the post-tag evidence commit. The already-included
   `dydo/project/migrations/3.0-pm-v2-final-ruleset.request.json` must contain exactly:

   ```json
   {
     "name": "protect-pm-v2-final",
     "target": "tag",
     "enforcement": "active",
     "bypass_actors": [],
     "conditions": {
       "ref_name": {
         "include": ["refs/tags/pm-v2-final"],
         "exclude": []
       }
     },
     "rules": [
       { "type": "update" },
       { "type": "deletion" }
     ]
   }
   ```

7. Create the annotated local tag at the freeze commit but do not push it. Before POST or push, run this
   PowerShell precondition; `<freeze-sha>` is the recorded full SHA:

   ```powershell
   $freezeSha = (git rev-parse '<freeze-sha>').Trim()
   if ($LASTEXITCODE -ne 0) { throw 'freeze SHA does not resolve' }
   $tagType = (git cat-file -t pm-v2-final).Trim()
   if ($LASTEXITCODE -ne 0 -or $tagType -ne 'tag') { throw 'pm-v2-final is not an annotated local tag' }
   $localPeel = (git rev-parse 'pm-v2-final^{}').Trim()
   if ($LASTEXITCODE -ne 0 -or $localPeel -ne $freezeSha) { throw "local tag peels to $localPeel, expected $freezeSha" }
   $remoteTag = @(git ls-remote --tags origin 'refs/tags/pm-v2-final' 'refs/tags/pm-v2-final^{}')
   if ($LASTEXITCODE -ne 0) { throw 'could not establish remote tag absence' }
   if ($remoteTag.Count -ne 0) { throw "remote pm-v2-final already exists; enter repair protocol, do not POST/push" }
   ```

   If local tag creation reports an existing tag, inspect it with the same `cat-file`/peel assertions. An
   annotated same-SHA tag is adopted; a lightweight or different-SHA tag stops for explicit human repair.
   Do not delete or recreate a local tag automatically.

8. With the precondition green, POST the request, save the full GET response, and assert every field in
   PowerShell. No print-only `jq` result is a gate:

   ```powershell
   $createdRaw = gh api --method POST repos/bodnarbalazs/dydo/rulesets --input dydo/project/migrations/3.0-pm-v2-final-ruleset.request.json
   if ($LASTEXITCODE -ne 0) { throw 'ruleset POST failed or was uncertain; enter repair protocol before retry' }
   $created = $createdRaw | ConvertFrom-Json
   $rulesetId = [long]$created.id
   if ($rulesetId -le 0) { throw 'ruleset POST returned no positive numeric id' }

   $readbackRaw = gh api "repos/bodnarbalazs/dydo/rulesets/$rulesetId"
   if ($LASTEXITCODE -ne 0) { throw 'ruleset GET failed' }
   [IO.File]::WriteAllText('dydo/project/migrations/3.0-pm-v2-final-ruleset.readback.json', $readbackRaw + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
   $ruleset = $readbackRaw | ConvertFrom-Json
   if ([long]$ruleset.id -ne $rulesetId) { throw 'ruleset id mismatch' }
   if ($ruleset.name -ne 'protect-pm-v2-final') { throw 'ruleset name mismatch' }
   if ($ruleset.target -ne 'tag') { throw 'ruleset target mismatch' }
   if ($ruleset.enforcement -ne 'active') { throw 'ruleset enforcement is not active' }
   $includes = @($ruleset.conditions.ref_name.include)
   if ($includes.Count -ne 1 -or $includes[0] -ne 'refs/tags/pm-v2-final') { throw 'ruleset include condition mismatch' }
   if (@($ruleset.conditions.ref_name.exclude).Count -ne 0) { throw 'ruleset has an exclusion' }
   $ruleTypes = @($ruleset.rules | ForEach-Object { $_.type } | Sort-Object)
   if ($ruleTypes.Count -ne 2 -or $ruleTypes[0] -ne 'deletion' -or $ruleTypes[1] -ne 'update') { throw "rules mismatch: $($ruleTypes -join ',')" }
   if (@($ruleset.bypass_actors).Count -ne 0) { throw 'ruleset has a bypass actor' }

   $allRulesetsRaw = gh api repos/bodnarbalazs/dydo/rulesets
   if ($LASTEXITCODE -ne 0) { throw 'ruleset list GET failed' }
   $matches = @(($allRulesetsRaw | ConvertFrom-Json) | Where-Object { $_.name -eq 'protect-pm-v2-final' -and $_.target -eq 'tag' })
   if ($matches.Count -ne 1) { throw "expected one protect-pm-v2-final tag ruleset, found $($matches.Count)" }
   if ([long]$matches[0].id -ne $rulesetId) { throw 'ruleset list id differs from created/read-back id' }

   git push origin refs/tags/pm-v2-final
   if ($LASTEXITCODE -ne 0) { throw 'tag push failed; enter repair protocol' }
   $remotePeel = ((git ls-remote origin 'refs/tags/pm-v2-final^{}') -split "`t")[0].Trim()
   if ($LASTEXITCODE -ne 0 -or $remotePeel -ne $freezeSha) { throw "remote tag peels to $remotePeel, expected $freezeSha" }
   ```

   Failure/repair protocol is non-destructive. If the POST result is uncertain, list exact-name rulesets:
   zero matches permits one retry after the API is healthy; one match must pass every assertion above and
   is adopted by ID; multiple matches stop for human cleanup. If a remote tag exists before the standard
   path, inspect its annotated type and peeled SHA without updating/deleting it. A same-SHA annotated tag
   may be adopted only after explicit human approval and after the ruleset assertions pass; a different
   SHA or lightweight tag stops the migration. If push fails after protection, re-read the remote: absent
   permits retry, the same peeled SHA is success, and any different value stops. Never auto-delete,
   force-update, or weaken the ruleset as repair.

   After the assertions and push pass, create
   `3.0-notion-freeze.md` with the exact SHA, dry-run transcript, inclusion ledger, tag, and ruleset
   result. In `3.0-pm-records.json`, update exactly the rows whose ratified `finalDisposition` is
   `extract-then-remove`, `remove-historical`, `cancel-remove`, or `drop-duplicate`: set their existing
   commit target to the exact SHA permalink where required and append
   `{ "kind": "freeze-commit", "value": "<exact-sha>" }` to `evidence`; do not add a top-level schema
   field. Mirror row IDs/counts in the `Freeze evidence` section of `3.0-pm-records.md`. Commit these
   three post-freeze evidence updates plus the read-back JSON without moving `pm-v2-final`. Fail the gate
   on any mismatch. No bypass actor is permitted.

### Exact per-lane implementation gates

Every lane first runs `dotnet build DynaDocs.Tests/DynaDocs.Tests.csproj -c Release` (which restores and
compiles both product and tests); then its exact test command is:

- **Issue 2:** `dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter
  "FullyQualifiedName~CommandSmokeTests|FullyQualifiedName~CommandDocConsistencyTests|FullyQualifiedName~CompletionProviderTests|FullyQualifiedName~ConfigFactoryTests|FullyQualifiedName~ModelCommandTests|FullyQualifiedName~ModelCapServiceTests|FullyQualifiedName~GuardIntegrationTests|FullyQualifiedName~IssueTests|FullyQualifiedName~TaskTests"`.
- **Issue 3:** `dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter
  "FullyQualifiedName~TemplateUpdateTests|FullyQualifiedName~TemplateGeneratorTests|FullyQualifiedName~FolderScaffolderTests|FullyQualifiedName~TemplateCommandTests|FullyQualifiedName~TemplateOverrideTests|FullyQualifiedName~ConfigServiceTests"`, then `dydo template update --diff`; the diff must not propose `sync-model.json`, Notion, or watchdog content.
- **Issue 4:** `dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter
  "FullyQualifiedName~FrontmatterParserTests"`, then run `py DynaDocs.Tests/coverage/gap_check.py --force-run`
  to prove `tier_registry.json` contains no deleted module/test pair and every surviving tier is valid.
- **Issue 5:** `dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter
  "FullyQualifiedName~CommandDocConsistencyTests|FullyQualifiedName~TemplateGeneratorTests|FullyQualifiedName~TemplateUpdateTests"`, then `dydo check` and `dydo template update --diff`.
- **Issue 6:** `dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter
  "FullyQualifiedName~ReleaseWorkflowTests"`, followed by every full/AOT/package gate below.

The following PowerShell predicate is the executable post-removal symbol/text gate after Issues 2–5:

```powershell
$forbiddenTracked = @(git ls-files -- 'Sync/**' 'DynaDocs.Tests/Sync/**' 'Models/SyncDoc.cs' 'Models/SyncField.cs' 'Serialization/NotionJsonContext.cs' 'Serialization/SyncModelJsonContext.cs' 'Commands/NotionCommand.cs' 'Commands/WatchdogCommand.cs' 'Services/WatchdogService.cs' 'Services/WatchdogLogger.cs')
if ($LASTEXITCODE -ne 0) { throw 'git ls-files deletion-boundary query failed' }
if ($forbiddenTracked.Count -ne 0) { throw "Deleted Sync/provider/test paths remain tracked:`n$($forbiddenTracked -join "`n")" }

$removedSymbols = 'NotionCommand|WatchdogCommand|WatchdogService|WatchdogLogger|INotionClient|ISyncAdapter|SyncRunner|ReconcileEngine|SyncModelLoader|SyncDocFile|\bSyncDoc\b|\bSyncField\b|\bSyncModel\b|DynaDocs\.Sync\.Projection|DualBodyBase|ProjectedMarkdown|SemanticTextMap|NotionConfig|NotionJsonContext|SyncModelJsonContext|NSec\.Cryptography'
$symbolRoots = @('Program.cs','Commands','Services','Models','Serialization','Sync','Utils','DynaDocs.csproj','DynaDocs.Tests') | Where-Object { Test-Path $_ }
$symbolHits = @(& rg -n $removedSymbols $symbolRoots --glob '*.cs' --glob '*.csproj')
if ($LASTEXITCODE -eq 0) { throw "Removed symbol consumer remains:`n$($symbolHits -join "`n")" }
if ($LASTEXITCODE -ne 1) { throw "Removed-symbol rg failed with exit $LASTEXITCODE" }

$removedText = 'notion|watchdog|sync-model\.json|DYDO_[A-Z0-9_]*NOTION|notion\.vault|notion_sync'
$activeRoots = @('Program.cs','Commands','Services','Models','Serialization','DynaDocs.csproj','README.md','npm/README.md','npm/package.json','Templates','dydo/reference','dydo/understand','dydo/guides','dydo.json','.github/workflows/release.yml','dydo/project/migrations/dydo-3-main-project-adoption.md') | Where-Object { Test-Path $_ }
$allowedTextFiles = @(
  'dydo/guides/migrating-dydo-1x-to-2x.md',
  'dydo/guides/migrating-dydo-2x-to-3x.md',
  'dydo/project/migrations/dydo-3-main-project-adoption.md'
)
$hitFiles = @(& rg -l -i $removedText $activeRoots)
if ($LASTEXITCODE -notin 0,1) { throw "Removed-text rg failed with exit $LASTEXITCODE" }
$unexpected = @($hitFiles | Where-Object { $_.Replace('\','/') -notin $allowedTextFiles })
if ($unexpected.Count -ne 0) { throw "Unexpected active Notion/watchdog text:`n$($unexpected -join "`n")" }
if (-not (Select-String -Quiet -LiteralPath 'dydo/guides/migrating-dydo-1x-to-2x.md' -Pattern '^status: historical$')) { throw '1.x-to-2.x guide lacks historical status' }
```

This is a closed file allowlist, not a reviewer judgment. Historical Decisions, changelog, and freeze/
disposition artifacts are deliberately outside `activeRoots`; adding any other active-file exception
requires a reviewed plan amendment.

### Full and coverage gates

- `py DynaDocs.Tests/coverage/run_tests.py` passes the complete suite.
- `py DynaDocs.Tests/coverage/gap_check.py --force-run` passes every surviving production module; the
  expected module count is deliberately recalculated after deletion rather than pinned to the old 141.
- `dydo check` exits 0 with no new warning class attributable to this change.
- `git diff --check` passes for the integrated change.

### Documentation and template gates

1. Run the repository's template update/install consistency tests, including command reference, template
   hashes, scaffold shape, and generated role/skill artifacts.
2. Run `dydo template update --diff` and require no deleted sync-model/Notion artifact to be proposed.
3. Compare source and npm README product claims and the built-in/generated command references.
4. Run the exact closed-allowlist PowerShell predicate above. Historical Decisions/changelog/freeze
   evidence are outside `activeRoots`; only the three named migration guides/playbooks may match.

### AOT, package, and release gates

- `dotnet publish DynaDocs.csproj -c Release -r win-x64 --self-contained -o <isolated-output>` succeeds
  locally with Native AOT and no removed serialization warnings.
- The reviewed release workflow succeeds for all five configured RIDs (`win-x64`, `linux-x64`,
  `linux-arm64`, `osx-x64`, `osx-arm64`).
- `dotnet pack DynaDocs.csproj -c Release -p:Version=3.0.0 -o <isolated-output>` succeeds; inspecting the
  package shows no Notion/sync-model docs or dependency.
- `npm pack --dry-run` in `npm/` succeeds with version 3.0.0 and the packaged README has no active Notion
  instructions.

#### Exact `v3.0.0` protection and tag gate

Do not begin this gate until all release files—including
`dydo/project/migrations/3.0-v3-release-ruleset.request.json`—are committed at the recorded final release
SHA, the worktree is clean, the integrated audit is PASS, and MCP read-back proves Linear Project 4
`c8ae27c3-5391-453a-8498-e02c064aa6ae` is Completed with an explicit human-acceptance URL recorded in
both the release changelog and main-project adoption playbook. The repository currently has no ruleset;
that is not a policy. Re-read current state and create the exact protection below.

The request JSON is exactly:

```json
{
  "name": "protect-v3.0.0",
  "target": "tag",
  "enforcement": "active",
  "bypass_actors": [],
  "conditions": {
    "ref_name": {
      "include": ["refs/tags/v3.0.0"],
      "exclude": []
    }
  },
  "rules": [
    { "type": "update" },
    { "type": "deletion" }
  ]
}
```

Create the annotated tag locally but do not push it, then execute the assertions and protection in this
order (`<final-release-sha>` is the recorded full SHA):

```powershell
$finalReleaseSha = (git rev-parse '<final-release-sha>').Trim()
if ($LASTEXITCODE -ne 0) { throw 'final release SHA does not resolve' }
if ((git rev-parse HEAD).Trim() -ne $finalReleaseSha) { throw 'HEAD is not the recorded final release SHA' }
if (@(git status --porcelain=v1 --untracked-files=all).Count -ne 0) { throw 'release worktree is not clean' }

git tag -a v3.0.0 $finalReleaseSha -m 'dydo 3.0.0'
if ($LASTEXITCODE -ne 0) { throw 'local v3.0.0 tag creation failed; enter repair protocol' }
$tagType = (git cat-file -t v3.0.0).Trim()
if ($LASTEXITCODE -ne 0 -or $tagType -ne 'tag') { throw 'v3.0.0 is not an annotated local tag' }
$localPeel = (git rev-parse 'v3.0.0^{}').Trim()
if ($LASTEXITCODE -ne 0 -or $localPeel -ne $finalReleaseSha) { throw "local v3.0.0 peels to $localPeel, expected $finalReleaseSha" }

$remoteTag = @(git ls-remote --tags origin 'refs/tags/v3.0.0' 'refs/tags/v3.0.0^{}')
if ($LASTEXITCODE -ne 0) { throw 'could not establish remote v3.0.0 absence' }
if ($remoteTag.Count -ne 0) { throw 'remote v3.0.0 already exists; enter repair protocol, do not POST/push' }

$preRulesetsRaw = gh api repos/bodnarbalazs/dydo/rulesets
if ($LASTEXITCODE -ne 0) { throw 'preflight ruleset list failed' }
$preMatches = @(($preRulesetsRaw | ConvertFrom-Json) | Where-Object { $_.name -eq 'protect-v3.0.0' -and $_.target -eq 'tag' })
if ($preMatches.Count -ne 0) { throw "protect-v3.0.0 already exists ($($preMatches.Count)); enter repair protocol" }

$createdRaw = gh api --method POST repos/bodnarbalazs/dydo/rulesets --input dydo/project/migrations/3.0-v3-release-ruleset.request.json
if ($LASTEXITCODE -ne 0) { throw 'v3 ruleset POST failed or was uncertain; enter repair protocol before retry' }
$created = $createdRaw | ConvertFrom-Json
$rulesetId = [long]$created.id
if ($rulesetId -le 0) { throw 'v3 ruleset POST returned no positive numeric id' }

$readbackRaw = gh api "repos/bodnarbalazs/dydo/rulesets/$rulesetId"
if ($LASTEXITCODE -ne 0) { throw 'v3 ruleset GET failed' }
[IO.File]::WriteAllText('dydo/project/migrations/3.0-v3-release-ruleset.readback.json', $readbackRaw + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
$ruleset = $readbackRaw | ConvertFrom-Json
if ([long]$ruleset.id -ne $rulesetId) { throw 'v3 ruleset id mismatch' }
if ($ruleset.name -ne 'protect-v3.0.0') { throw 'v3 ruleset name mismatch' }
if ($ruleset.target -ne 'tag') { throw 'v3 ruleset target mismatch' }
if ($ruleset.enforcement -ne 'active') { throw 'v3 ruleset enforcement is not active' }
$includes = @($ruleset.conditions.ref_name.include)
if ($includes.Count -ne 1 -or $includes[0] -ne 'refs/tags/v3.0.0') { throw 'v3 ruleset include condition mismatch' }
if (@($ruleset.conditions.ref_name.exclude).Count -ne 0) { throw 'v3 ruleset has an exclusion' }
$ruleTypes = @($ruleset.rules | ForEach-Object { $_.type } | Sort-Object)
if ($ruleTypes.Count -ne 2 -or $ruleTypes[0] -ne 'deletion' -or $ruleTypes[1] -ne 'update') { throw "v3 rules mismatch: $($ruleTypes -join ',')" }
if (@($ruleset.bypass_actors).Count -ne 0) { throw 'v3 ruleset has a bypass actor' }

$allRulesetsRaw = gh api repos/bodnarbalazs/dydo/rulesets
if ($LASTEXITCODE -ne 0) { throw 'v3 ruleset list GET failed' }
$matches = @(($allRulesetsRaw | ConvertFrom-Json) | Where-Object { $_.name -eq 'protect-v3.0.0' -and $_.target -eq 'tag' })
if ($matches.Count -ne 1) { throw "expected one protect-v3.0.0 tag ruleset, found $($matches.Count)" }
if ([long]$matches[0].id -ne $rulesetId) { throw 'v3 ruleset list id differs from created/read-back id' }

git push origin refs/tags/v3.0.0
if ($LASTEXITCODE -ne 0) { throw 'v3.0.0 push failed; enter repair protocol' }
$remotePeel = ((git ls-remote origin 'refs/tags/v3.0.0^{}') -split "`t")[0].Trim()
if ($LASTEXITCODE -ne 0 -or $remotePeel -ne $finalReleaseSha) { throw "remote v3.0.0 peels to $remotePeel, expected $finalReleaseSha" }
```

The read-back JSON is post-tag evidence: commit it after the push without moving `v3.0.0`. Repair is
non-destructive. An existing local annotated same-SHA tag may be adopted; a lightweight/different tag
stops. For an uncertain POST, list exact-name rulesets: zero permits one retry after API recovery, one
must pass all assertions and is adopted by ID, multiple stop for human cleanup. For a pre-existing remote
tag, inspect type/peel without mutation: same-SHA annotated may be adopted only with explicit human
approval after protection passes; lightweight/different stops. After push failure, absent permits retry,
same peeled SHA is success, different stops. Never auto-delete/force-update the tag, weaken protection,
or add a bypass actor.

- A fresh integrated audit receives this plan, the entire merged diff, full gate transcripts, and the
  freeze evidence. Only PASS plus Project 4's recorded explicit human acceptance permits the annotated
  protected `v3.0.0` tag and publication.

## 6. Ordering, dirty-worktree isolation, and merge protocol

1. Review this plan and publish its exact governing commit. Create the detailed Linear Issues only after
   PASS, recording commit permalinks and exact owned paths.
2. Attribute the current dirty tree. No lane may reset it, include it wholesale, or treat untracked files
   as generated trash. The existing `dydo.json` edit makes that file a mandatory serial integration seam.
3. Execute Issue 1 from the current 2.2.9 implementation before any deletion. This is the sole hard
   pre-deletion dependency that the acceleration does not waive.
4. Branch isolated lanes from the frozen exact commit. Land Issue 2, then prove and land Issue 3, then
   land Issue 4. Project 2 and the exact overlap/amendment gate must already have landed before Issue 3;
   Issue 5 follows Issue 3 with no speculative concurrent edits.
5. Merge passed Issues serially using only the exact Shared hot-file landing order table. A path absent
   from that table is disjoint; discovering another overlap blocks merge until a reviewed amendment names
   its owners and order.
6. Rebase/reconcile against the Linear-native ontology and corpus migrations. Source deletion can land
   before dogfood; final 3.0 packaging cannot land until Projects 2–3 supply the definitive surviving
   command/folder/docs shape.
7. Run full gates and an independent integrated audit. Prepare release artifacts, but do not create or
   push `v3.0.0` and do not publish GitHub/NuGet/npm packages until Project 4 records explicit human
   acceptance of the dogfooded operating model.

## 7. Rollback and failure handling

- Before deletion, rollback is simply “do not start”: leave the 2.2.9 tree and remote Notion untouched.
- During implementation, revert the bounded deletion commits or abandon their isolated worktrees. Never
  reconstruct files manually when the frozen exact commit is available.
- After deletion but before release, restore any required source/file from the exact freeze SHA or move
  the release branch back by ordinary revert commits. Do not restart an old watchdog from a dirty or
  partially restored tree.
- After 3.0 publication, ship a corrective 3.x release or explicitly restore the complete 2.2.9 boundary
  from `pm-v2-final`; do not create a partial hybrid adapter.
- Linear work remains independent throughout. No rollback copies Linear state into repo PM records or
  restarts synchronization.
- Remote Notion data and preserved local stores remain available as evidence, but querying or cleaning
  them requires a separate bounded authorization after the migration is accepted.

## 8. Watch-outs and human touchpoints

- The old sync engine and the surviving `dydo sync` command are different systems. Symbol/path-based
  deletion is safe; noun-based deletion is not.
- `ModelCapService` is a hidden watchdog consumer. Removing its logging call must not remove automatic
  expiry restoration from `GuardCommand`.
- The release job graph currently depends on a Notion-specific job. Removing only its steps leaves an
  invalid or meaningless dependency.
- Template hashes and generated installed files must move together; otherwise `dydo template update`
  can resurrect deleted content or report permanent drift.
- Unknown `notion` keys in existing 2.x configs should remain forward-tolerated. Do not add a 3.0 runtime
  migrator solely to delete a harmless ignored key.
- The local vault/state paths may contain secrets or authored bodies. Do not inspect them for proof and
  do not include them in commits, test fixtures, transcripts, or support bundles.
- Historical evidence is allowed to mention Notion; active documentation is not. A repository-wide
  zero-hit assertion would destroy provenance and is therefore the wrong gate.
- Current human involvement is not needed for routine source deletion, test repair, docs, or audits.
  It is needed if a credential/passphrase is unavailable, the dirty `dydo.json` owner has not landed
  their change, Project 4 needs explicit acceptance, or the final 3.0 release/tag is ready for acceptance.
  A non-empty dry-run is recorded, not escalated into live-write authorization inside this plan.

## Plan review

**PASS — 2026-08-27.** An independent reviewer found the plan delivery-ready after the remote read-only
freeze boundary, both exact tag-protection protocols, file ownership/serial landing order, dirty-tree
ledger, Project-2/Project-4 gates, and executable deletion/test/docs/release checks closed all prior
blocker classes. This verdict activates the plan only; no freeze, implementation, deletion, tag, or
release is claimed complete.

Plan-gate evidence: 2,758/2,758 tests passed with 25 skipped; coverage gap verification passed 141/141
modules; targeted `git diff --check` passed; targeted `dydo check` reported 0 errors (only the expected
generated-index orphan warning before index regeneration).
