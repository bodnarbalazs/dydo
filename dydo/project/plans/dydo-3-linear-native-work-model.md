---
title: dydo 3.0 Linear-Native Work Model
status: draft
area: project
type: context
linear-project: https://linear.app/bodnar-balazs/project/dydo-30-adopt-linear-native-work-model-8145ca3f78ad
---

# dydo 3.0 Linear-Native Work Model

This plan makes Linear the only live work graph and reduces dydo to its durable knowledge and proof role. It replaces the file-backed Campaign/Sprint/Slice/Task/observed-Issue machinery across doctrine, authored templates, compiled skills, CLI/config/model surfaces, and tests, while retaining FutureFeature as a deliberately unscheduled repo-native idea.

## 1. Specification

### Intent

Deliver the work-model contract required by [DR 044](../decisions/044-linear-canonical-pm-and-dydo-knowledge-boundary.md) before Project 3 disposes of the old PM corpus and before Project 5 removes the stopped Notion runtime. This Project does not build a Linear client, mirror, local cache, webhook receiver, or alternate PM schema. It removes dydo's live-work record and command contracts so agents use Linear's native objects, while dydo continues to author and validate durable documentation.

### Linear-native ontology

| Concept | Canonical owner | Rule |
|---|---|---|
| Initiative | Linear workspace | Broad multi-Project goal. Optional; the current Dydo dogfood intentionally creates none because its workspace is shared. |
| Project | Linear team | Bounded product or technical outcome, linked to one reviewed repo Project plan when work is coordinated or architecture-sensitive. |
| Milestone | Linear Project | Optional meaningful checkpoint, never a required mirror of a repo document. |
| Issue | Linear team | The only actionable work item. Workflow status, priority, assignee, blockers, updates, review state, and execution evidence live here. |
| Sub-issue | Linear | Optional decomposition of an Issue only when the children need independent tracking; it creates no new dydo record class. |
| Cycle | Linear team | Optional capacity timebox, orthogonal to Projects; do not enable it until observed accepted-increment throughput can calibrate it. |
| Label | Linear team | Use sparingly for cross-cutting routing, not a shadow type system. `HITL`, `AFK`, and `Needs human` are the initial approved labels. |
| Wayfinding map / Waypoint / Fog | dydo/Git | Optional durable navigation knowledge, never live work objects or a duplicate hierarchy. |

Campaign, Sprint, Slice, Task, backlog item, and the old observed-problem Issue are retired as canonical PM object types. “Slice” may remain an informal implementation technique; it never creates a file, command, state machine, or Linear type. A branch, worktree, coding session, worker subagent, reviewer pass, PR, or audit attempt is evidence linked to an Issue, not work in its own right.

### FutureFeature remains an idea record

`dydo/project/future-features/` remains the sole home of a FutureFeature. It is a repo-native idea: unscheduled, non-actionable, and intentionally absent from Linear until a human promotes it. Its exact frontmatter is `area: project`, `type: concept`, and `status: idea` or terminal `status: promoted`. The body must contain a non-empty `## Rationale` section and a `## Related` section with at least one resolving, non-Linear durable-knowledge link.

Only the human may promote an idea. Promotion creates exactly one appropriately shaped Linear Initiative, Project, or Issue and records that stable URL under `linear-reference`; its grammar is `https://linear.app/<workspace>/(issue/<TEAM>-<number>[/<slug>]|project/<slug>-<12-lowercase-hex>|initiative/<slug>-<12-lowercase-hex>)`. `promoted` is terminal and does not mirror subsequent delivery status. `idea` requires no `linear-reference`; `promoted` requires exactly one. Both states prohibit `assigned`, `assignee`, `priority`, `blocked-by`, `blocks`, `dependency`, `dependencies`, `project`, `initiative`, `cycle`, `milestone`, `sprint`, `campaign`, `slice`, `task`, `issue`, `workflow`, `state`, `due-date`, `estimate`, `labels`, `parent`, `sub-issue`, and `team`. Project 3, not this Project, normalizes existing records and performs any human-approved promotion.

### Reference and evidence rules

