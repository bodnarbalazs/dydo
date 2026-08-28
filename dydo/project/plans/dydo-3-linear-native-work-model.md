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
| 2 | `Program.cs`; delete `Commands/TaskCommand.cs`, `Commands/TaskCreateHandler.cs`, `Commands/TaskDoneHandler.cs`, `Commands/TaskListHandler.cs`, `Commands/TaskReviewHandler.cs`, `Commands/IssueCommand.cs`, `Commands/IssueCreateHandler.cs`, `Commands/IssueListHandler.cs`, `Commands/IssueResolveHandler.cs`, `Commands/ReviewCommand.cs`, `Models/TaskFile.cs`, `Models/TaskStatus.cs`, `Models/IssueStatus.cs`, `Models/IssueSeverity.cs`, `Models/IssueFoundBy.cs`; edit `Models/StructureConfig.cs`, `Services/ConfigService.cs`, `Services/IConfigService.cs`, `Services/ConfigFactory.cs`, `Services/FolderScaffolder.cs`, `Services/HubGenerator.cs`, `Commands/FixHubHandler.cs`, `Commands/HelpCommand.cs`, `Services/CompletionProvider.cs`, `Utils/PathUtils.cs`, and `dydo.json` | Delete `DynaDocs.Tests/Integration/IssueTests.cs`, `DynaDocs.Tests/Integration/TaskTests.cs`, `DynaDocs.Tests/Integration/WorkflowTests.cs`, and `DynaDocs.Tests/Models/TaskFileTests.cs`; rewrite `DynaDocs.Tests/Commands/CompleteCommandTests.cs`, `DynaDocs.Tests/Commands/CommandSmokeTests.cs`, `DynaDocs.Tests/Commands/CompletionsCommandTests.cs`, `DynaDocs.Tests/Commands/FixHubHandlerTests.cs`, `DynaDocs.Tests/Commands/GuardCommandTests.cs`, `DynaDocs.Tests/Commands/HelpCommandTests.cs`, `DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs`, `DynaDocs.Tests/Integration/GuardIntegrationTests.cs`, `DynaDocs.Tests/Integration/InitCommandTests.cs`, `DynaDocs.Tests/Integration/InitCheckIntegrationTests.cs`, `DynaDocs.Tests/Integration/FixCommandIntegrationTests.cs`, `DynaDocs.Tests/Services/CompletionProviderTests.cs`, `DynaDocs.Tests/Services/ConfigFactoryTests.cs`, `DynaDocs.Tests/Services/ConfigServiceTests.cs`, `DynaDocs.Tests/Services/ConfigurablePathsTests.cs`, `DynaDocs.Tests/Services/DocScannerTests.cs`, `DynaDocs.Tests/Services/FolderScaffolderTests.cs`, `DynaDocs.Tests/Services/HubGeneratorTests.cs`, `DynaDocs.Tests/Utils/RuleSkipPathsTests.cs`, and `DynaDocs.Tests/Rules/OffLimitsRuleTests.cs`. In `DocScannerTests`, remove only the obsolete `IConfigService.GetTasksPath`/`GetIssuesPath` fake members after Lane 2 deletes those interface methods; Lane 1 continues to own its scanner behavior coverage. Replace only the retired task/issue/review command and nudge assertions in `FixHubHandlerTests`, `CliEndToEndTests`, `GuardCommandTests`, and `GuardIntegrationTests`; preserve their surviving generic coverage. Prove removal leaves no local PM command/model/config consumer. |
| 3 | delete `Templates/_tasks.template.md`, `Templates/_issues.template.md`, and `Templates/_backlog.template.md`; edit `Templates/_project.template.md`, `Templates/about-dynadocs.template.md`, `Templates/dydo-commands.template.md`, `Templates/architecture.template.md`, `Templates/index.template.md`, `Templates/mode-bro.template.md`, `Templates/mode-chief-of-staff.template.md`, `Templates/mode-co-thinker.template.md`, `Templates/mode-code-writer.template.md`, `Templates/mode-docs-writer.template.md`, `Templates/mode-grilling.template.md`, `Templates/mode-inquisitor.template.md`, `Templates/mode-orchestrator.template.md`, `Templates/mode-planner.template.md`, `Templates/mode-reviewer.template.md`, `Templates/mode-self-improvement.template.md`, `Templates/mode-test-writer.template.md`, `Templates/mode-wayfinder.template.md`, `Templates/reviewer-resource-code.template.md`, `Templates/reviewer-resource-docs.template.md`, `Templates/reviewer-resource-merge-sprint.template.md`, `Templates/reviewer-resource-plan.template.md`, `Templates/reviewer-resource-tests.template.md`, `Templates/workflow-run-sprint.js`, `Templates/workflow-inquisition.js`, and `Services/TemplateGenerator.cs`; regenerate `dydo/_system/templates/**`, `.agents/skills/**`, and the exact retrospective Claude/Codex outputs below with the product command | Rewrite `DynaDocs.Tests/Services/TemplateGeneratorTests.cs`, `DynaDocs.Tests/Services/TemplateUpdateTests.cs`, `DynaDocs.Tests/Integration/TemplateCommandTests.cs`, `DynaDocs.Tests/Integration/TemplateOverrideTests.cs`, `DynaDocs.Tests/Integration/ProcessWorkflowTests.cs`, `DynaDocs.Tests/Commands/WayfinderHarmonyTests.cs`, and `DynaDocs.Tests/Integration/CodexSyncArtifactsE2ETests.cs`; apply only the four bounded `DynaDocs.Tests/Commands/SyncCommandTests.cs` expectation hunks below. Prove source templates, installed framework files, and compiled skills agree. |
| 4 | `README.md`; `dydo/index.md`; `dydo/project/_index.md`; `dydo/reference/_index.md`; `dydo/reference/about-dynadocs.md`; `dydo/reference/configuration.md`; `dydo/reference/dydo-commands.md`; `dydo/understand/about.md`; `dydo/understand/architecture.md`; `dydo/understand/documentation-model.md`; `dydo/understand/templates-and-customization.md`; `dydo/guides/getting-started.md`; `dydo/guides/customizing-roles.md`; `dydo/guides/testing-strategy.md`; `dydo/guides/troubleshooting.md`; `dydo/guides/adding-a-command.md`; `dydo/guides/how-to-use-docs.md` | Rewrite `DynaDocs.Tests/Commands/CommandDocConsistencyTests.cs`, `DynaDocs.Tests/Commands/ValidateCommandTests.cs`, `DynaDocs.Tests/Integration/DocumentationTests.cs`, `DynaDocs.Tests/Integration/InitCommandTests.cs`, and `DynaDocs.Tests/Integration/CodexSyncArtifactsE2ETests.cs`; run `dydo check` against a fresh initialized fixture and prove no legacy hierarchy is scaffolded. |
| 5 | new authored `dydo/project/migrations/3.0-linear-work-model.md` and `dydo/project/migrations/3.0-linear-work-model-assimilation.md`; generated `dydo/project/migrations/_index.md` | No production or test edit. Run the integrated commands and capture their exact exits/read-back as evidence; retain only the command-produced migrations hub from generated hub changes. |

