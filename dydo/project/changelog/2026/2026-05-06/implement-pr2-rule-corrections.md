---
area: general
type: changelog
date: 2026-05-06
---

# Task: implement-pr2-rule-corrections

Review PR2 of the dydo-check-drift batch (4 commits on master, 0 in PR-isolation).

COMMITS (oldest first; PR1 is included as commit 1 because it had been sitting uncommitted in the working tree all session — see Adele/Charlie correspondence):
- fc83e31 feat(check): scan boundary + RuleBase ShouldSkip + RuleSkipPaths helper (#0163)  [PR1, authored by Brian, review-passed by Charlie before this branch]
- 3213931 feat(check): types.json vocabulary + inquisition type (#0159, D1)
- 8b71cd4 feat(check): rule skip moves — template-additions, project/tasks (#0160, #0162)
- d05f696 feat(hubs): drop tasks/_index auto-gen + project hub Tasks prose (D4)

PLAN: dydo/agents/Brian/archive/20260504-215742/plan-dydo-check-drift.md ('PR2' section). Locked decisions D1–D5 are in the 'Resolved decisions' index at top.

PR1 NOTE: I (Charlie) was the original reviewer for PR1 in a prior session and passed it (see dydo/project/tasks/pr1-scan-boundary.md). Adele's brief said it was 'on master' but it actually sat uncommitted in the working tree. I lifted it into git as commit 1 of this branch with the original author/reviewer attribution in the commit body. If you re-review PR1 substantively, that's fine — but it's not strictly the new work in this batch.

PR2 SUBSTANCE:
- D1: types.json embedded baseline + IFrontmatterTypesService merging baseline ∪ user-added entries (case-sensitive, AOT-clean via TypesJsonContext source-gen). EnsureTypesJson in TemplateCommand.ExecuteUpdate. FolderScaffolder writes types.json on init. Inquisition added to baseline ValidTypes.
- #0160: SummaryRule top-of-Validate skip via RuleSkipPaths.IsTemplateOrAddition. Also moves FrontmatterRule, BrokenLinksRule, NamingRule from inline StartsWith blocks to the same helper.
- #0162: OrphanDocsRule returns [] for project/tasks/<file>.md when filename does not start with _ (folder-meta still validated). HubFilesRule skips project/tasks folder (Brian's surfaced surprise #1: HubFilesRule would otherwise error after D4).
- D4: HubGenerator drops project/tasks from foldersToProcess and from GetSubfolderHubs; appends a hardcoded ## Tasks section to the project hub. FixHubHandler deletes a stale project/tasks/_index.md when it carries the auto-gen banner. _project.template.md / _issues.template.md / _changelog.template.md prose updated to drop ../tasks/_index.md links.

VERIFICATION GATE:
- dotnet build: clean (0 warnings, 0 errors).
- python DynaDocs.Tests/coverage/run_tests.py: 4111/4111 pass (was ~4076 before; +35 net new tests across PR1+PR2).
- python DynaDocs.Tests/coverage/gap_check.py: 140/140 modules pass tier requirements (T1).
- dydo check on the dydo project itself: NOT performed; per Adele's reply, this is a soft-pass for this wave because the dev binary is not on PATH (the residual broken links in dydo/project/_index.md, _issues.md, _changelog.md will be repaired by the next dydo template update + dydo fix once the new binary ships — same convention as #0166). The Verification gate item 1 from Brian's plan ('0/0 on dydo project') is therefore deferred to release-time verification.

KEY DECISIONS WORTH SCRUTINY:
- IFrontmatterTypesService is constructed once per check pass in CheckDocValidator (single-load semantics; the cache is per-instance, not static, so concurrent check runs each get fresh state).
- FrontmatterRule constructor takes IFrontmatterTypesService as optional (nullable) so the existing parameterless 'new FrontmatterRule()' in tests still works (falls back to baseline ValidTypes).
- HubGenerator.AutoGenComment is now public so FixHubHandler.DeleteStaleTasksIndex can compare against it. Cleaner than re-declaring the constant.
- Templates/types.json uses a leading // comment block; FrontmatterTypesService and TemplateCommand both parse with ReadCommentHandling.Skip + AllowTrailingCommas via the AOT-friendly TypesJsonContext source-gen.
- FixHubHandler.DeleteStaleTasksIndex only deletes when the auto-gen banner is present (banner check guards against clobbering a hand-written file).

CONSTRAINTS FOR YOU:
- Soft-pass per Adele's note: gap_check parallelism flakes (#0167 territory) are out of scope; my diff doesn't touch racing code. Both my full and re-run gap_check came back clean (140/140 + 4111/4111 tests).
- Don't relitigate architecture (D1–D5 are locked).
- If you find a concrete implementation reason something won't work, surface it.

REPORT BACK to Adele on task implement-pr2-rule-corrections (per the dispatch flow, your reply on the task fulfills my origin-side obligation).

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review PR2 of the dydo-check-drift batch (4 commits on master, 0 in PR-isolation).

COMMITS (oldest first; PR1 is included as commit 1 because it had been sitting uncommitted in the working tree all session — see Adele/Charlie correspondence):
- fc83e31 feat(check): scan boundary + RuleBase ShouldSkip + RuleSkipPaths helper (#0163)  [PR1, authored by Brian, review-passed by Charlie before this branch]
- 3213931 feat(check): types.json vocabulary + inquisition type (#0159, D1)
- 8b71cd4 feat(check): rule skip moves — template-additions, project/tasks (#0160, #0162)
- d05f696 feat(hubs): drop tasks/_index auto-gen + project hub Tasks prose (D4)

PLAN: dydo/agents/Brian/archive/20260504-215742/plan-dydo-check-drift.md ('PR2' section). Locked decisions D1–D5 are in the 'Resolved decisions' index at top.

PR1 NOTE: I (Charlie) was the original reviewer for PR1 in a prior session and passed it (see dydo/project/tasks/pr1-scan-boundary.md). Adele's brief said it was 'on master' but it actually sat uncommitted in the working tree. I lifted it into git as commit 1 of this branch with the original author/reviewer attribution in the commit body. If you re-review PR1 substantively, that's fine — but it's not strictly the new work in this batch.

PR2 SUBSTANCE:
- D1: types.json embedded baseline + IFrontmatterTypesService merging baseline ∪ user-added entries (case-sensitive, AOT-clean via TypesJsonContext source-gen). EnsureTypesJson in TemplateCommand.ExecuteUpdate. FolderScaffolder writes types.json on init. Inquisition added to baseline ValidTypes.
- #0160: SummaryRule top-of-Validate skip via RuleSkipPaths.IsTemplateOrAddition. Also moves FrontmatterRule, BrokenLinksRule, NamingRule from inline StartsWith blocks to the same helper.
- #0162: OrphanDocsRule returns [] for project/tasks/<file>.md when filename does not start with _ (folder-meta still validated). HubFilesRule skips project/tasks folder (Brian's surfaced surprise #1: HubFilesRule would otherwise error after D4).
- D4: HubGenerator drops project/tasks from foldersToProcess and from GetSubfolderHubs; appends a hardcoded ## Tasks section to the project hub. FixHubHandler deletes a stale project/tasks/_index.md when it carries the auto-gen banner. _project.template.md / _issues.template.md / _changelog.template.md prose updated to drop ../tasks/_index.md links.

VERIFICATION GATE:
- dotnet build: clean (0 warnings, 0 errors).
- python DynaDocs.Tests/coverage/run_tests.py: 4111/4111 pass (was ~4076 before; +35 net new tests across PR1+PR2).
- python DynaDocs.Tests/coverage/gap_check.py: 140/140 modules pass tier requirements (T1).
- dydo check on the dydo project itself: NOT performed; per Adele's reply, this is a soft-pass for this wave because the dev binary is not on PATH (the residual broken links in dydo/project/_index.md, _issues.md, _changelog.md will be repaired by the next dydo template update + dydo fix once the new binary ships — same convention as #0166). The Verification gate item 1 from Brian's plan ('0/0 on dydo project') is therefore deferred to release-time verification.

KEY DECISIONS WORTH SCRUTINY:
- IFrontmatterTypesService is constructed once per check pass in CheckDocValidator (single-load semantics; the cache is per-instance, not static, so concurrent check runs each get fresh state).
- FrontmatterRule constructor takes IFrontmatterTypesService as optional (nullable) so the existing parameterless 'new FrontmatterRule()' in tests still works (falls back to baseline ValidTypes).
- HubGenerator.AutoGenComment is now public so FixHubHandler.DeleteStaleTasksIndex can compare against it. Cleaner than re-declaring the constant.
- Templates/types.json uses a leading // comment block; FrontmatterTypesService and TemplateCommand both parse with ReadCommentHandling.Skip + AllowTrailingCommas via the AOT-friendly TypesJsonContext source-gen.
- FixHubHandler.DeleteStaleTasksIndex only deletes when the auto-gen banner is present (banner check guards against clobbering a hand-written file).

CONSTRAINTS FOR YOU:
- Soft-pass per Adele's note: gap_check parallelism flakes (#0167 territory) are out of scope; my diff doesn't touch racing code. Both my full and re-run gap_check came back clean (140/140 + 4111/4111 tests).
- Don't relitigate architecture (D1–D5 are locked).
- If you find a concrete implementation reason something won't work, surface it.

REPORT BACK to Adele on task implement-pr2-rule-corrections (per the dispatch flow, your reply on the task fulfills my origin-side obligation).

## Code Review

- Reviewed by: Grace
- Date: 2026-05-05 20:45
- Result: PASSED
- Notes: PASS. Substantive review of all 4 commits on master (PR1 fc83e31 + PR2 3213931/8b71cd4/d05f696). D1 types.json, #0160 SummaryRule + skip moves, #0162 OrphanDocsRule + HubFilesRule, D4 HubGenerator + FixHubHandler all match Brian's plan. Code is surgical, tests are thorough (FrontmatterTypesService 7 cases, FrontmatterRule 3 cases for D1 vocabulary, SummaryRule template skip, OrphanDocs task skip + _tasks.md guard, HubFiles project/tasks skip, HubGenerator project Tasks prose + no auto-gen index, FixHubHandler delete-with-banner / preserve-without). Verification gates: dotnet build 0/0; run_tests.py 4112/4112; gap_check.py 140/140; dydo check via dotnet run -- check confirms the only residual errors are 38 broken links inside the stale dydo/project/tasks/_index.md, which the new FixHubHandler.DeleteStaleTasksIndex removes on next dydo fix. Soft-pass on dydo check from PATH binary acknowledged per Adele's note (1.4.5 on PATH; new code only reachable via dotnet run).

Awaiting human approval.

## Approval

- Approved: 2026-05-06 17:47
