---
area: general
type: changelog
date: 2026-04-29
---

# Task: fix-release-autoclose-and-atomic-write

Review fix-release-autoclose-and-atomic-write (commit 8d3e3b1).

Two surgical edits in Services/AgentRegistry.cs implementing Charlie's plan + Brian's brief resolutions:

1. ReleaseAgent (lines 492-506): added s.AutoClose = false to the UpdateAgentState lambda so on-disk state after release is 'free + auto-close: false', closing the #0121/#0123 redispatch-race window. SetDispatchMetadata remains the single authoritative producer of 'auto-close: true'.

2. WriteStateFile (lines 1406-1421): replaced final File.WriteAllText with same-directory temp-and-rename via File.Move(..., overwrite: true). Atomic on POSIX (rename(2)) and NTFS (MoveFileEx). Temp suffix is PID+Guid; same-volume by construction. Best-effort cleanup on rename failure preserves prior throw semantics.

Test changes (DynaDocs.Tests):
- Inverted/renamed AgentLifecycleTests Release_WithAutoCloseState_PreservesAutoCloseForWatchdog -> Release_ClearsAutoCloseOnDisk per new contract.
- Refreshed obsolete comment in AgentRegistryTests.cs (line 1964-1966 area).
- Added 3 regressions: ReleaseAgent_ClearsAutoCloseOnDisk (service-level dual of integration test), WriteStateFile_AtomicReplace_ConcurrentReaderNeverSeesPartial (200-iter writer loop + concurrent reader, asserts zero ParseFields failures, swallows writer-side IOException to lock the reader-integrity contract), WriteStateFile_NoTempFilesLeftBehind (asserts no state.md.tmp.* siblings on success path).

Brief resolutions honored:
- Q1 (atomic-write ClearAutoClose): out of scope (not touched).
- Q2 (clear WindowId/DispatchedBy/DispatchedByRole): out of scope (only AutoClose cleared).
- Q3 (retry on File.Move): no retry in v1.

Pre-commit grep: every remaining 'auto-close: true' test site is a synthetic watchdog fixture, a post-dispatch assertion, or my new pre-release assertion in ReleaseAgent_ClearsAutoCloseOnDisk. No test still asserts the old release-contract.

Build: clean (0 warnings, 0 errors). Tests: 3888/3888. gap_check: 100% (136/136 modules pass).

Plan: dydo/agents/Charlie/plan-release-autoclose-and-atomic-write.md
Inquisition: dydo/project/inquisitions/agent-deaths.md (#0123, #0125)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review fix-release-autoclose-and-atomic-write (commit 8d3e3b1).

Two surgical edits in Services/AgentRegistry.cs implementing Charlie's plan + Brian's brief resolutions:

1. ReleaseAgent (lines 492-506): added s.AutoClose = false to the UpdateAgentState lambda so on-disk state after release is 'free + auto-close: false', closing the #0121/#0123 redispatch-race window. SetDispatchMetadata remains the single authoritative producer of 'auto-close: true'.

2. WriteStateFile (lines 1406-1421): replaced final File.WriteAllText with same-directory temp-and-rename via File.Move(..., overwrite: true). Atomic on POSIX (rename(2)) and NTFS (MoveFileEx). Temp suffix is PID+Guid; same-volume by construction. Best-effort cleanup on rename failure preserves prior throw semantics.

Test changes (DynaDocs.Tests):
- Inverted/renamed AgentLifecycleTests Release_WithAutoCloseState_PreservesAutoCloseForWatchdog -> Release_ClearsAutoCloseOnDisk per new contract.
- Refreshed obsolete comment in AgentRegistryTests.cs (line 1964-1966 area).
- Added 3 regressions: ReleaseAgent_ClearsAutoCloseOnDisk (service-level dual of integration test), WriteStateFile_AtomicReplace_ConcurrentReaderNeverSeesPartial (200-iter writer loop + concurrent reader, asserts zero ParseFields failures, swallows writer-side IOException to lock the reader-integrity contract), WriteStateFile_NoTempFilesLeftBehind (asserts no state.md.tmp.* siblings on success path).

Brief resolutions honored:
- Q1 (atomic-write ClearAutoClose): out of scope (not touched).
- Q2 (clear WindowId/DispatchedBy/DispatchedByRole): out of scope (only AutoClose cleared).
- Q3 (retry on File.Move): no retry in v1.

Pre-commit grep: every remaining 'auto-close: true' test site is a synthetic watchdog fixture, a post-dispatch assertion, or my new pre-release assertion in ReleaseAgent_ClearsAutoCloseOnDisk. No test still asserts the old release-contract.

Build: clean (0 warnings, 0 errors). Tests: 3888/3888. gap_check: 100% (136/136 modules pass).

Plan: dydo/agents/Charlie/plan-release-autoclose-and-atomic-write.md
Inquisition: dydo/project/inquisitions/agent-deaths.md (#0123, #0125)

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-29 13:57
- Result: PASSED
- Notes: LGTM. Code matches Charlie's plan exactly.

Production changes:
- ReleaseAgent (Services/AgentRegistry.cs:505): s.AutoClose = false added in UpdateAgentState lambda; load-bearing comment explains the SetDispatchMetadata pairing. Closes #0121/#0123 redispatch race.
- WriteStateFile (Services/AgentRegistry.cs:1410-1430): same-directory temp-and-rename via File.Move(..., overwrite: true). Same-volume by construction. PID+Guid suffix avoids collisions. Best-effort temp cleanup on rename failure preserves prior throw semantics. Closes #0125.

Tests:
- Inverted Release_ClearsAutoCloseOnDisk in AgentLifecycleTests.cs.
- Obsolete comment refreshed at AgentRegistryTests.cs:2052-2053.
- 3 new regressions: ReleaseAgent_ClearsAutoCloseOnDisk, WriteStateFile_AtomicReplace_ConcurrentReaderNeverSeesPartial (200-iter writer + concurrent reader, asserts zero parse failures), WriteStateFile_NoTempFilesLeftBehind.
- Pre-commit grep verified: every remaining 'auto-close: true' test site is a synthetic watchdog fixture, post-dispatch assertion, or the new pre-release assertion. No test asserts the old release-contract.

Verification:
- Tests: 3888/3888 pass via worktree-isolated runner.
- gap_check: 136/137 modules pass (99.3%).

WAIVED: Services/WatchdogLogger.cs (T1, 0% line coverage) is the single failing module. This file is UNTRACKED in the working tree (?? Services/WatchdogLogger.cs) and is NOT part of commit 8d3e3b1. It is Jack's in-flight work on task fix-watchdog-structured-logging (Grace's plan, inquisition #0129). Brian explicitly waived this failure for this review since Jack's PR will deliver the coverage. Confirmed via dydo msg from Brian (a588ae93).

Awaiting human approval.

## Approval

- Approved: 2026-04-29 16:50