#### Retrospective DYD-20 authorization

This amendment is retrospective governance correction for the already merged DYD-20 commit
`df2c33a0323b1b5daf41fc7e1f93d6a19161ee76`; it is not prior authorization and does not reopen
DYD-20. In addition to the paths already authorized above, it authorizes exactly these 25 paths and no
wildcard or category substitute.

Deterministic `dydo sync` outputs, exactly 23:

1. `.claude/agents/inquisitor.md`
2. `.claude/agents/reviewer.md`
3. `.claude/skills/chief-of-staff/SKILL.md`
4. `.claude/skills/co-thinker/SKILL.md`
5. `.claude/skills/code-writer/SKILL.md`
6. `.claude/skills/docs-writer/SKILL.md`
7. `.claude/skills/grilling/SKILL.md`
8. `.claude/skills/inquisitor/SKILL.md`
9. `.claude/skills/orchestrator/SKILL.md`
10. `.claude/skills/planner/SKILL.md`
11. `.claude/skills/reviewer/SKILL.md`
12. `.claude/skills/reviewer/resources/code.md`
13. `.claude/skills/reviewer/resources/docs.md`
14. `.claude/skills/reviewer/resources/merge-sprint.md`
15. `.claude/skills/reviewer/resources/plan.md`
16. `.claude/skills/reviewer/resources/tests.md`
17. `.claude/skills/self-improvement/SKILL.md`
18. `.claude/skills/test-writer/SKILL.md`
19. `.claude/skills/wayfinder/SKILL.md`
20. `.claude/workflows/inquisition.js`
21. `.claude/workflows/run-sprint.js`
22. `.codex/agents/inquisitor.toml`
23. `.codex/agents/reviewer.toml`

