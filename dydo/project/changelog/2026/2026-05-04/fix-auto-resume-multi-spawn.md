---
area: general
type: changelog
date: 2026-05-04
---

# Task: fix-auto-resume-multi-spawn

Review commit 397011f for fix-auto-resume-multi-spawn (#0143). Approach: refresh .session.ClaimedPid in HandleExistingSession's same-SessionId short-circuit so the watchdog stops seeing a dead PID after auto-resume. Identity preserved per Decision 022 — SessionId and Claimed unchanged, only PID updates. Files: Services/AgentRegistry.cs (RefreshClaimedPid helper + 1-line call inside the SessionId-match branch), DynaDocs.Tests/Services/AgentRegistryTests.cs (ClaimAgent_SameSessionIdReclaim_RefreshesClaimedPid). NOTE: Brian's original brief proposed a cooldown approach in state.md; he superseded it with a follow-up message correcting to this 1-line fix instead — implemented per the correction. gap_check 137/137, 4008/4008 tests green (one parallel-execution flake on StaleDispatchDoubleClaimTests on first run cleared on re-run; my change does not touch ReserveAgent). Approve or reject.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit 397011f for fix-auto-resume-multi-spawn (#0143). Approach: refresh .session.ClaimedPid in HandleExistingSession's same-SessionId short-circuit so the watchdog stops seeing a dead PID after auto-resume. Identity preserved per Decision 022 — SessionId and Claimed unchanged, only PID updates. Files: Services/AgentRegistry.cs (RefreshClaimedPid helper + 1-line call inside the SessionId-match branch), DynaDocs.Tests/Services/AgentRegistryTests.cs (ClaimAgent_SameSessionIdReclaim_RefreshesClaimedPid). NOTE: Brian's original brief proposed a cooldown approach in state.md; he superseded it with a follow-up message correcting to this 1-line fix instead — implemented per the correction. gap_check 137/137, 4008/4008 tests green (one parallel-execution flake on StaleDispatchDoubleClaimTests on first run cleared on re-run; my change does not touch ReserveAgent). Approve or reject.

## Code Review

- Reviewed by: Adele
- Date: 2026-05-01 13:03
- Result: PASSED
- Notes: PASS. Surgical 1-line fix per Brian's correction. RefreshClaimedPid preserves SessionId+Claimed (Decision 022 identity), only updates PID via ResolveClaimedPid; no-op early return when PID unchanged; AOT-safe source-gen JSON; runs under existing TryAcquireLock so write is serialized. Test stages stale PID 99999999 and asserts identity preserved + PID changed. gap_check 137/137. Test suite 4012/4012 on re-run (first run had an unrelated WatchdogServiceTests Dispose file-lock flake on Windows; Charlie noted a similar parallel-execution flake; this commit does not touch WatchdogService). dydo check errors are all pre-existing in unrelated docs (decisions/inquisitions area+type schema drift, orphan task docs); none are introduced by this commit.

Awaiting human approval.

## Approval

- Approved: 2026-05-04 21:51
