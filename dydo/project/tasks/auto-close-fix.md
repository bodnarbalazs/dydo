---
area: platform
name: auto-close-fix
status: human-reviewed
created: 2026-03-09T12:08:58.2177492Z
assigned: Charlie
updated: 2026-03-09T14:59:11.3700760Z
---

# Task: auto-close-fix

Fix powershell resolution in TerminalCloser, improve PID detection, fix auto-close pipeline end-to-end

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

## Implementation Complete: Auto-Close Fix

### What was implemented:
1. **PowerShell resolution** - Added ProcessUtils.ResolvePowerShell() that tries pwsh first, falls back to powershell. Used by both TerminalCloser and TerminalLauncher.
2. **Robust PID detection** - Replaced fragile grandparent PID chain with ProcessUtils.FindAncestorProcess("claude"), which walks the ancestor chain regardless of tree depth.
3. **PowerShell resolution in TerminalLauncher** - Updated LaunchWindows() to use resolved PowerShell for both wt and fallback paths.
4. **Improved error messages** - Clear "Auto-close failed" messages with specific reason.

### Files changed:
- Services/ProcessUtils.cs - Added ResolvePowerShell()
- Services/TerminalCloser.cs - FindAncestorProcess-based termination, resolved PowerShell
- Services/TerminalLauncher.cs - Resolved PowerShell in LaunchWindows()
- DynaDocs.Tests/Services/ProcessUtilsTests.cs - 3 new tests
- DynaDocs.Tests/Services/TerminalCloserTests.cs - 6 focused tests
- DynaDocs.Tests/Services/TerminalLauncherTests.cs - 4 updated + 2 new tests

### 163 tests passing, 0 failures.

## Code Review (2026-03-09 14:41)

- Reviewed by: Henry
- Result: FAILED
- Issues: Review FAILED. Issues:

1. FLAKY TEST (BUG): LaunchWindows_AutoClose_PowerShellFallback_ContainStatusCheck fails ~1/5 runs due to race condition. ProcessUtils.PowerShellResolverOverride is a static mutable field modified by TerminalCloserTests (constructor sets it to 'pwsh'), TerminalLauncherTests (individual tests set it to 'powershell'), and ProcessUtilsTests — all running in parallel via xUnit. When TerminalCloserTests constructor overwrites the field mid-test in TerminalLauncherTests, the fallback FileName becomes 'pwsh' instead of expected 'powershell'. Fix: put all three test classes in the same xUnit collection to disable parallelism: [Collection("ProcessUtils")] on ProcessUtilsTests, TerminalCloserTests, and TerminalLauncherTests.

2. MINOR: LaunchWindows calls ProcessUtils.ResolvePowerShell() twice (line 192 for wt path, line 213 for fallback). In production this spawns 'pwsh --version' twice per launch. Should resolve once and reuse.

Requires rework.

## Code Review

- Reviewed by: Henry
- Date: 2026-03-09 14:59
- Result: PASSED
- Notes: LGTM. Both issues fixed correctly. [Collection("ProcessUtils")] eliminates the race condition — 10/10 runs stable. ResolvePowerShell hoisted to single call in LaunchWindows. All 1656 tests pass.

Awaiting human approval.