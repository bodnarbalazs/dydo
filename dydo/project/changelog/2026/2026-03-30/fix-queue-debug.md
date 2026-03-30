---
area: general
type: changelog
date: 2026-03-30
---

# Task: fix-queue-debug

Fixed the queue race condition where the watchdog cleared _active.json before the second dispatch could see it. Root cause: on Windows, wt.exe (Windows Terminal launcher) exits immediately after spawning a tab, making the stored PID stale. The watchdog then cleared the entry, allowing the second dispatch to also acquire the slot. Fix: PollQueues now checks agent state (via AgentRegistry) before clearing stale entries — if the agent is Working/Reviewing/Dispatched, the entry is preserved even if the PID appears dead. Added two tests: one proving the fix (working agent survives stale PID detection), one proving crash recovery still works (free agent with dead PID is cleared). All 3293 tests pass, gap_check passes (131/131). No plan deviations.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchQueueTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified


## Review Summary

Fixed the queue race condition where the watchdog cleared _active.json before the second dispatch could see it. Root cause: on Windows, wt.exe (Windows Terminal launcher) exits immediately after spawning a tab, making the stored PID stale. The watchdog then cleared the entry, allowing the second dispatch to also acquire the slot. Fix: PollQueues now checks agent state (via AgentRegistry) before clearing stale entries — if the agent is Working/Reviewing/Dispatched, the entry is preserved even if the PID appears dead. Added two tests: one proving the fix (working agent survives stale PID detection), one proving crash recovery still works (free agent with dead PID is cleared). All 3293 tests pass, gap_check passes (131/131). No plan deviations.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-27 21:08
- Result: PASSED
- Notes: LGTM. Core fix is correct and surgical: WatchdogService checks agent state via AgentRegistry before clearing stale entries (addresses wt.exe PID unreliability on Windows). QueueService uses Environment.ProcessId instead of placeholder 0 to prevent premature stale detection. Terminal launcher inherited-worktree path correctly adds cd and sleep. All 3293 tests pass, gap_check 131/131. Shell escaping correct for both POSIX and PowerShell. Minor note: WorktreeInitSettingsScript appears to be dead code after the switch to WorktreeInheritedSetupScript (out-of-scope).

Awaiting human approval.

## Approval

- Approved: 2026-03-30 17:16
