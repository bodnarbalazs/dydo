---
area: general
name: fix-phantom-close
status: human-reviewed
created: 2026-03-25T20:46:04.6501785Z
assigned: Charlie
updated: 2026-03-25T22:15:40.3610615Z
---

# Task: fix-phantom-close

Implemented Option 3 (wait-then-verify) to fix the phantom close TOCTOU race. PollAndCleanup now defers intervention on first sighting of a free auto-close agent with processes still running, letting natural exit work. On the next poll cycle, if processes persist, kills non-shell processes as fallback. TryCloseWindow is no longer called from PollAndCleanup. No plan deviations. All 3193 tests pass, coverage gate 100%.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented Option 3 (wait-then-verify) to fix the phantom close TOCTOU race. PollAndCleanup now defers intervention on first sighting of a free auto-close agent with processes still running, letting natural exit work. On the next poll cycle, if processes persist, kills non-shell processes as fallback. TryCloseWindow is no longer called from PollAndCleanup. No plan deviations. All 3193 tests pass, coverage gate 100%.

## Code Review (2026-03-25 22:11)

- Reviewed by: Dexter
- Result: FAILED
- Issues: Dead code: TryCloseWindow and ResolveWtExe have no production callers after PollAndCleanup change. Orphaned XML doc in ProcessUtils.cs. Core logic is correct.

Requires rework.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-25 22:26
- Result: PASSED
- Notes: LGTM. All three Dexter issues fixed: TryCloseWindow and ResolveWtExe dead code removed from WatchdogService.cs, orphaned XML doc repositioned in ProcessUtils.cs, two dead tests removed. 39 WatchdogService tests pass, coverage gate 129/129 (100%). Out-of-scope: Release_BlockedByReplyPending test failing in DispatchWaitIntegrationTests (unrelated).

Awaiting human approval.