Bounded test correction, exactly one path: `DynaDocs.Tests/Commands/SyncCommandTests.cs`, and only its
four `df2c33a0` expectation hunks: the workflow/inquisition Linear-Issue and Project-audit contract;
Wayfinder's committed-Project, atomic-Issue, non-work-object ontology; the Tier-1 manager
reviewed-intent doctrine; and the orchestrator's Linear-Issue/native-delegation/integrated-audit
methodology.

Source-parity correction, exactly one path: `Templates/how-to-use-docs.template.md`, and only its one
`df2c33a0` ontology correction from repository tasks to Decisions, reviewed plans, audits, changelog,
and pitfalls. Its generated counterpart is `dydo/guides/how-to-use-docs.md`.

The integrated audit compares the exact paths changed in
`1cf75a219e4a7a30397174e0ab79f4aff1326547..5cceda39657d9023c7c456b4f754e594f7cd0410`
under the `.claude` and `.codex` scan roots plus the two bounded authored paths against this literal
allow-set and must report `allowed=25; unexpected=0`. For each of the 25 paths, `git log` over that
range must return exactly the single introducing commit
`df2c33a0323b1b5daf41fc7e1f93d6a19161ee76`.

Parity is mandatory: the 17 exact Claude skill/resource Git blobs numbered 3–19 are identical to the
same relative paths under `.agents/skills/`; the Git blobs for `Templates/workflow-inquisition.js` and
`.claude/workflows/inquisition.js` are identical; and the Git blobs for
`Templates/workflow-run-sprint.js` and `.claude/workflows/run-sprint.js` are identical. A source-built
`dydo sync` in a disposable copy must mechanically
reproduce all four agent outputs numbered 1–2 and 22–23 from the already authorized
`Templates/mode-inquisitor.template.md` and `Templates/mode-reviewer.template.md` role descriptions
after their authorized `dydo template update` installation. The authorized `SyncCommandTests.cs` diff,
rendered with `git diff --unified=5`, remains exactly four bounded expectation hunks, and the Git blobs
for `Templates/how-to-use-docs.template.md` and
`dydo/guides/how-to-use-docs.md` remain identical. Any mismatch fails the Project-level integrated audit.

Generated paths are never hand-edited. `dydo template update` owns `dydo/_system/templates/**`, and
`dydo sync` owns `.agents/skills/**` plus the 23 exact Claude/Codex outputs listed above; a generated
change outside those commands fails the lane.

## 3. Delivery lanes and ownership

Every lane begins from the published reviewed plan commit in an isolated worktree. Lanes create no Linear objects until the plan gate below passes and their detailed Linear Issues have governing context, exact owned paths, and gates.

| Lane | Outcome and exclusive ownership | Depends on |
|---|---|---|
| 1. Doctrine and compatibility contract | The Lane-1 exact paths in §2. Defines the target terms and FutureFeature state/reference rules while preserving the frozen corpus's task exception and issue vocabulary until Project 3 applies the manifest. | Project 1 accepted; this plan PASS |
| 2. Retire local work runtime | The exact `Program.cs`, `Commands/*Task*`, `Commands/Issue*.cs`, Models, config/scaffold/completion/path files, `dydo.json`, and their direct tests named in §2. Deletes local task/issue command and model surfaces without introducing a Linear client. | Lane 1 |
| 3. Templates, skills, and workflows | The mode/resource/workflow templates in §2, generated `.agents/skills/**`, the 23 exact retrospective Claude/Codex outputs in §2, and their template/skill/workflow tests. Replaces record-based planning/orchestration/review/audit language with reviewed Linear Issue/Project-plan contracts. | Lane 1; Lane 2's command names settled |
| 4. Active product docs and integration proof | `README.md`, active reference/understand/guides whose content is generated from or complements Lanes 1–3, plus command-doc/completion/init/validation integration tests. Removes live PM claims and verifies a fresh scaffold contains no repo work hierarchy. | Lanes 2–3 |
| 5. Serial integration and audit | Author `dydo/project/migrations/3.0-linear-work-model.md` and `dydo/project/migrations/3.0-linear-work-model-assimilation.md`; generate only `dydo/project/migrations/_index.md`; perform evidence updates, conflict resolution, full gates, and independent review. No unrelated migration corpus file or generated hub is absorbed. | Lanes 1–4; this retrospective amendment merged |

