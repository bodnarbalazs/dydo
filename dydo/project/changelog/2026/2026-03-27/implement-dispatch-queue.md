---
area: general
type: changelog
date: 2026-03-27
---

# Task: implement-dispatch-queue

Implemented dispatch queue feature: --queue flag on dydo dispatch defers terminal launch when queue has active item. QueueService manages named queues under dydo/_system/.local/queues/. Dequeue triggers on agent release. Watchdog extended for stale active detection and transient queue cleanup. One known deviation: dydo/reference/dydo-commands.md needs --queue documented (template updated, reference doc needs docs-writer). One test fails: AllOptions_AppearInReferenceDoc due to the missing doc update. dydo.json queues section not added (guard blocked) — code defaults to merge as built-in persistent queue.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\InboxServiceTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Models\QueueEntry.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Models\QueueActiveEntry.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\QueueService.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Commands\QueueCommand.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\QueueServiceTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\QueueCommandTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchQueueTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Models\InboxItem.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\InboxItemParser.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\InboxService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\InboxItemParserTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCompatTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\IProcessStarter.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\LinuxTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MacTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\AgentLifecycleHandlers.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\NoOpProcessStarter.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WorkspaceArchiver.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-reviewer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\InitCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\InitCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\DydoConfig.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Serialization\DydoJsonContext.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Program.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\DispatchCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\ReviewCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CommandSmokeTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\dydo-commands.template.md — Modified


## Review Summary

Implemented dispatch queue feature: --queue flag on dydo dispatch defers terminal launch when queue has active item. QueueService manages named queues under dydo/_system/.local/queues/. Dequeue triggers on agent release. Watchdog extended for stale active detection and transient queue cleanup. One known deviation: dydo/reference/dydo-commands.md needs --queue documented (template updated, reference doc needs docs-writer). One test fails: AllOptions_AppearInReferenceDoc due to the missing doc update. dydo.json queues section not added (guard blocked) — code defaults to merge as built-in persistent queue.

## Code Review (2026-03-26 12:35)

- Reviewed by: Charlie
- Result: FAILED
- Issues: Wrong PID in queue active entries: Environment.ProcessId captures the short-lived dispatch/release CLI PID, not the launched terminal PID. Watchdog sees it as stale within ~10 seconds and prematurely dequeues, breaking queue serialization. Fix: change IProcessStarter.Start() to return int (PID), thread it through LaunchNewTerminal, use it in SetActive calls. Minor: .queued marker missing from preserved paths in AgentRegistry/WorkspaceArchiver.

Requires rework.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-26 13:10
- Result: PASSED
- Notes: LGTM. PID fix correctly threaded through all launchers and SetActive call sites. .queued marker preserved in both SystemManagedEntries arrays. 3238 tests pass, gap_check 131/131 modules pass. Known deviation: AllOptions_AppearInReferenceDoc fails (needs docs-writer for --queue). Minor note: dotnet-run nudge pattern missing queue subcommand.

Awaiting human approval.

## Approval

- Approved: 2026-03-27 13:14
