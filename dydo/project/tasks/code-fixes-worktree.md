---
area: general
name: code-fixes-worktree
status: human-reviewed
created: 2026-03-23T14:17:29.5250235Z
assigned: Emma
updated: 2026-03-23T16:16:31.8619359Z
---

# Task: code-fixes-worktree

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented both worktree fixes from Adele's brief.

ISSUE 1 - Serialize worktree creation: Moved git worktree add from terminal scripts to DispatchService.CreateGitWorktree(), which runs synchronously before terminal launch. Added cross-process file locking (dydo/_system/.local/worktrees/.lock) using the same FileMode.CreateNew pattern as agent claim locks. Terminal scripts now only cd into the pre-created worktree and set up symlinks/junctions. All 3 platforms updated (Windows, Linux, Mac).

ISSUE 2 - Audit file preservation: Added PreserveAuditFiles() to WorktreeCommand.cs, called in both ExecuteCleanup and FinalizeMerge before the worktree directory is deleted. Copies audit JSON files from the worktree's dydo/_system/audit/ to the main repo's audit directory, preserving year subfolder structure. Uses PathUtils.GetMainProjectRoot() to detect the main repo root.

22 new tests added covering lock serialization, audit preservation, and terminal script changes. All 3061 tests pass. No coverage regressions.

## Code Review (2026-03-23 16:12)

- Reviewed by: Grace
- Result: FAILED
- Issues: 1 issue: AgentRegistry.cs:23 unused ProjectRoot property. Dispatched code-writer to fix.

Requires rework.

## Code Review

- Reviewed by: Frank
- Date: 2026-03-23 16:28
- Result: PASSED
- Notes: LGTM. All 3061 tests pass. Issue 1 (worktree serialization): clean move of git worktree add to DispatchService with cross-process file locking. Issue 2 (audit preservation): PreserveAuditFiles correctly called in both cleanup paths. Grace's fix (ProjectRoot removal): confirmed removed. Additional conditional must-reads and brief injection are well designed. gap_check has 13 pre-existing failures in unmodified files — zero coverage regressions from this task.

Awaiting human approval.