### Owned path overlap with Project 5

Project 2 lands first and Project 5 lands second for every overlap below. Project 5 may remove residual Notion/sync behavior only after taking the then-current Project 2 result; it must not restore a retired repo-PM surface.

| Shared path | Project 2 first | Project 5 second |
|---|---|---|
| `Program.cs`, `Commands/HelpCommand.cs`, `Services/CompletionProvider.cs`, `Services/ConfigFactory.cs`, `DynaDocs.Tests/Commands/CommandSmokeTests.cs`, `DynaDocs.Tests/Commands/CommandDocConsistencyTests.cs`, `DynaDocs.Tests/Services/CompletionProviderTests.cs`, `DynaDocs.Tests/Services/ConfigFactoryTests.cs` | Remove task/issue/review command semantics and expectations. | Remove Notion/watchdog seams from the surviving command/config contract. |
| `Services/FolderScaffolder.cs`, `DynaDocs.Tests/Services/FolderScaffolderTests.cs`, `DynaDocs.Tests/Services/ConfigServiceTests.cs`, `DynaDocs.Tests/Integration/TemplateCommandTests.cs`, `DynaDocs.Tests/Integration/TemplateOverrideTests.cs` | Remove task/issue/backlog scaffold and configuration expectations. | Remove sync-model/template-config expectations after the Project-2 result lands. |
| `DynaDocs.Tests/Integration/GuardIntegrationTests.cs` | Remove only retired task/issue command-nudge assertions while preserving generic guard coverage. | Remove only the watchdog command case while preserving the Project-2 result and generic shell/guard coverage. |
| `dydo.json` | Retire `structure.tasks`, `structure.issues`, and task/issue nudge vocabulary after its current dirty-tree owner is resolved. | Remove Notion config and sync-model/watchdog hash remnants. |
| `Services/TemplateGenerator.cs`, `DynaDocs.Tests/Services/TemplateGeneratorTests.cs`, `DynaDocs.Tests/Services/TemplateUpdateTests.cs` | Establish Linear/Git PM template prose and compiled-artifact expectations. | Remove sync-model/Notion/watchdog generation and final product wording. |
| `README.md`, `dydo/reference/_index.md`, `dydo/reference/dydo-commands.md`, `dydo/reference/about-dynadocs.md`, `dydo/understand/about.md`, `dydo/understand/architecture.md`, `dydo/understand/work-model.md`, `dydo/guides/orchestration-pitfalls.md`, `Templates/about-dynadocs.template.md`, `Templates/dydo-commands.template.md` | Establish the Linear-native work model. | Remove remaining active Notion claims and publish migration/release guidance. |
| `Utils/FrontmatterParser.cs`, `DynaDocs.Tests/Utils/FrontmatterParserTests.cs` | Preserve the parser and update only its repository-PM expectations if needed; do not delete either path. | Retain both paths as native-compiler implementation and coverage; only clean stale sync-specific commentary after proving the three named consumers. |

Any additional overlap requires reviewed amendments to both plans before a later Lane enters `Todo`.

## 4. Evidence, migration, and verification

Lane 5 authors `dydo/project/migrations/3.0-linear-work-model.md` and `dydo/project/migrations/3.0-linear-work-model-assimilation.md`. The former records the published plan SHA/permalink; the exact Linear Project 2 URL; Project-resource read-back showing that URL is attached; each detailed Linear Issue ID/URL and governing SHA; generated-skill/template update evidence; scan counts; and test command exits. The latter records the independent review verdict, integrated-audit result, observed friction, adopted changes, and deferred follow-ups. Neither file copies Linear workflow state.

