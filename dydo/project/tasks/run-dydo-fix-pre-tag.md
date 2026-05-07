---
area: general
name: run-dydo-fix-pre-tag
status: human-reviewed
created: 2026-05-06T21:00:34.9389682Z
assigned: Brian
updated: 2026-05-06T21:22:00.8283618Z
---

# Task: run-dydo-fix-pre-tag

Review commit 95d7224 ("docs(hubs): regenerate auto-gen index files post-wave"). This is a mechanical regen pass via dydo fix on the v1.4.6 dev binary - final pre-tag bookkeeping step (item 5 of 7 from the pre-tag-audit, items 1-4 done by 49c0759, items 6-7 already resolved).

WHAT TO CHECK
1. Only auto-gen files mutated (4 files; +29/-60). Diff:
   - dydo/project/_index.md (Tasks link replaced by prose paragraph - PR2 D4)
   - dydo/project/inquisitions/_index.md (3 new entries: dydo-check-drift, pre-tag-audit, test-runtime-regression)
   - dydo/project/issues/_index.md (#0151-#0158 gain summaries from cbd063f / 49c0759 backfills; #0159-#0172 added; #0164 has no summary line because source file lacks one)
   - dydo/project/tasks/_index.md (deleted - PR2 D4 / FixHubHandler.DeleteStaleTasksIndex; pre-image carried auto-gen banner)
2. No non-auto-gen file touched.
3. Changelog hubs are no-op (#0166 fix at 844579f doing its job).

VERIFICATION ALREADY DONE
- dotnet build: clean.
- python DynaDocs.Tests/coverage/run_tests.py: 4131/0 (worktree-isolated, 4m 27s).

OUT-OF-SCOPE FLAG
dydo fix flagged #0164 for manual attention (missing summary paragraph in source issue file). Not in this commit's scope and not blocking the tag - flagged to Adele in the run-dydo-fix-pre-tag report-back. Do not address as part of this review.

After approval, balazs is clear to push the tag.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit 95d7224 ("docs(hubs): regenerate auto-gen index files post-wave"). This is a mechanical regen pass via dydo fix on the v1.4.6 dev binary - final pre-tag bookkeeping step (item 5 of 7 from the pre-tag-audit, items 1-4 done by 49c0759, items 6-7 already resolved).

WHAT TO CHECK
1. Only auto-gen files mutated (4 files; +29/-60). Diff:
   - dydo/project/_index.md (Tasks link replaced by prose paragraph - PR2 D4)
   - dydo/project/inquisitions/_index.md (3 new entries: dydo-check-drift, pre-tag-audit, test-runtime-regression)
   - dydo/project/issues/_index.md (#0151-#0158 gain summaries from cbd063f / 49c0759 backfills; #0159-#0172 added; #0164 has no summary line because source file lacks one)
   - dydo/project/tasks/_index.md (deleted - PR2 D4 / FixHubHandler.DeleteStaleTasksIndex; pre-image carried auto-gen banner)
2. No non-auto-gen file touched.
3. Changelog hubs are no-op (#0166 fix at 844579f doing its job).

VERIFICATION ALREADY DONE
- dotnet build: clean.
- python DynaDocs.Tests/coverage/run_tests.py: 4131/0 (worktree-isolated, 4m 27s).

OUT-OF-SCOPE FLAG
dydo fix flagged #0164 for manual attention (missing summary paragraph in source issue file). Not in this commit's scope and not blocking the tag - flagged to Adele in the run-dydo-fix-pre-tag report-back. Do not address as part of this review.

After approval, balazs is clear to push the tag.

## Code Review

- Reviewed by: Charlie
- Date: 2026-05-06 21:29
- Result: PASSED
- Notes: PASS. 95d7224 verified: 4 auto-gen files, +29/-60, scope matches brief exactly. project/_index.md (Tasks link -> prose paragraph), inquisitions/_index.md (+3 entries: dydo-check-drift, pre-tag-audit, test-runtime-regression), issues/_index.md (#0151-#0158 summary backfills, #0159-#0172 added, #0164 source-lacks-summary confirmed - already flagged to Adele OOS), tasks/_index.md (deleted, pre-image carried auto-gen banner). No non-auto-gen file touched. Changelog hubs no-op in this commit (#0166 fix at 844579f confirmed working); HEAD's follow-up 016318c drops the now-broken _changelog.md link to tasks/_index.md. dydo check: 0 errors / 0 warnings on 926 files (only output warning is a pre-existing Adele stale-session note - unrelated). gap_check.py at HEAD (016318c): 4131/0 tests, 140/140 modules pass tier (worktree-isolated, 4m 16s). balazs is clear to push the tag.

Awaiting human approval.