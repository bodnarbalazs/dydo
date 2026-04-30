---
area: general
type: changelog
date: 2026-04-30
---

# Task: fix-ci-test-failures

Review commit 3b58876 for fix-ci-test-failures. Brief: dydo/agents/Brian/brief-fix-ci-test-failures.md. Fixes the single failure from CI run 25122266336 (WatchdogServiceTests.Run_NewAnchorAddedMidFlight_DefersExit, line 1268). Root cause: 150ms Task.Delay was insufficient grace for Run() to start under Linux CI thread-pool starvation; watchdog ended up in the 24h orphan path. Fix is test-only — adds a dead-anchor decoy and polls for its deletion as a deterministic 'initial ScanAnchors completed with anchor 100 live' signal. Verify: (a) the scope is test-only, no production touched; (b) the decoy mechanism is sound (decoy PID 99999999 is never alive in the override, so it gets deleted on the first scan); (c) the 5s deadline is generous; (d) gap_check is green (3916/3916). Approve or reject.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit 3b58876 for fix-ci-test-failures. Brief: dydo/agents/Brian/brief-fix-ci-test-failures.md. Fixes the single failure from CI run 25122266336 (WatchdogServiceTests.Run_NewAnchorAddedMidFlight_DefersExit, line 1268). Root cause: 150ms Task.Delay was insufficient grace for Run() to start under Linux CI thread-pool starvation; watchdog ended up in the 24h orphan path. Fix is test-only — adds a dead-anchor decoy and polls for its deletion as a deterministic 'initial ScanAnchors completed with anchor 100 live' signal. Verify: (a) the scope is test-only, no production touched; (b) the decoy mechanism is sound (decoy PID 99999999 is never alive in the override, so it gets deleted on the first scan); (c) the 5s deadline is generous; (d) gap_check is green (3916/3916). Approve or reject.

## Code Review

- Reviewed by: Adele
- Date: 2026-04-29 19:26
- Result: PASSED
- Notes: PASS. Decoy mechanism is sound: ScanAnchors deletes 99999999.anchor on the initial ResolveAnchors call (PID not in live set), and that same call sets hasSeenLiveAnchor=true via anchor 100 — polling for the decoy's deletion is a deterministic gate. 5s deadline is generous. Scope is test-only (single file, 12 lines added). Force-run gap_check: 3916/3916, 137/137 modules pass.

Awaiting human approval.

## Approval

- Approved: 2026-04-30 12:51