After both records exist, Lane 5 runs `dotnet build DynaDocs.csproj -c Release` and, from the repository
root, the source-built corpus-scoped command `dotnet bin/Release/net10.0/dydo.dll fix`. It retains the
command-produced `dydo/project/migrations/_index.md` byte-for-byte only when that hub has exactly five
Markdown links in filename order: `./3.0-linear-bootstrap.md`,
`./3.0-linear-work-model-assimilation.md`, `./3.0-linear-work-model.md`,
`./3.0-notion-freeze.md`, and `./3.0-pm-records.md`. It rejects or restores every other generated hub
change, and `dydo/project/tasks/_index.md` must remain absent. `dydo/project/_index.md`, every plans
hub, historical record contents, production, and tests remain unchanged.

The final DYD-16 implementation diff is exactly three paths: the two authored migration records and
the generated migrations hub. Its total Project-integration branch diff against
`5cceda39657d9023c7c456b4f754e594f7cd0410` is exactly four paths including this governing plan
amendment. The source-built `dotnet bin/Release/net10.0/dydo.dll check` must exit zero with zero errors
and no migrations/current-plan orphan warning before review or audit can pass.

Before Project 3 begins, the integrator must use the official Linear connector to resolve Project 2 by the fixed ID `44eba9ff-0242-4179-b94b-932339b364fd`, assert team ID `caa6ccbf-4f9b-477e-826c-a51ed43b0687`, assert its URL equals the frontmatter URL, and read back an attached published GitHub plan resource whose URL equals the published plan permalink. For each Project-2 Issue, read back its ID, identifier, Project ID, team ID, governing-commit permalink attachment, and its actual `Done` status before the Project is completed. This is a one-time audit of references, not a dydo runtime feature. Project 3 then uses the Project 1 disposition manifest to remove legacy record files and rewrite incoming links; Project 2 does not silently remove them.

## 5. Tests and acceptance gates

1. Each lane first runs `dotnet build DynaDocs.Tests/DynaDocs.Tests.csproj -c Release`, then its exact command below. Deleted tests are not run: their required absence is verified by the lane's zero-consumer predicate and build.

   - Lane 1: `dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "FullyQualifiedName~LegacyPmManifestServiceTests|FullyQualifiedName~LegacyPmRecordRuleTests|FullyQualifiedName~SummaryRuleTests|FullyQualifiedName~BrokenLinksRuleTests|FullyQualifiedName~HubFilesRuleTests|FullyQualifiedName~OrphanDocsRuleTests|FullyQualifiedName~FolderMetaFilesRuleTests|FullyQualifiedName~DocScannerTests"`.
   - Lane 2: `dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "FullyQualifiedName~CompleteCommandTests|FullyQualifiedName~CommandSmokeTests|FullyQualifiedName~CompletionsCommandTests|FullyQualifiedName~FixHubHandlerTests|FullyQualifiedName~GuardCommandTests|FullyQualifiedName~HelpCommandTests|FullyQualifiedName~CliEndToEndTests|FullyQualifiedName~GuardIntegrationTests|FullyQualifiedName~InitCommandTests|FullyQualifiedName~InitCheckIntegrationTests|FullyQualifiedName~FixCommandIntegrationTests|FullyQualifiedName~CompletionProviderTests|FullyQualifiedName~ConfigFactoryTests|FullyQualifiedName~ConfigServiceTests|FullyQualifiedName~ConfigurablePathsTests|FullyQualifiedName~DocScannerTests|FullyQualifiedName~FolderScaffolderTests|FullyQualifiedName~HubGeneratorTests|FullyQualifiedName~RuleSkipPathsTests|FullyQualifiedName~OffLimitsRuleTests"`.
   - Lane 3: `dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "FullyQualifiedName~TemplateGeneratorTests|FullyQualifiedName~TemplateUpdateTests|FullyQualifiedName~TemplateCommandTests|FullyQualifiedName~TemplateOverrideTests|FullyQualifiedName~ProcessWorkflowTests|FullyQualifiedName~WayfinderHarmonyTests|FullyQualifiedName~CodexSyncArtifactsE2ETests"`.
   - Lane 4: `dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "FullyQualifiedName~CommandDocConsistencyTests|FullyQualifiedName~ValidateCommandTests|FullyQualifiedName~DocumentationTests|FullyQualifiedName~InitCommandTests|FullyQualifiedName~CodexSyncArtifactsE2ETests"`.
