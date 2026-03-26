---
area: general
name: fix-merge-discoverability-merge
status: review-pending
created: 2026-03-26T19:23:57.4519313Z
assigned: Charlie
updated: 2026-03-26T19:42:03.6194668Z
---

# Task: fix-merge-discoverability-merge

Merged worktree/fix-merge-discoverability changes into master. Two changes to GuardCommand.cs: (1) git merge is now treated as write-like for must-read enforcement, (2) git merge block now also triggers when .merge-source marker exists (not just in worktrees). Two new integration tests added for the expanded guard behavior. All 3244 tests pass, coverage gate green.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Merged worktree/fix-merge-discoverability changes into master. Two changes to GuardCommand.cs: (1) git merge is now treated as write-like for must-read enforcement, (2) git merge block now also triggers when .merge-source marker exists (not just in worktrees). Two new integration tests added for the expanded guard behavior. All 3244 tests pass, coverage gate green.