- A current human-navigation link uses a branch-following GitHub URL. An execution Issue also records the exact governing commit SHA and its commit permalink before work starts.
- A coordinated Project plan has one `linear-project` frontmatter URL. It is a provenance link, not synchronization.
- A Linear Project/Issue links to the plan, relevant Decision(s), acceptance/audit evidence, and the governing commit. A PR/commit references its Linear Issue key, so Linear's GitHub integration provides native execution linkage.
- Retained documents that must cite removed v2 work use an exact `pm-v2-final` commit-SHA permalink or a retained durable artifact; they do not retain a dead local record link.
- New durable invariants, decisions, assimilation briefs, and audit reports are written to dydo/Git, not stranded in Linear comments or sessions.
- No dydo command reads, writes, provisions, polls, caches, mirrors, or validates Linear. The official Linear MCP/UI/API remains the live PM surface outside the dydo runtime.

### Out of scope

- Creating, modifying, or importing Linear objects; Project 2 Issues are created only after this plan is reviewed and published.
- Applying the Project 1 record-disposition manifest or deleting legacy corpus files; that is Project 3.
- Deleting Notion/sync/watchdog/token runtime code, local rollback stores, or remote Notion data; that is Project 5 and the separate freeze boundary.
- Changing accepted historical Decisions, changelog, audits, or the protected pre-deletion evidence merely to erase old terminology.

### Frozen-v2 compatibility exception

Project 2 must pass while the frozen v2 corpus and Notion projection schema remain tracked. Before every Project-2 compatibility gate, run `pwsh -NoProfile -File dydo/project/migrations/build-3.0-pm-manifest.ps1 -RepoRoot . -Verify` and require its zero-missing/zero-duplicate/zero-unresolved result; this verifies inventory shape only and does not assert ratification. Project 2 adds `Services/LegacyPmManifestService.cs` and `Rules/LegacyPmRecordRule.cs`, registered in `Commands/CheckDocValidator.cs`. Their closed allow-set is: each exact normalized `records[].path` with `executionState: pending`; six retained hubs, `dydo/project/campaigns/_index.md`, `dydo/project/sprints/_index.md`, `dydo/project/slices/_index.md`, `dydo/project/tasks/_index.md`, `dydo/project/issues/_index.md`, and `dydo/project/backlog/_index.md`; and six retained meta files, `dydo/project/campaigns/_campaigns.md`, `dydo/project/sprints/_sprints.md`, `dydo/project/slices/_slices.md`, `dydo/project/tasks/_tasks.md`, `dydo/project/issues/_issues.md`, and `dydo/project/backlog/_backlog.md`. The rule rejects every candidate outside that allow-set: an unknown `_*.md` file, a legacy typed record outside its canonical legacy directory, a pending manifest path that no longer resolves, and any otherwise shaped non-manifest record. `DynaDocs.Tests/Services/LegacyPmManifestServiceTests.cs` proves parsing/path normalization and the twelve retained non-record paths; `DynaDocs.Tests/Rules/LegacyPmRecordRuleTests.cs` proves each of those allow-set classes, plus rejection of an unknown `_*.md`, an out-of-directory typed record, a missing pending manifest path, and an ordinary non-allow-set candidate. Thus no new non-manifest repo-PM record can enter during Project 2 without relaxing a tested rule.

Project 3 owns human ratification, record normalization, and removal. Only after every record has a human ruling may it run `pwsh -NoProfile -File dydo/project/migrations/build-3.0-pm-manifest.ps1 -RepoRoot . -Verify -RequireRatified`; it then changes ratified rows to `executionState: applied` as their exact removals/migrations land. Project 3 also removes `Rules/FrontmatterRule.cs`'s task-file exception and `issue` from `Models/Frontmatter.cs`, `Templates/types.json.template`, and `dydo/_system/types.json`; creates/registers `Rules/FutureFeatureRule.cs`; and updates `DynaDocs.Tests/Rules/FrontmatterRuleTests.cs`, new `DynaDocs.Tests/Rules/FutureFeatureRuleTests.cs`, `DynaDocs.Tests/Services/FrontmatterTypesServiceTests.cs`, and the tested LegacyPm rule behavior. `DynaDocs.Tests/Utils/FrontmatterParserTests.cs` remains a Project-2 compatibility test and a surviving native-compiler test, not a Project-3 deletion target. Until Project 5, `Templates/sync-model.template.json`, `dydo/_system/sync-model.json`, `Sync/**`, `DynaDocs.Tests/Sync/**`, and local Notion rollback stores remain Project-5-owned compatibility residue. Project 2's scans expressly exclude those exact paths and instead prove that surviving non-sync runtime, templates, generated skills, active product docs, and non-sync tests have no repo-PM consumer. This exception expires only when Projects 3 and 5 complete their respective gates.

## 2. Legacy surface to retire