2. Assert that help and completion data expose no `task` or `issue` command, and a newly initialized project contains no `project/tasks`, `project/issues`, `project/sprints`, `project/slices`, `project/campaigns`, or `project/backlog` scaffold. It must retain `project/future-features` as ideas-only documentation.
3. Run `dydo template update --diff` and `dydo sync`; generated role/skill artifacts must agree with their source templates and must require Linear Issue/Project context rather than repo PM files. No generated artifact may contain a command or instruction to create/update a repo task, Issue, Sprint, Slice, Campaign, or backlog record.
4. Run `dydo check` with zero errors, then scan active product, template, and generated surfaces. Historical Decisions, changelog, freeze evidence, disposition manifests, and archived v2 corpus are explicit exclusions; every remaining live occurrence of retired terms needs an approved non-PM meaning or is a failure.
5. Project 2 documents the FutureFeature contract but does not run strict record-content fixtures while existing records remain unnormalized. Project 3 owns the fixtures for an unpromoted idea, a valid terminal promotion with one Linear URL, and invalid combinations (workflow fields on an idea, missing URL on promoted, or multiple promoted targets), and no such test calls Linear.
6. Run the full isolated test suite and coverage-gap verification after serial integration. A clean independent reviewer must return PASS against this plan, followed by a Project-level integrated audit and an assimilation brief.

### Per-lane zero-consumer predicate

After all of Lanes 1–4 have landed, run the following predicate with the frozen-v2 compatibility exclusions in §1. Earlier lanes report expected hits in exact later-lane-owned paths rather than crossing ownership to remove them. The integrated predicate proves that the surviving runtime and generated active surface cannot consume the retired repository PM model; it does not claim the still-tracked v2 corpus or Project-5 sync schema has already disappeared.

