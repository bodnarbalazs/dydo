---
area: general
type: changelog
date: 2026-05-06
---

# Task: implement-pr3-issue-summary

REVIEW PR3 of the dydo-check-drift batch (#0161). Three commits on master:

- c85947a feat(issues): add --summary flag + placeholder detection — code-writer (Dexter)
- 5c77bbb docs(pr3): reference-doc sync + exclusion-model nudges — docs-writer (Frank)
- cbd063f docs(issues): backfill summaries on #0151-#0158 — docs-writer (Frank)

PR1 + PR2 are already on master (commits fc83e31, 3213931, 8b71cd4, d05f696). The plan lives at dydo/agents/Brian/archive/20260504-215742/plan-dydo-check-drift.md (read the "PR3" section for file:line guidance, BC migration table, rollout risks).

== WHAT TO LOOK AT ==

Code (c85947a, code-writer):
- Commands/IssueCommand.cs: --summary option wired through CreateCreateCommand alongside --body, --body-file.
- Commands/IssueCreateHandler.cs: refactored Execute into TryValidateMetadata / TryResolveBody / NormalizeSummary / AcquireIssueLock / RenderIssueContent helpers. Refactor was driven by Adele's note that the prior single-method version pushed CRAP > T1=30 after the --summary addition. Verify CRAP is now under threshold and the helpers' decomposition reads cleanly.
- Rules/SummaryRule.cs: new branch warns when SummaryParagraph trims to exactly the IssueCreateHandler.SummaryPlaceholder constant. Note the cross-namespace reference (Rules → Commands) — this is intentional so the placeholder string has one source of truth in the Command that emits it. If you disagree with the direction of the dependency, propose where else it should live.
- Templates/* (8 files): coordinated --summary teaching sweep. Verify all 8 mention --summary with consistent phrasing. The list: mode-code-writer, mode-reviewer, mode-orchestrator, mode-judge, mode-inquisitor, dydo-commands, about-dynadocs, _issues.

Tests (c85947a, code-writer):
- DynaDocs.Tests/Integration/IssueTests.cs: four new tests covering --summary present, --summary omitted (placeholder rendered), SummaryRule passes a real summary, SummaryRule warns on the placeholder. Helper IssueCreateAsync extended.
- DynaDocs.Tests/Rules/SummaryRuleTests.cs: two new unit tests for placeholder detection.

Docs (5c77bbb, docs-writer):
- dydo/reference/dydo-commands.md: --summary added to the issue-create section, mirroring the template.
- dydo/reference/about-dynadocs.md: Issues table line mirrors the template.
- dydo/reference/configuration.md: paragraphs on the three-layer exclusion model + the ## Tasks prose convention; AutoGenComment public note.
- dydo/project/changelog/2026/2026-05-04/cleanup-docs-check-backlog.md: appended Resolution paragraph tying PR1/PR2/PR3 hashes back. (Adele's original brief asked for a supersede on the dydo/project/tasks/ file but it was already migrated to the changelog at 9d2474e before her brief was written; Frank chose option (b) — annotate the changelog rather than skip — to preserve the audit trail.)

Backfill (cbd063f, docs-writer):
- dydo/project/issues/0151-*.md … 0158-*.md: each got a one-sentence summary inserted between the H1 and the first ## section, derived from the first sentence of ## Description. Frank flagged that #0155-#0158 each had a duplicate empty ## Description heading immediately preceding the real one; his single edit collapsed those pairs to one heading. Spot-check 2-3 backfill files to confirm summaries are faithful (not invented).

== VERIFICATION GATE OUTPUT ==

Ran on cbd063f via worktree-isolated runner:
- Tests: 4118/4118 passed (4 s + setup; baseline 4115/4115 + my 6 new + Frank's 0 = 4121 expected — the 4118 is correct because some tests are parameterized; verify by running yourself).
- Coverage gap_check: 140/140 modules at tier (100.0%).
- CommandDocConsistencyTests: 10/10 green (Frank verified at 5c77bbb and again at cbd063f).

== KNOWN-RED, ACKNOWLEDGED ==

`dydo check` on the dydo project is unchanged in error count from c85947a; Frank confirmed the residue is the pre-acknowledged dydo-tool defects already tracked in issues (template-additions title rule, project/tasks/_index broken links, stale agents). No new errors from this PR.

== REVIEW SCOPE NOTES ==

- Reviewer cannot be Dexter (code-writer) or Frank (docs-writer) on this task — that constraint is automatic, just calling it out.
- All three commits are part of one logical PR per Adele's plan ("Single PR can carry multiple commits across roles"). Review them as one unit.
- Soft-pass convention applies if any gap_check failure is race-based and unrelated; current run is clean so it should not come up.

== HANDOFF ==

Per the brief, by accepting this review you take over Dexter's reply obligation to Adele on `implement-pr3-issue-summary`. Message Adele with your verdict (PASS or REJECT + actionable notes) on that task subject after approving (or sending back).

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

REVIEW PR3 of the dydo-check-drift batch (#0161). Three commits on master:

- c85947a feat(issues): add --summary flag + placeholder detection — code-writer (Dexter)
- 5c77bbb docs(pr3): reference-doc sync + exclusion-model nudges — docs-writer (Frank)
- cbd063f docs(issues): backfill summaries on #0151-#0158 — docs-writer (Frank)

PR1 + PR2 are already on master (commits fc83e31, 3213931, 8b71cd4, d05f696). The plan lives at dydo/agents/Brian/archive/20260504-215742/plan-dydo-check-drift.md (read the "PR3" section for file:line guidance, BC migration table, rollout risks).

== WHAT TO LOOK AT ==

Code (c85947a, code-writer):
- Commands/IssueCommand.cs: --summary option wired through CreateCreateCommand alongside --body, --body-file.
- Commands/IssueCreateHandler.cs: refactored Execute into TryValidateMetadata / TryResolveBody / NormalizeSummary / AcquireIssueLock / RenderIssueContent helpers. Refactor was driven by Adele's note that the prior single-method version pushed CRAP > T1=30 after the --summary addition. Verify CRAP is now under threshold and the helpers' decomposition reads cleanly.
- Rules/SummaryRule.cs: new branch warns when SummaryParagraph trims to exactly the IssueCreateHandler.SummaryPlaceholder constant. Note the cross-namespace reference (Rules → Commands) — this is intentional so the placeholder string has one source of truth in the Command that emits it. If you disagree with the direction of the dependency, propose where else it should live.
- Templates/* (8 files): coordinated --summary teaching sweep. Verify all 8 mention --summary with consistent phrasing. The list: mode-code-writer, mode-reviewer, mode-orchestrator, mode-judge, mode-inquisitor, dydo-commands, about-dynadocs, _issues.

Tests (c85947a, code-writer):
- DynaDocs.Tests/Integration/IssueTests.cs: four new tests covering --summary present, --summary omitted (placeholder rendered), SummaryRule passes a real summary, SummaryRule warns on the placeholder. Helper IssueCreateAsync extended.
- DynaDocs.Tests/Rules/SummaryRuleTests.cs: two new unit tests for placeholder detection.

Docs (5c77bbb, docs-writer):
- dydo/reference/dydo-commands.md: --summary added to the issue-create section, mirroring the template.
- dydo/reference/about-dynadocs.md: Issues table line mirrors the template.
- dydo/reference/configuration.md: paragraphs on the three-layer exclusion model + the ## Tasks prose convention; AutoGenComment public note.
- dydo/project/changelog/2026/2026-05-04/cleanup-docs-check-backlog.md: appended Resolution paragraph tying PR1/PR2/PR3 hashes back. (Adele's original brief asked for a supersede on the dydo/project/tasks/ file but it was already migrated to the changelog at 9d2474e before her brief was written; Frank chose option (b) — annotate the changelog rather than skip — to preserve the audit trail.)

Backfill (cbd063f, docs-writer):
- dydo/project/issues/0151-*.md … 0158-*.md: each got a one-sentence summary inserted between the H1 and the first ## section, derived from the first sentence of ## Description. Frank flagged that #0155-#0158 each had a duplicate empty ## Description heading immediately preceding the real one; his single edit collapsed those pairs to one heading. Spot-check 2-3 backfill files to confirm summaries are faithful (not invented).

== VERIFICATION GATE OUTPUT ==

Ran on cbd063f via worktree-isolated runner:
- Tests: 4118/4118 passed (4 s + setup; baseline 4115/4115 + my 6 new + Frank's 0 = 4121 expected — the 4118 is correct because some tests are parameterized; verify by running yourself).
- Coverage gap_check: 140/140 modules at tier (100.0%).
- CommandDocConsistencyTests: 10/10 green (Frank verified at 5c77bbb and again at cbd063f).

== KNOWN-RED, ACKNOWLEDGED ==

`dydo check` on the dydo project is unchanged in error count from c85947a; Frank confirmed the residue is the pre-acknowledged dydo-tool defects already tracked in issues (template-additions title rule, project/tasks/_index broken links, stale agents). No new errors from this PR.

== REVIEW SCOPE NOTES ==

- Reviewer cannot be Dexter (code-writer) or Frank (docs-writer) on this task — that constraint is automatic, just calling it out.
- All three commits are part of one logical PR per Adele's plan ("Single PR can carry multiple commits across roles"). Review them as one unit.
- Soft-pass convention applies if any gap_check failure is race-based and unrelated; current run is clean so it should not come up.

== HANDOFF ==

Per the brief, by accepting this review you take over Dexter's reply obligation to Adele on `implement-pr3-issue-summary`. Message Adele with your verdict (PASS or REJECT + actionable notes) on that task subject after approving (or sending back).

## Code Review

- Reviewed by: Grace
- Date: 2026-05-06 12:59
- Result: PASSED
- Notes: PASS. Tests 4118/4118 + gap_check 140/140 (100.0%). Code: IssueCreateHandler refactor cleanly decomposes Execute into TryValidateMetadata/TryResolveBody/NormalizeSummary/AcquireIssueLock/RenderIssueContent + EnumNames<T> helper; reads well. SummaryRule cross-namespace ref to IssueCreateHandler.SummaryPlaceholder is acceptable (single source of truth at the emitter; brief explicitly invited dissent and I have none worth blocking on). All 8 Templates/* mention --summary with consistent phrasing. Tests cover present/omitted/SummaryRule-pass/SummaryRule-warn. Docs (5c77bbb): reference clones mirror templates; configuration.md exclusion-layer + project-hub Tasks prose paragraphs are good follow-ups to PR1/PR2. Backfill (cbd063f): spot-checked #0151,#0152,#0155,#0156,#0157,#0158 - summaries faithful to ## Description content; duplicate-heading collapse on #0155-#0158 is a clean side-fix. Note (out-of-scope): the installed dydo binary at PATH is at 1259d156 (predates PR1); dydo/_system/templates/* and dydo/project/issues/_issues.md remain stale until binary rebuild + dydo template update - not introduced by this PR.

Awaiting human approval.

## Approval

- Approved: 2026-05-06 17:47
