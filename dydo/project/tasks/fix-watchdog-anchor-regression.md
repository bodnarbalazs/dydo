---
area: general
name: fix-watchdog-anchor-regression
status: human-reviewed
created: 2026-04-24T15:34:06.7851245Z
assigned: Adele
updated: 2026-04-24T16:20:24.8097328Z
---

# Task: fix-watchdog-anchor-regression

def1fa4 anchored the watchdog on its immediate parent (the short-lived dydo dispatch CLI). Result: --auto-close no longer works. Fix: resolve the user's claude session PID on the dispatcher side and hand it to the watchdog via env var DYDO_WATCHDOG_ANCHOR_PID. See plan-fix-watchdog-anchor-regression.md.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

# Review: fix-watchdog-anchor-regression

## What I implemented

Split watchdog anchor-PID resolution across the dispatcher/watchdog boundary:

1. Services/WatchdogService.cs :: EnsureRunning — before Process.Start, call ProcessUtils.FindAncestorProcess("claude"). If it resolves, stamp psi.Environment["DYDO_WATCHDOG_ANCHOR_PID"] on the child's ProcessStartInfo. The dispatcher (still alive) knows the right anchor; the watchdog cannot walk there once the dispatcher is dead.
2. Services/WatchdogService.cs :: Run — replace GetParentPid(Environment.ProcessId) with a read of DYDO_WATCHDOG_ANCHOR_PID. Precedence: GetParentPidOverride (test seam) > env var > null. No fallback to GetParentPid — that was the bug. If env var is missing/unparseable, anchorPid is null (pre-def1fa4 behavior; watchdog runs forever, graceful-shutdown via ProcessExit/CancelKeyPress still works).

## Tests

Added two tests in DynaDocs.Tests/Services/WatchdogServiceTests.cs (Watchdog Lifecycle region). Both confirmed red on master pre-fix, green after fix:

- EnsureRunning_PassesClaudeAncestorPidToChildViaEnv — stubs FindAncestorProcessOverride + StartProcessOverride, asserts child PSI env carries DYDO_WATCHDOG_ANCHOR_PID="99999".
- Run_ReadsAnchorPidFromEnvironmentWhenOverrideNull_DoesNotFallBackToParentPid — long-lived dummy + env var, override=null, PollInterval=100ms. Asserts Run still running at 250ms, then kills dummy and asserts Run exits within 5s. On master this assert-fails because Run anchors on the test-host parent, not the env-var PID.

Extended Dispose to clean up ProcessUtils.FindAncestorProcessOverride and the DYDO_WATCHDOG_ANCHOR_PID env var so test state does not leak.

## Plan deviations

None. Followed the plan verbatim.

## Key decisions

- Anchor resolution happens inside EnsureRunning just before Process.Start — no new helper method. PSI already has UseShellExecute=false, so psi.Environment is writable without extra setup.
- Run's anchor precedence uses int.TryParse(..., out var envPid) ? envPid : null — silently ignores unset/malformed env vars, yielding a null anchor (pre-def1fa4 semantics).
- Env-var name DYDO_WATCHDOG_ANCHOR_PID follows the dydo prefix used elsewhere.

## Verification

- python DynaDocs.Tests/coverage/run_tests.py — 3781/3781 pass.
- python DynaDocs.Tests/coverage/gap_check.py --force-run — exit 0, 136/136 modules pass tier requirements.
- Existing Run_ExitsWhenAnchorProcessDies, Run_ExitsWhenCancellationRequested, Run_DeletesPidFileOnExit still green — the GetParentPidOverride test seam stays ahead of the env-var read.

## Out of scope (not touched)

Per brief: Commands/WorktreeCommand.cs, Services/TerminalLauncher.cs — separate task. DispatchService.cs — the fix is contained in WatchdogService. No --anchor-pid CLI flag added.

## Files modified

- Services/WatchdogService.cs — 2 small edits in EnsureRunning and Run.
- DynaDocs.Tests/Services/WatchdogServiceTests.cs — 2 new tests + Dispose cleanup.

## Code Review

- Reviewed by: Dexter
- Date: 2026-04-24 16:28
- Result: PASSED
- Notes: LGTM. Diff is surgical and matches plan verbatim. EnsureRunning stamps DYDO_WATCHDOG_ANCHOR_PID on child PSI (UseShellExecute=false already set, so psi.Environment writable). Run's anchor precedence (override > env var > null) preserves existing test seams and eliminates the GetParentPid(Environment.ProcessId) regression. Comments explain the why. Both new tests (EnsureRunning_PassesClaudeAncestorPidToChildViaEnv, Run_ReadsAnchorPidFromEnvironmentWhenOverrideNull_DoesNotFallBackToParentPid) target exactly the two sides of the split. Dispose cleanup extended for FindAncestorProcessOverride and the env var. run_tests.py 3781/3781 green; gap_check.py exit 0, 136/136 modules. Out-of-scope working-tree changes in WorktreeCommand/TerminalLauncher belong to investigate-worktree-race — unrelated to this task, not blocking.

Awaiting human approval.