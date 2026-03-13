---
area: general
type: changelog
date: 2026-03-13
---

# Task: worktree-agents-symlink

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\AgentRegistryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified


## Review Summary

Implemented symlink/junction for dydo/agents/ in worktrees. Windows: NTFS junction via New-Item -ItemType Junction after Set-Location into worktree, cleaned up via cmd /c rmdir before git worktree remove. Linux/Mac: captures _wt_root before cd, creates ln -s symlink, cleaned up via rm -f before worktree removal. Added 12 new tests covering junction/symlink creation, target path correctness, and cleanup ordering for all platforms. All 66 worktree tests pass. 2 pre-existing failures in DispatchCommandTests (window-id: null) are unrelated. No plan deviations except: cleanup uses rm -f dydo/agents (relative from worktree dir) instead of the plan's full path, since cleanup runs from inside the worktree.

## Code Review (2026-03-13 14:21)

- Reviewed by: Adele
- Result: FAILED
- Issues: WindowsTerminalLauncher.cs lines 29, 30, 33: dydo\\agents produces double-backslash paths (dydo\agents). Works in wt path (wt interprets \ as \) but fallback PowerShell path gets literal double backslash. Fix: use forward slashes (dydo/agents) consistently, matching the rest of the function. 3 lines to change.

Requires rework.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-13 14:27
- Result: PASSED
- Notes: LGTM. Backslash fix verified on lines 29/30/33. Full implementation reviewed: junction/symlink logic correct, cleanup ordering correct, path prefix fix correct, 12 new tests are meaningful. All 226 TerminalLauncher tests pass.

Awaiting human approval.

## Approval

- Approved: 2026-03-13 17:32