The following are executable or authored live-PM surfaces, not historical evidence. Project 2 removes or rewrites them; Project 3 removes their legacy corpus after ratified disposition.

| Surface | Required outcome |
|---|---|
| `Program.cs`; `Commands/TaskCommand.cs`; `Commands/TaskCreateHandler.cs`; `Commands/TaskDoneHandler.cs`; `Commands/TaskListHandler.cs`; `Commands/TaskReviewHandler.cs`; `Commands/IssueCommand.cs`; `Commands/IssueCreateHandler.cs`; `Commands/IssueListHandler.cs`; `Commands/IssueResolveHandler.cs` | Delete the repo task/issue CLI and registrations. No replacement command proxies Linear. |
| `Models/TaskFile.cs`; `Models/TaskStatus.cs`; `Models/IssueStatus.cs`; `Models/IssueSeverity.cs`; `Models/IssueFoundBy.cs` | Delete the local live-work models/enums when their callers are gone. |
| `Models/StructureConfig.cs`; `Services/ConfigService.cs`; `Services/ConfigFactory.cs`; `Services/FolderScaffolder.cs`; `Services/HubGenerator.cs`; `Commands/FixHubHandler.cs`; `Services/TemplateGenerator.cs`; `Services/CompletionProvider.cs`; `Utils/PathUtils.cs`; `dydo.json` | Remove task/issue path configuration, scaffold/index generation, completion/help/nudge command vocabulary, and task-name path validation. Preserve generic documentation scanning, guarding, native role/skill compilation, and non-PM configuration. Existing 2.x `structure.tasks`/`structure.issues` input is ignored safely, with migration guidance; no config migration writes files. |
| `Templates/_tasks.template.md`; `Templates/_issues.template.md`; `Templates/_backlog.template.md`; `Templates/_project.template.md`; `Templates/_future-features.template.md`; `Templates/dydo-glossary.template.md`; `Templates/about-dynadocs.template.md`; `Templates/dydo-commands.template.md`; `Templates/architecture.template.md` | Delete task/issue/backlog templates and rewrite the surviving project/FutureFeature/reference templates to the Linear/Git boundary. Do not add a Linear schema template. |
| `Templates/mode-chief-of-staff.template.md`; `Templates/mode-co-thinker.template.md`; `Templates/mode-code-writer.template.md`; `Templates/mode-docs-writer.template.md`; `Templates/mode-inquisitor.template.md`; `Templates/mode-orchestrator.template.md`; `Templates/mode-planner.template.md`; `Templates/mode-reviewer.template.md`; `Templates/mode-self-improvement.template.md`; `Templates/mode-test-writer.template.md`; `Templates/mode-wayfinder.template.md`; `Templates/reviewer-resource-code.template.md`; `Templates/reviewer-resource-merge-sprint.template.md`; `Templates/reviewer-resource-plan.template.md`; `Templates/reviewer-resource-tests.template.md`; `Templates/workflow-run-sprint.js`; `Templates/workflow-inquisition.js` | Rewrite reviewed-intent, Issue, Project-plan, independent-review, integrated-audit, and assimilation language. Retire record-root/run-sprint assumptions. Preserve native platform delegation and worktree use. Generate `.agents/skills/**` from these authoritative templates; never hand-edit compiled skills. |
| `dydo/reference/dydo-glossary.md`; `dydo/understand/work-model.md`; `dydo/understand/task-lifecycle.md`; `dydo/glossary.md`; `dydo/guides/orchestration-pitfalls.md`; `dydo/guides/writing-good-briefs.md`; `README.md` | Rewrite active doctrine and examples so they describe the Linear work graph and Git knowledge boundary. Historical terminology remains only where explicitly framed as history or migration evidence. |

`Templates/sync-model.template.json`, `dydo/_system/sync-model.json`, Notion references, and generated framework-hash cleanup remain Project 5 work. Project 2 may remove PM terms from active product prose, but must not delete or alter those Project-5-owned runtime/template files without a reviewed amendment.

### Closed Project-2 implementation matrix

The following exact paths replace the globs and category wording above. A lane may change no other
production, test, template, or generated path without a reviewed amendment.

