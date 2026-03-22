---
area: general
name: fix-merge-marker-race
status: human-reviewed
created: 2026-03-22T20:51:15.9999069Z
assigned: Brian
updated: 2026-03-22T20:54:09.4472612Z
---

# Task: fix-merge-marker-race

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed the merge marker race condition. Two changes: (1) Split RemoveMarkers into RemoveWorktreeMarkers (excludes .merge-source, used by ExecuteCleanup) and RemoveAllMarkers (includes .merge-source, used by FinalizeMerge). This prevents worktree cleanup from deleting .merge-source written for the next dispatch. (2) Swapped order in FinalizeMerge: worktree removal now happens before branch deletion so git branch -D no longer fails. Added two new tests: RemoveWorktreeMarkers_PreservesMergeSource and RemoveAllMarkers_DeletesMergeSource. No plan deviations — implemented Option A as specified.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-22 21:00
- Result: PASSED
- Notes: LGTM. Clean split of RemoveMarkers into RemoveWorktreeMarkers/RemoveAllMarkers correctly prevents the race. FinalizeMerge ordering fix (worktree removal before branch deletion) is correct. Two new tests cover both sides of the split. No coverage regressions. No slop.

Awaiting human approval.