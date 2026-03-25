---
area: general
type: changelog
date: 2026-03-25
---

# Task: fix-autoclose-error

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ProcessUtilsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\WorktreeDispatchTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\FileCoverageService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WatchdogServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Utils\PathUtils.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\InitCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TemplateCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorkspaceCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\PathUtilsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\AgentRegistryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCompatTests.cs — Modified


## Review Summary

Fixed the auto-close 0x80070002 error. Root cause: PollAndCleanup called wt.exe close on windows that were already gone (closed by postClaudeCheck exit). The fix adds a process-existence check before calling TryCloseWindow — if no agent processes remain, the terminal is already closed so we skip the wt close call and just clear the auto-close flag. Also fixed a secondary bug: the fallback (process kill) path never called ClearAutoClose, so the watchdog would retry every 10 seconds indefinitely. No plan deviations — kept TryCloseWindow/ResolveWtExe intact since they work when the window is still open.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-20 13:39
- Result: PASSED
- Notes: LGTM. Logic reordering is correct: process-existence check before TryCloseWindow prevents 0x80070002 on gone windows. Secondary fix (ClearAutoClose after kill fallback) stops infinite retry. Test covers the primary path. WatchdogService passes coverage gate. Clean, surgical change.

Awaiting human approval.

## Approval

- Approved: 2026-03-25 17:24