| Lane | Exact production/template/generated paths | Exact tests and required result |
|---|---|---|
| 1 | `Commands/CheckDocValidator.cs`; new `Services/LegacyPmManifestService.cs`; new `Rules/LegacyPmRecordRule.cs`; `Rules/SummaryRule.cs`; `Rules/HubFilesRule.cs`; `Rules/OrphanDocsRule.cs`; `Rules/FolderMetaFilesRule.cs`; `Templates/_future-features.template.md`; `Templates/dydo-glossary.template.md`; `dydo/project/future-features/_future-features.md`; `dydo/reference/dydo-glossary.md`; `dydo/understand/work-model.md`; `dydo/understand/task-lifecycle.md`; `dydo/glossary.md`; `dydo/guides/orchestration-pitfalls.md`; `dydo/guides/writing-good-briefs.md` | New `DynaDocs.Tests/Services/LegacyPmManifestServiceTests.cs`; new `DynaDocs.Tests/Rules/LegacyPmRecordRuleTests.cs`; `DynaDocs.Tests/Rules/SummaryRuleTests.cs`; `DynaDocs.Tests/Rules/BrokenLinksRuleTests.cs`; `DynaDocs.Tests/Rules/HubFilesRuleTests.cs`; `DynaDocs.Tests/Rules/OrphanDocsRuleTests.cs`; `DynaDocs.Tests/Rules/FolderMetaFilesRuleTests.cs`; `DynaDocs.Tests/Services/DocScannerTests.cs`. Replace the removed `IssueCreateHandler.SummaryPlaceholder` dependency with a local generic placeholder contract; add the manifest-backed no-new-record rule; document, but do not yet register, strict FutureFeature validation. |
| 2 | `Program.cs`; delete `Commands/TaskCommand.cs`, `Commands/TaskCreateHandler.cs`, `Commands/TaskDoneHandler.cs`, `Commands/TaskListHandler.cs`, `Commands/TaskReviewHandler.cs`, `Commands/IssueCommand.cs`, `Commands/IssueCreateHandler.cs`, `Commands/IssueListHandler.cs`, `Commands/IssueResolveHandler.cs`, `Commands/ReviewCommand.cs`, `Models/TaskFile.cs`, `Models/TaskStatus.cs`, `Models/IssueStatus.cs`, `Models/IssueSeverity.cs`, `Models/IssueFoundBy.cs`; edit `Models/StructureConfig.cs`, `Services/ConfigService.cs`, `Services/IConfigService.cs`, `Services/ConfigFactory.cs`, `Services/FolderScaffolder.cs`, `Services/HubGenerator.cs`, `Commands/FixHubHandler.cs`, `Commands/HelpCommand.cs`, `Services/CompletionProvider.cs`, `Utils/PathUtils.cs`, and `dydo.json` | Delete `DynaDocs.Tests/Integration/IssueTests.cs`, `DynaDocs.Tests/Integration/TaskTests.cs`, `DynaDocs.Tests/Integration/WorkflowTests.cs`, and `DynaDocs.Tests/Models/TaskFileTests.cs`; rewrite `DynaDocs.Tests/Commands/CompleteCommandTests.cs`, `DynaDocs.Tests/Commands/CommandSmokeTests.cs`, `DynaDocs.Tests/Commands/CompletionsCommandTests.cs`, `DynaDocs.Tests/Commands/HelpCommandTests.cs`, `DynaDocs.Tests/Integration/InitCommandTests.cs`, `DynaDocs.Tests/Integration/InitCheckIntegrationTests.cs`, `DynaDocs.Tests/Integration/FixCommandIntegrationTests.cs`, `DynaDocs.Tests/Services/CompletionProviderTests.cs`, `DynaDocs.Tests/Services/ConfigFactoryTests.cs`, `DynaDocs.Tests/Services/ConfigServiceTests.cs`, `DynaDocs.Tests/Services/ConfigurablePathsTests.cs`, `DynaDocs.Tests/Services/FolderScaffolderTests.cs`, `DynaDocs.Tests/Services/HubGeneratorTests.cs`, `DynaDocs.Tests/Utils/RuleSkipPathsTests.cs`, and `DynaDocs.Tests/Rules/OffLimitsRuleTests.cs`. Prove removal leaves no local PM command/model/config consumer. |
| 3 | delete `Templates/_tasks.template.md`, `Templates/_issues.template.md`, and `Templates/_backlog.template.md`; edit `Templates/_project.template.md`, `Templates/about-dynadocs.template.md`, `Templates/dydo-commands.template.md`, `Templates/architecture.template.md`, `Templates/index.template.md`, `Templates/mode-bro.template.md`, `Templates/mode-chief-of-staff.template.md`, `Templates/mode-co-thinker.template.md`, `Templates/mode-code-writer.template.md`, `Templates/mode-docs-writer.template.md`, `Templates/mode-grilling.template.md`, `Templates/mode-inquisitor.template.md`, `Templates/mode-orchestrator.template.md`, `Templates/mode-planner.template.md`, `Templates/mode-reviewer.template.md`, `Templates/mode-self-improvement.template.md`, `Templates/mode-test-writer.template.md`, `Templates/mode-wayfinder.template.md`, `Templates/reviewer-resource-code.template.md`, `Templates/reviewer-resource-docs.template.md`, `Templates/reviewer-resource-merge-sprint.template.md`, `Templates/reviewer-resource-plan.template.md`, `Templates/reviewer-resource-tests.template.md`, `Templates/workflow-run-sprint.js`, `Templates/workflow-inquisition.js`, and `Services/TemplateGenerator.cs`; regenerate `dydo/_system/templates/**` and `.agents/skills/**` with the product command | Rewrite `DynaDocs.Tests/Services/TemplateGeneratorTests.cs`, `DynaDocs.Tests/Services/TemplateUpdateTests.cs`, `DynaDocs.Tests/Integration/TemplateCommandTests.cs`, `DynaDocs.Tests/Integration/TemplateOverrideTests.cs`, `DynaDocs.Tests/Integration/ProcessWorkflowTests.cs`, `DynaDocs.Tests/Commands/WayfinderHarmonyTests.cs`, and `DynaDocs.Tests/Integration/CodexSyncArtifactsE2ETests.cs`. Prove source templates, installed framework files, and compiled skills agree. |
| 4 | `README.md`; `dydo/index.md`; `dydo/project/_index.md`; `dydo/reference/_index.md`; `dydo/reference/about-dynadocs.md`; `dydo/reference/configuration.md`; `dydo/reference/dydo-commands.md`; `dydo/understand/about.md`; `dydo/understand/architecture.md`; `dydo/understand/documentation-model.md`; `dydo/understand/templates-and-customization.md`; `dydo/guides/getting-started.md`; `dydo/guides/customizing-roles.md`; `dydo/guides/testing-strategy.md`; `dydo/guides/troubleshooting.md`; `dydo/guides/adding-a-command.md`; `dydo/guides/how-to-use-docs.md` | Rewrite `DynaDocs.Tests/Commands/CommandDocConsistencyTests.cs`, `DynaDocs.Tests/Commands/ValidateCommandTests.cs`, `DynaDocs.Tests/Integration/DocumentationTests.cs`, `DynaDocs.Tests/Integration/InitCommandTests.cs`, and `DynaDocs.Tests/Integration/CodexSyncArtifactsE2ETests.cs`; run `dydo check` against a fresh initialized fixture and prove no legacy hierarchy is scaffolded. |
| 5 | new `dydo/project/migrations/3.0-linear-work-model.md` and new `dydo/project/migrations/3.0-linear-work-model-assimilation.md` only | No production edit. Run the integrated commands and capture their exact exits/read-back as evidence. |

