---
area: general
type: changelog
date: 2026-03-07
---

# Task: auto-close-dispatch

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalCloser.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalCloserTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Models\DispatchConfig.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\ProcessUtils.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\DispatchCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\AgentCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ProcessUtilsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\AgentLifecycleTests.cs — Modified


## Review Summary

Added ProcessStarterOverride to TerminalCloser (same IProcessStarter pattern as TerminalLauncher) so tests don't kill the real Claude process. IntegrationTestBase sets a NoOpProcessStarter, TerminalCloserTests uses RecordingProcessStarter. 3 files changed, all 1388 tests pass.

## Code Review

- Reviewed by: Iris
- Date: 2026-03-07 19:59
- Result: PASSED
- Notes: LGTM. P/Invoke replaces PowerShell process spawning cleanly. LinuxTerminalsAutoClose deleted; auto-close inlined via string replacement. TerminalCloser testable via ProcessStarterOverride. Shell metacharacter detection, stdin timeout, and agent list scoping are solid additions. All 1401 tests pass.

Awaiting human approval.

## Approval

- Approved: 2026-03-07 22:42
