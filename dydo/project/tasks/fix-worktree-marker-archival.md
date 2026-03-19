---
area: general
name: fix-worktree-marker-archival
status: human-reviewed
created: 2026-03-19T20:47:36.5807019Z
assigned: Brian
updated: 2026-03-19T20:52:08.9671514Z
---

# Task: fix-worktree-marker-archival

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Added 7 worktree markers (.worktree, .worktree-path, .worktree-base, .worktree-root, .worktree-hold, .merge-source, .needs-merge) to SystemManagedEntries in both AgentRegistry.cs and WorkspaceArchiver.cs so they survive ArchiveWorkspace. Added a test in each test file (WorkspaceArchiveTests and WorkspaceArchiverTests) verifying all 7 markers are preserved. All 27 archive tests pass. No plan deviations.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-19 20:54
- Result: PASSED
- Notes: LGTM. All 7 worktree markers correctly added to SystemManagedEntries in both AgentRegistry.cs and WorkspaceArchiver.cs. Tests are meaningful and all 27 pass. gap_check has 15 pre-existing failures in unrelated modules — no regressions. Note: SystemManagedEntries duplication between the two files is pre-existing.

Awaiting human approval.