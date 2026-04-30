---
area: general
type: changelog
date: 2026-04-30
---

# Task: investigate-auto-close-regression

Review commit bd3cebe — five-line revert of 8d3e3b1's  addition in ReleaseAgent (Services/AgentRegistry.cs:502-505), plus two test renames + assertion flips and one stale-comment touch-up.

Diagnosis full notes: dydo/agents/Charlie/notes-investigate-auto-close-regression.md.
Issue: #0134 (filed).

What the change does: post-release on-disk state goes back to free + auto-close: true. The watchdog's kill condition at Services/WatchdogService.cs:359 then fires; ClearAutoClose flips auto-close to false after the kill (existing behaviour). Verified live: pre-revert, watchdog log shows continuous kills_attempted:0 with 14 free agents — the regression. New regression test ReleaseAgent_PreservesAutoCloseOnDisk_ForWatchdogKill captures the correct contract; failed before fix, passes after.

Hard constraints honoured (verify):
- Per-agent .claim.lock from 06512de (PollAndCleanupForAgent at Services/WatchdogService.cs:354) STAYS — closes #0121.
- ClaudeProcessNames whitelist from 06512de (Services/WatchdogService.cs:404) STAYS — closes #0122.
- Atomic WriteStateFile from 8d3e3b1 (#0125) STAYS — independent improvement.

Tests: 3916/3916 pass. gap_check: 137/137 modules pass.

Plan deviations: none. Brian's brief outlined exactly this fix shape; the user approved code-writer graduation.

Out-of-scope flag for Brian: e1eac2e diff inspected — only -NoExit is touched; --window/--new-tab routing flags untouched. The window-routing concern Brian flagged is therefore not from e1eac2e and is independent. Charlie did NOT investigate it further (out of scope for this task).

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit bd3cebe — five-line revert of 8d3e3b1's  addition in ReleaseAgent (Services/AgentRegistry.cs:502-505), plus two test renames + assertion flips and one stale-comment touch-up.

Diagnosis full notes: dydo/agents/Charlie/notes-investigate-auto-close-regression.md.
Issue: #0134 (filed).

What the change does: post-release on-disk state goes back to free + auto-close: true. The watchdog's kill condition at Services/WatchdogService.cs:359 then fires; ClearAutoClose flips auto-close to false after the kill (existing behaviour). Verified live: pre-revert, watchdog log shows continuous kills_attempted:0 with 14 free agents — the regression. New regression test ReleaseAgent_PreservesAutoCloseOnDisk_ForWatchdogKill captures the correct contract; failed before fix, passes after.

Hard constraints honoured (verify):
- Per-agent .claim.lock from 06512de (PollAndCleanupForAgent at Services/WatchdogService.cs:354) STAYS — closes #0121.
- ClaudeProcessNames whitelist from 06512de (Services/WatchdogService.cs:404) STAYS — closes #0122.
- Atomic WriteStateFile from 8d3e3b1 (#0125) STAYS — independent improvement.

Tests: 3916/3916 pass. gap_check: 137/137 modules pass.

Plan deviations: none. Brian's brief outlined exactly this fix shape; the user approved code-writer graduation.

Out-of-scope flag for Brian: e1eac2e diff inspected — only -NoExit is touched; --window/--new-tab routing flags untouched. The window-routing concern Brian flagged is therefore not from e1eac2e and is independent. Charlie did NOT investigate it further (out of scope for this task).

## Code Review

- Reviewed by: Dexter
- Date: 2026-04-29 19:19
- Result: PASSED
- Notes: PASS. Verified: (1) bd3cebe is a clean 4-line revert of s.AutoClose=false in ReleaseAgent + a why-comment that names the watchdog precondition and the lock that obviates clearing; (2) hard constraints intact - .claim.lock bracketing (WatchdogService.cs:354), ClaudeProcessNames whitelist (WatchdogService.cs:404), atomic WriteStateFile (#0125) all untouched; (3) new regression test ReleaseAgent_PreservesAutoCloseOnDisk_ForWatchdogKill asserts the correct contract; the two existing Release_ClearsAutoCloseOnDisk tests are renamed and assertions flipped; SetDispatchMetadata_PersistsAcrossStateUpdates comment touched up. Tests: 3916/3916 pass via worktree runner. gap_check: 137/137 modules pass. Edge cases reasoned through (Release_WithoutAutoClose_StateShowsFalse still holds because default state is auto-close: false when never dispatched with auto-close; redispatch race genuinely closed by per-agent lock so the post-release free + auto-close: true window is design, not bug). Charlie's out-of-scope flag on e1eac2e (only -NoExit touched, --window/--new-tab routing untouched) noted - independent concern, not part of this fix.

Awaiting human approval.

## Approval

- Approved: 2026-04-30 12:51