Generated paths are never hand-edited. `dydo template update` owns `dydo/_system/templates/**`, and
`dydo sync` owns `.agents/skills/**`; a generated change outside those commands fails the lane.

## 3. Delivery lanes and ownership

Every lane begins from the published reviewed plan commit in an isolated worktree. Lanes create no Linear objects until the plan gate below passes and their detailed Linear Issues have governing context, exact owned paths, and gates.

| Lane | Outcome and exclusive ownership | Depends on |
|---|---|---|
| 1. Doctrine and compatibility contract | The Lane-1 exact paths in §2. Defines the target terms and FutureFeature state/reference rules while preserving the frozen corpus's task exception and issue vocabulary until Project 3 applies the manifest. | Project 1 accepted; this plan PASS |
| 2. Retire local work runtime | The exact `Program.cs`, `Commands/*Task*`, `Commands/Issue*.cs`, Models, config/scaffold/completion/path files, `dydo.json`, and their direct tests named in §2. Deletes local task/issue command and model surfaces without introducing a Linear client. | Lane 1 |
| 3. Templates, skills, and workflows | The mode/resource/workflow templates in §2, generated `.agents/skills/**`, and their template/skill/workflow tests. Replaces record-based planning/orchestration/review/audit language with reviewed Linear Issue/Project-plan contracts. | Lane 1; Lane 2's command names settled |
| 4. Active product docs and integration proof | `README.md`, active reference/understand/guides whose content is generated from or complements Lanes 1–3, plus command-doc/completion/init/validation integration tests. Removes live PM claims and verifies a fresh scaffold contains no repo work hierarchy. | Lanes 2–3 |
| 5. Serial integration and audit | `dydo/project/migrations/3.0-linear-work-model.md`, `dydo/project/migrations/3.0-linear-work-model-assimilation.md`, evidence updates, conflict resolution, full gates, and independent review. No unrelated migration corpus files are absorbed. | Lanes 1–4 |