```powershell
$retired = 'TaskCommand|TaskCreateHandler|TaskDoneHandler|TaskListHandler|TaskReviewHandler|IssueCommand|IssueCreateHandler|IssueListHandler|IssueResolveHandler|ReviewCommand|TaskFile|TaskStatus|IssueStatus|IssueSeverity|IssueFoundBy|GetTasksPath|GetIssuesPath|project/(tasks|issues|sprints|slices|campaigns|backlog)'
$roots = @('Program.cs','Commands','Models','Services','Utils','Rules','Serialization','Templates','dydo/_system/templates','.agents/skills','.claude','.codex','DynaDocs.Tests','README.md','dydo/index.md','dydo/reference','dydo/understand','dydo/guides','dydo.json') | Where-Object { Test-Path $_ }
$hits = @(& rg -n -i $retired $roots --glob '!Templates/sync-model.template.json' --glob '!DynaDocs.Tests/Sync/**' --glob '!DynaDocs.Tests/Rules/FrontmatterRuleTests.cs' --glob '!DynaDocs.Tests/Fixtures/**' --glob '!dydo/guides/migrating-dydo-1x-to-2x.md' --glob '!notion-sync.md' --glob '!notion-oss-survey.md')
if ($LASTEXITCODE -notin 0, 1) { throw "Retired-PM scan failed with exit $LASTEXITCODE" }
$allowedRetiredMatches = @(
    @{ Path = 'DynaDocs.Tests/Services/LegacyPmManifestServiceTests.cs'; Text = '[InlineData("{\"path\":\"dydo/project/tasks/one.md\"}")]' }
    @{ Path = 'DynaDocs.Tests/Services/LegacyPmManifestServiceTests.cs'; Text = '[InlineData("{\"path\":\"dydo/project/tasks/one.md\",\"executionState\":1}")]' }
    @{ Path = 'DynaDocs.Tests/Services/LegacyPmManifestServiceTests.cs'; Text = '[InlineData("{\"path\":\"dydo/project/tasks/one.md\",\"executionState\":\"unknown\"}")]' }
    @{ Path = 'DynaDocs.Tests/Services/TemplateGeneratorTests.cs'; Text = 'Assert.DoesNotContain("project/tasks", content, StringComparison.OrdinalIgnoreCase);' }
    @{ Path = 'DynaDocs.Tests/Services/TemplateGeneratorTests.cs'; Text = 'Assert.DoesNotContain("project/issues", content, StringComparison.OrdinalIgnoreCase);' }
)
$allowedKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($allowed in $allowedRetiredMatches) {
    $key = "$($allowed.Path)`0$($allowed.Text)"
    if (-not $allowedKeys.Add($key)) { throw "Duplicate retired-PM allow tuple: $($allowed.Path): $($allowed.Text)" }
}
$allowedCounts = @{}
$unexpectedHits = [Collections.Generic.List[string]]::new()
foreach ($hit in $hits) {
    if ($hit -notmatch '^(?<path>.*?):(?<line>\d+):(?<text>.*)$') { throw "Unparseable retired-PM scan hit: $hit" }
    $path = $Matches.path.Replace('\', '/')
    $text = $Matches.text.Trim()
    $key = "$path`0$text"
    if ($allowedKeys.Contains($key)) {
        $allowedCounts[$key] = 1 + ($allowedCounts[$key] ?? 0)
    } else {
        $unexpectedHits.Add($hit)
    }
}
$invalidAllowed = @($allowedRetiredMatches | Where-Object {
    $key = "$($_.Path)`0$($_.Text)"
    ($allowedCounts[$key] ?? 0) -ne 1
})
if ($invalidAllowed.Count -gt 0) {
    throw "Retired-PM proof tuples missing or duplicated:`n$($invalidAllowed | ForEach-Object { "$($_.Path): $($_.Text)" } | Out-String)"
}
$allowedCount = @($allowedCounts.Values | Measure-Object -Sum).Sum
if ($allowedCount -ne 5 -or $unexpectedHits.Count -ne 0) {
    throw "Retired repo-PM consumer remains (allowed=$allowedCount; unexpected=$($unexpectedHits.Count)):`n$($unexpectedHits -join "`n")"
}
Write-Output "Retired-PM scan: allowed=5; unexpected=0."
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

After Lane 4, run `dydo template update --diff`, `dydo sync`, and the predicate again, including the
`.claude` and `.codex` roots; capture zero unexpected retired-PM hits. Lane 5 then performs the exact
25-path provenance/parity safeguards above and its migrations-hub procedure. Finally run
`py DynaDocs.Tests/coverage/run_tests.py`, `py DynaDocs.Tests/coverage/gap_check.py --force-run`,
source-built `dotnet bin/Release/net10.0/dydo.dll check`, link/plan consistency checks, and
`git diff --check`. All must exit zero, the coverage module count is recalculated after deletion rather
than pinned, and a fresh independent plan review of this exact single-file amendment diff must return
strict PASS before DYD-16 resumes.

## 6. Acceptance criteria

- Active dydo runtime, CLI, config, scaffolding, completion, templates, docs, skills, workflows, and tests no longer define or require file-backed Campaign/Sprint/Slice/Task/observed-Issue/backlog work objects.
- Linear's native Issue is the only actionable work record; Project plans and durable knowledge remain in Git with the reference rules in §1.
- FutureFeature remains a repo-native idea and only a human promotion records one stable Linear target; there is no sync or duplicate delivery state.
- No Linear client, token, schema, daemon, poller, webhook, Markdown mirror, or cache is added to dydo.
- Project 5's Notion/sync/watchdog owned paths remain untouched except for the explicit overlap landing order above.
- The retrospective DYD-20 exception is exactly 25 paths with `allowed=25; unexpected=0`, exact
  `df2c33a0` provenance, and all required source/generated parity; it creates no prospective wildcard.
- Lane 5 owns only two authored migration records and one generated migrations hub; the final DYD-16
  implementation diff is three paths and its total Project-integration branch diff is four paths including
  this amendment.
- The Project 2 plan resource, governing SHA links, detailed Issue links, review, audit, and assimilation evidence pass the required Linear read-back.

## 7. Chronological DYD-22 generated-navigation amendment

This plan-only amendment was discovered after the DYD-21 amendment had merged at
`c4aa06c0d44174c0fb341908a2573a68fd31bd7e`. It is new authorization only from this amendment's own
reviewed merge forward: it does not rewrite history, retroactively authorize an earlier Lane-5 action,
or imply that DYD-16 could retain either additional hub under the preceding contract. DYD-16 remains
blocked until the exact single-file DYD-22 amendment receives independent plan review, merges, and is
read back from the integration branch; only then may DYD-16 resume from the cumulatively amended plan.

### Additional generated ownership and provenance

In addition to the already authorized `dydo/project/migrations/_index.md`, Lane 5 may retain exactly
two further command-produced outputs: `dydo/understand/_index.md` and `dydo/guides/_index.md`. This
amendment authorizes no source-document edit and no other production, test, generated hub, evidence
record, historical corpus, sync, or configuration path. Generated hubs are never hand-edited.

The additional ownership repairs exactly three verified stale navigation entries introduced by
`c22a8a69c73349b6e8ff7c9bc5cb909fde010821`. Their canonical sources were already corrected by
`38c70b63fb984aa8c91d11606bcee994d5111801`; regeneration must reflect those existing sources rather
than changing them:

1. In `dydo/understand/_index.md`, replace the stale **Task Lifecycle** tasks-flow entry with exactly
   `- [Linear Issue Lifecycle](./task-lifecycle.md) - Actionable work lives in Linear Issues.`, derived
   from `dydo/understand/task-lifecycle.md`.
2. In `dydo/understand/_index.md`, replace the stale **Work Model**
   Slice → Sprint → Campaign → Release entry with exactly
   `- [Work Model](./work-model.md) - Linear owns dydo's live work graph; Git owns durable knowledge and proof.`,
   derived from `dydo/understand/work-model.md`.
3. In `dydo/guides/_index.md`, replace the stale **Writing Good Briefs** slice-file implementation-detail
   entry with exactly
   `- [Writing Good Briefs](./writing-good-briefs.md) - The self-containment bar for a Linear Issue, Project-plan lane, or prompt handed to a fresh agent.`,
   derived from `dydo/guides/writing-good-briefs.md`.

### Superseding Lane-5 generation and scope contract

For execution after this amendment merges, this subsection supersedes only the conflicting Lane-5
generated-output and path-count limits above. After the two authored evidence records exist, DYD-16
must perform the following generation procedure from the repository root:

1. Run `dotnet build DynaDocs.csproj -c Release` and require success.
2. Run the source-built `dotnet bin/Release/net10.0/dydo.dll fix` from the repository root.
3. Retain only the command-produced `dydo/project/migrations/_index.md`,
   `dydo/understand/_index.md`, and `dydo/guides/_index.md`; restore or reject every other
   command-produced change. Never hand-edit any retained hub.
4. In a disposable checkout at the exact candidate commit, repeat the same Release build and repo-root
   source-built `fix` command. Prove each of the three retained hubs is byte-identical to its disposable
   exact-candidate counterpart.
5. Prove `dydo/project/migrations/_index.md` contains exactly five Markdown links in filename order:
   `./3.0-linear-bootstrap.md`, `./3.0-linear-work-model-assimilation.md`,
   `./3.0-linear-work-model.md`, `./3.0-notion-freeze.md`, and `./3.0-pm-records.md`.
6. Assert the two navigation hubs contain the three exact corrected entries above and none of their
   stale tasks-flow, Slice → Sprint → Campaign → Release, or slice-file implementation-detail
   forms.
7. Prove `dydo/project/_index.md` is unchanged and `dydo/project/tasks/_index.md` remains absent.

The final DYD-16 implementation diff is exactly these five paths:

1. `dydo/project/migrations/3.0-linear-work-model.md`
2. `dydo/project/migrations/3.0-linear-work-model-assimilation.md`
3. `dydo/project/migrations/_index.md`
4. `dydo/understand/_index.md`
5. `dydo/guides/_index.md`

The total Project-integration diff from `5cceda39657d9023c7c456b4f754e594f7cd0410` is exactly six paths:
the five implementation paths above and the cumulatively amended
`dydo/project/plans/dydo-3-linear-native-work-model.md`. Any additional path fails scope.

After generation and exact scope checks, DYD-16 must rerun the amended retired-PM scan, the
source-built `dotnet bin/Release/net10.0/dydo.dll check`, `git diff --check`, a fresh independent
documentation review, and the strict Project-level integrated audit. Each gate must return PASS before
DYD-16 may complete.

## Related

- [DR 044 — Linear-Canonical PM and the dydo Knowledge Boundary](../decisions/044-linear-canonical-pm-and-dydo-knowledge-boundary.md) — Binding ontology and ownership decision.
- [dydo 3.0 Linear PM Migration](./dydo-3-linear-migration.md) — Portfolio sequence and Project 2 boundary.
- [dydo 3.0 Notion Runtime Removal and Release](./dydo-3-notion-runtime-removal.md) — Project 5 deletion ownership and serial handoff.
