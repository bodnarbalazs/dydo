---
area: general
type: changelog
date: 2026-03-25
---

# Task: fix-worktree-lifecycle-v2

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Models\NudgeConfig.cs — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-test-v8.txt — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-test-v11.txt — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-test-v9.txt — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-test-v10.txt — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\ConfigFactory.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigFactoryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TemplateCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\GuardCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\LinuxTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\DydoConfig.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Serialization\DydoJsonContext.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\ValidationService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCompatTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ValidationServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\BashCommandAnalyzer.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\BashCommandAnalyzerTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\ReviewCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MessageService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\MessageIntegrationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchWaitIntegrationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MacTerminalLauncher.cs — Modified


## Review Summary

Fixed 2 of 4 worktree lifecycle issues with confirmed root causes. Issue 1: DispatchService.CompleteDispatch created dispatch markers with the dispatched task name instead of the sender task, causing HasDispatchMarker lookup to fail and code-writers to dispatch duplicate reviewers. Fixed by using sender.Task instead of task. Issue 2: MessageService.CheckTargetActive blocked reply-pending message sends to inactive targets, creating a Catch-22 where the marker could never be cleared. Fixed by allowing sends when hasReplyPending is true. Issues 3+4: code analysis showed correct logic; may need runtime verification. Added 3 integration tests covering both fixes. All 3000 tests pass.

## Code Review (2026-03-22 15:10)

- Reviewed by: Charlie
- Result: FAILED
- Issues: BuildReplyPendingMessage (MessageService.cs:116-133) is now dead code — its only call site was replaced by 'return null'. Per coding standards, YOUR changes made it unused so it must be deleted. Minor: no-subject sends bypass marker cleanup (see workspace for details).

Requires rework.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-22 15:24
- Result: PASSED
- Notes: LGTM. Dead code removed, reply-pending bypass fixed, no-subject marker cleanup correct. All 3000 tests pass. Coverage gate failures are pre-existing (0 regressions).

Awaiting human approval.

## Approval

- Approved: 2026-03-25 17:25