### Owned path overlap with Project 5

Project 2 lands first and Project 5 lands second for every overlap below. Project 5 may remove residual Notion/sync behavior only after taking the then-current Project 2 result; it must not restore a retired repo-PM surface.

| Shared path | Project 2 first | Project 5 second |
|---|---|---|
| `Program.cs`, `Commands/HelpCommand.cs`, `Services/CompletionProvider.cs`, `Services/ConfigFactory.cs`, `DynaDocs.Tests/Commands/CommandSmokeTests.cs`, `DynaDocs.Tests/Commands/CommandDocConsistencyTests.cs`, `DynaDocs.Tests/Services/CompletionProviderTests.cs`, `DynaDocs.Tests/Services/ConfigFactoryTests.cs` | Remove task/issue/review command semantics and expectations. | Remove Notion/watchdog seams from the surviving command/config contract. |
| `Services/FolderScaffolder.cs`, `DynaDocs.Tests/Services/FolderScaffolderTests.cs`, `DynaDocs.Tests/Services/ConfigServiceTests.cs`, `DynaDocs.Tests/Integration/TemplateCommandTests.cs`, `DynaDocs.Tests/Integration/TemplateOverrideTests.cs` | Remove task/issue/backlog scaffold and configuration expectations. | Remove sync-model/template-config expectations after the Project-2 result lands. |
| `dydo.json` | Retire `structure.tasks`, `structure.issues`, and task/issue nudge vocabulary after its current dirty-tree owner is resolved. | Remove Notion config and sync-model/watchdog hash remnants. |
| `Services/TemplateGenerator.cs`, `DynaDocs.Tests/Services/TemplateGeneratorTests.cs`, `DynaDocs.Tests/Services/TemplateUpdateTests.cs` | Establish Linear/Git PM template prose and compiled-artifact expectations. | Remove sync-model/Notion/watchdog generation and final product wording. |
| `README.md`, `dydo/reference/_index.md`, `dydo/reference/dydo-commands.md`, `dydo/reference/about-dynadocs.md`, `dydo/understand/about.md`, `dydo/understand/architecture.md`, `dydo/understand/work-model.md`, `dydo/guides/orchestration-pitfalls.md`, `Templates/about-dynadocs.template.md`, `Templates/dydo-commands.template.md` | Establish the Linear-native work model. | Remove remaining active Notion claims and publish migration/release guidance. |
| `Utils/FrontmatterParser.cs`, `DynaDocs.Tests/Utils/FrontmatterParserTests.cs` | Preserve the parser and update only its repository-PM expectations if needed; do not delete either path. | Retain both paths as native-compiler implementation and coverage; only clean stale sync-specific commentary after proving the three named consumers. |

Any additional overlap requires reviewed amendments to both plans before a later Lane enters `Todo`.

## 4. Evidence, migration, and verification

Lane 5 creates `dydo/project/migrations/3.0-linear-work-model.md` and `dydo/project/migrations/3.0-linear-work-model-assimilation.md`. The former records the published plan SHA/permalink; the exact Linear Project 2 URL; Project-resource read-back showing that URL is attached; each detailed Linear Issue ID/URL and governing SHA; generated-skill/template update evidence; scan counts; and test command exits. The latter records the independent review verdict, integrated-audit result, observed friction, adopted changes, and deferred follow-ups. Neither file copies Linear workflow state.

Before Project 3 begins, the integrator must use the official Linear connector to resolve Project 2 by the fixed ID `44eba9ff-0242-4179-b94b-932339b364fd`, assert team ID `caa6ccbf-4f9b-477e-826c-a51ed43b0687`, assert its URL equals the frontmatter URL, and read back an attached published GitHub plan resource whose URL equals the published plan permalink. For each Project-2 Issue, read back its ID, identifier, Project ID, team ID, governing-commit permalink attachment, and its actual `Done` status before the Project is completed. This is a one-time audit of references, not a dydo runtime feature. Project 3 then uses the Project 1 disposition manifest to remove legacy record files and rewrite incoming links; Project 2 does not silently remove them.

