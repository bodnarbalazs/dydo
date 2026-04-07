---
area: general
name: fix-inquisition-coverage-denominator
status: human-reviewed
created: 2026-04-07T19:38:12.0389666Z
assigned: Emma
updated: 2026-04-07T20:33:10.2148374Z
---

# Task: fix-inquisition-coverage-denominator

Fixed inquisition coverage integration tests. The source filter in FileCoverageService (filtering tracked files via paths.source from dydo.json) was already committed in 82604ed but integration tests were not updated - they used default config with src/** patterns that didn't match the test files (Commands/Foo.cs, etc.). Added PatchSourcePaths helper to set correct source patterns after InitProjectAsync. All 36 FileCoverage tests pass. Coverage gap_check passes (135/135 modules). Note: 2 pre-existing CommandDocConsistencyTests failures exist (template/reference doc sync issues from prior commits, unrelated to this task).

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed inquisition coverage integration tests. The source filter in FileCoverageService (filtering tracked files via paths.source from dydo.json) was already committed in 82604ed but integration tests were not updated - they used default config with src/** patterns that didn't match the test files (Commands/Foo.cs, etc.). Added PatchSourcePaths helper to set correct source patterns after InitProjectAsync. All 36 FileCoverage tests pass. Coverage gap_check passes (135/135 modules). Note: 2 pre-existing CommandDocConsistencyTests failures exist (template/reference doc sync issues from prior commits, unrelated to this task).

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-07 21:24
- Result: PASSED
- Notes: LGTM. Clean, minimal fix — PatchSourcePaths helper correctly aligns test config source patterns with test file structure. All 3483 tests pass. gap_check green (135/135). Note: WorktreeCommandTests.Merge_ConflictDetected flaked once during review (StringBuilder race in CaptureAll) — passed on re-run.

Awaiting human approval.