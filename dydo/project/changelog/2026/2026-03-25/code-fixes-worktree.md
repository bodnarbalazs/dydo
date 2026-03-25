---
area: general
type: changelog
date: 2026-03-25
---

# Task: code-fixes-worktree

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Commands/RolesResetCommandTests.cs — Created
C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Commands/ValidateCommandTests.cs — Created
C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Commands/WatchdogCommandTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WorktreeCreationLockTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/Services/BashCommandAnalyzer.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Services/BashCommandAnalyzerTests.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Services/RoleConstraintEvaluatorTests.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Services/RoleDefinitionServiceTests.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Commands/WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\IntegrationTestBase.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified


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

## Approval

- Approved: 2026-03-25 17:24