## 5. Tests and acceptance gates

1. Each lane first runs `dotnet build DynaDocs.Tests/DynaDocs.Tests.csproj -c Release`, then its exact command below. Deleted tests are not run: their required absence is verified by the lane's zero-consumer predicate and build.

   - Lane 1: `dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "FullyQualifiedName~LegacyPmManifestServiceTests|FullyQualifiedName~LegacyPmRecordRuleTests|FullyQualifiedName~SummaryRuleTests|FullyQualifiedName~BrokenLinksRuleTests|FullyQualifiedName~HubFilesRuleTests|FullyQualifiedName~OrphanDocsRuleTests|FullyQualifiedName~FolderMetaFilesRuleTests|FullyQualifiedName~DocScannerTests"`.
   - Lane 2: `dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "FullyQualifiedName~CompleteCommandTests|FullyQualifiedName~CommandSmokeTests|FullyQualifiedName~CompletionsCommandTests|FullyQualifiedName~HelpCommandTests|FullyQualifiedName~InitCommandTests|FullyQualifiedName~InitCheckIntegrationTests|FullyQualifiedName~FixCommandIntegrationTests|FullyQualifiedName~CompletionProviderTests|FullyQualifiedName~ConfigFactoryTests|FullyQualifiedName~ConfigServiceTests|FullyQualifiedName~ConfigurablePathsTests|FullyQualifiedName~FolderScaffolderTests|FullyQualifiedName~HubGeneratorTests|FullyQualifiedName~RuleSkipPathsTests|FullyQualifiedName~OffLimitsRuleTests"`.
   - Lane 3: `dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "FullyQualifiedName~TemplateGeneratorTests|FullyQualifiedName~TemplateUpdateTests|FullyQualifiedName~TemplateCommandTests|FullyQualifiedName~TemplateOverrideTests|FullyQualifiedName~ProcessWorkflowTests|FullyQualifiedName~WayfinderHarmonyTests|FullyQualifiedName~CodexSyncArtifactsE2ETests"`.
   - Lane 4: `dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "FullyQualifiedName~CommandDocConsistencyTests|FullyQualifiedName~ValidateCommandTests|FullyQualifiedName~DocumentationTests|FullyQualifiedName~InitCommandTests|FullyQualifiedName~CodexSyncArtifactsE2ETests"`.
2. Assert that help and completion data expose no `task` or `issue` command, and a newly initialized project contains no `project/tasks`, `project/issues`, `project/sprints`, `project/slices`, `project/campaigns`, or `project/backlog` scaffold. It must retain `project/future-features` as ideas-only documentation.
3. Run `dydo template update --diff` and `dydo sync`; generated role/skill artifacts must agree with their source templates and must require Linear Issue/Project context rather than repo PM files. No generated artifact may contain a command or instruction to create/update a repo task, Issue, Sprint, Slice, Campaign, or backlog record.
4. Run `dydo check` with zero errors, then scan active product, template, and generated surfaces. Historical Decisions, changelog, freeze evidence, disposition manifests, and archived v2 corpus are explicit exclusions; every remaining live occurrence of retired terms needs an approved non-PM meaning or is a failure.
5. Project 2 documents the FutureFeature contract but does not run strict record-content fixtures while existing records remain unnormalized. Project 3 owns the fixtures for an unpromoted idea, a valid terminal promotion with one Linear URL, and invalid combinations (workflow fields on an idea, missing URL on promoted, or multiple promoted targets), and no such test calls Linear.
6. Run the full isolated test suite and coverage-gap verification after serial integration. A clean independent reviewer must return PASS against this plan, followed by a Project-level integrated audit and an assimilation brief.

### Per-lane zero-consumer predicate

After Lanes 1–4, run the following predicate with the frozen-v2 compatibility exclusions in §1. It proves that the surviving runtime and generated active surface cannot consume the retired repository PM model; it does not claim the still-tracked v2 corpus or Project-5 sync schema has already disappeared.

```powershell
$retired = 'TaskCommand|TaskCreateHandler|TaskDoneHandler|TaskListHandler|TaskReviewHandler|IssueCommand|IssueCreateHandler|IssueListHandler|IssueResolveHandler|ReviewCommand|TaskFile|TaskStatus|IssueStatus|IssueSeverity|IssueFoundBy|GetTasksPath|GetIssuesPath|project/(tasks|issues|sprints|slices|campaigns|backlog)'
$roots = @('Program.cs','Commands','Models','Services','Utils','Rules','Serialization','Templates','dydo/_system/templates','.agents/skills','DynaDocs.Tests','README.md','dydo/index.md','dydo/reference','dydo/understand','dydo/guides','dydo.json') | Where-Object { Test-Path $_ }
$hits = @(& rg -n -i $retired $roots --glob '!Templates/sync-model.template.json' --glob '!DynaDocs.Tests/Sync/**' --glob '!DynaDocs.Tests/Rules/FrontmatterRuleTests.cs' --glob '!DynaDocs.Tests/Fixtures/**' --glob '!dydo/guides/migrating-dydo-1x-to-2x.md' --glob '!notion-sync.md' --glob '!notion-oss-survey.md')
if ($LASTEXITCODE -eq 0) { throw "Retired repo-PM consumer remains:`n$($hits -join "`n")" }
if ($LASTEXITCODE -ne 1) { throw "Retired-PM scan failed with exit $LASTEXITCODE" }
```

`DynaDocs.Tests/Rules/FrontmatterRuleTests.cs` is excluded because Project 3 alone removes the task-file
exception after the ratified manifest is applied. `DynaDocs.Tests/Fixtures/**` is excluded because it is a
retained historical audit-fixture corpus, not an active runtime surface and not Project-5-owned; it remains
unless a later reviewed Issue names an exact fixture deletion. Project 2 must prove that distinction rather
than relying on the exclusion:

```powershell
$fixtureConsumers = @(& rg -n 'DynaDocs\.Tests[\\/]Fixtures' Program.cs Commands Models Services Utils Rules Serialization DynaDocs.Tests --glob '!DynaDocs.Tests/Fixtures/**' --glob '!DynaDocs.Tests/Sync/**')
if ($LASTEXITCODE -eq 0) { throw "Historical fixture corpus has a live consumer:`n$($fixtureConsumers -join "`n")" }
if ($LASTEXITCODE -ne 1) { throw "Historical-fixture consumer scan failed with exit $LASTEXITCODE" }
$fixtureContentIncludes = @(& rg -n '<Content[^>]*(Include|Update)="Fixtures[\\/]|<None[^>]*(Include|Update)="Fixtures[\\/]' DynaDocs.Tests/DynaDocs.Tests.csproj)
if ($LASTEXITCODE -eq 0) { throw "Historical fixture corpus is included as active test content:`n$($fixtureContentIncludes -join "`n")" }
if ($LASTEXITCODE -ne 1) { throw "Historical-fixture content-include scan failed with exit $LASTEXITCODE" }
```

The existing `Sync\\Notion\\Fixtures` content include is intentionally outside this assertion and remains
Project-5-owned under the separately excluded `DynaDocs.Tests/Sync/**` subtree. No other
`DynaDocs.Tests/**` path is excluded.

After Lane 4, run `dydo template update --diff`, `dydo sync`, and the predicate again; capture zero proposed/generated legacy-work paths. Finally run `py DynaDocs.Tests/coverage/run_tests.py`, `py DynaDocs.Tests/coverage/gap_check.py --force-run`, `dydo check`, and `git diff --check`. All must exit zero; the coverage module count is recalculated after deletion rather than pinned.

## 6. Acceptance criteria

- Active dydo runtime, CLI, config, scaffolding, completion, templates, docs, skills, workflows, and tests no longer define or require file-backed Campaign/Sprint/Slice/Task/observed-Issue/backlog work objects.
- Linear's native Issue is the only actionable work record; Project plans and durable knowledge remain in Git with the reference rules in §1.
- FutureFeature remains a repo-native idea and only a human promotion records one stable Linear target; there is no sync or duplicate delivery state.
- No Linear client, token, schema, daemon, poller, webhook, Markdown mirror, or cache is added to dydo.
- Project 5's Notion/sync/watchdog owned paths remain untouched except for the explicit overlap landing order above.
- The Project 2 plan resource, governing SHA links, detailed Issue links, review, audit, and assimilation evidence pass the required Linear read-back.

## Related

- [DR 044 — Linear-Canonical PM and the dydo Knowledge Boundary](../decisions/044-linear-canonical-pm-and-dydo-knowledge-boundary.md) — Binding ontology and ownership decision.
- [dydo 3.0 Linear PM Migration](./dydo-3-linear-migration.md) — Portfolio sequence and Project 2 boundary.
- [dydo 3.0 Notion Runtime Removal and Release](./dydo-3-notion-runtime-removal.md) — Project 5 deletion ownership and serial handoff.
