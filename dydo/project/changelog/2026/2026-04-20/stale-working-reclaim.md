---
area: platform
type: changelog
date: 2026-04-20
---

# Task: stale-working-reclaim

Issue #103: no reclaim path for Working agents with dead Claude process. Extends decision 017's IsReservable to cover stale Working + dead .session PID.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review stale-working reclaim (issue #103). Implements decision 018 section 2: status=Working past 5min with dead session PID becomes reservable, mirroring decision 017's stale-dispatch pattern. 

Files touched:
- Models/AgentSession.cs: added nullable ClaimedPid (backward-compat for old .session files).
- Services/AgentRegistry.cs: StaleWorkingMinutes=5, IsStaleWorking, IsSessionPidAlive (+IsSessionPidAliveOverride for tests), extended IsEffectivelyFree with stale-working OR branch, added IsReservable per decision 017, stale-working branch in HandleExistingSession (prints claim-time hint), ResolveClaimedPid using FindAncestorProcess(claude) with parent-shell-pid fallback. ReserveAgent now calls IsReservable.
- DynaDocs.Tests/Services/StaleWorkingReclaimTests.cs: new, 7 tests covering ReserveAgent and GetFreeAgents paths plus missing-session / missing-pid edge cases. Uses [Collection(ProcessUtils)].
- DynaDocs.Tests/Services/AgentRegistryTests.cs: the 3 stale-dispatch tests now set IsLauncherAliveOverride=_=>false (required by Adele's uncommitted IsEffectivelyFree strictness). Dispose nulls IsLauncherAliveOverride.
- DynaDocs.Tests/Commands/AgentListHandlerTests.cs: WriteAgentState now uses fresh started timestamp so Working agents aren't interpreted as stale-reclaim candidates.

Adele's uncommitted edits to Services/AgentRegistry.cs (IsEffectivelyFree + IsLauncherAliveOverride + IsLauncherAlive) are untouched and extended additively.

PID-source choice: FindAncestorProcess(claude) primary, parent-pid fallback. Matches WaitCommand's liveness pattern. Needs capture in decision 018 follow-ups by docs-writer (out of scope for this task).

Verification:
- python DynaDocs.Tests/coverage/run_tests.py -- --filter FullyQualifiedName~StaleWorking : 7/7 pass
- python DynaDocs.Tests/coverage/gap_check.py : 136/136 modules pass tier
- Full suite: 3744/3746 pass. The 2 failures are in DynaDocs.Tests/Commands/PhantomUnreadInboxTests.cs (Charlie's in-progress phantom-unread-inbox work; explicitly carved out in my brief). Brian confirmed to disregard them for this review.

Out-of-scope follow-ups (do not fix here):
- Decision 018 Consequences section needs to record the shell-PID-fallback PID-source choice (docs-writer dispatch).
- Issue #103 needs to be marked resolved (docs/issues scope).

## Code Review (2026-04-20 12:50)

- Reviewed by: Henry
- Result: FAILED
- Issues: FAIL. (1) IsReservable contains dead code - its trailing guards duplicate checks already in IsEffectivelyFree, never fire. (2) Compounds decision 017's permissive/strict split by putting the session-pid gate inside IsEffectivelyFree instead of only in IsReservable. Full feedback in dydo/agents/Henry/review-feedback.md.

Requires rework.

## Code Review

- Reviewed by: Frank
- Date: 2026-04-20 13:15
- Result: PASSED
- Notes: LGTM. Both of Henry's blockers resolved via Option B (decision-017-aligned).

Blocker 1 (dead code in IsReservable) — FIXED. IsEffectivelyFree's stale-working clause is now permissive (just IsStaleWorking(state), no PID gate), so the IsReservable guard '!(IsStaleWorking(state) && IsSessionPidAlive(state.Name))' now actually fires when the PID is alive. The two predicates have meaningfully different behavior.

Blocker 2 (decision 017 alignment) — FIXED for stale-working. IsEffectivelyFree keeps the new stale-working clause permissive (display surfaces reclaim candidates regardless of PID liveness), matching decision 017 rationale #2. Adele's uncommitted stale-dispatch clause retaining !IsLauncherAlive is correctly flagged as out-of-scope and does not worsen the divergence.

Comment on IsReservable rewritten to describe real behavior instead of the prior redundancy-as-self-documentation.

Tests:
- DynaDocs.Tests/Services/StaleWorkingReclaimTests.cs: 7/7 pass. The renamed GetFreeAgents_IncludesStaleWorkingRegardlessOfPid correctly enforces permissive display by making IsSessionPidAliveOverride throw if the display path consults it. The moved ReserveAgent_StaleWorkingWithNoSessionFile_Succeeds and ReserveAgent_StaleWorkingWithNoClaimedPidInSession_Succeeds verify the reservation-path fallbacks now that those probes only matter there.

Verification:
- python DynaDocs.Tests/coverage/run_tests.py -- --filter FullyQualifiedName~StaleWorking: 7/7 pass
- python DynaDocs.Tests/coverage/gap_check.py: exit 0, 136/136 modules pass tier
- Full suite: 3744/3746. The 2 PhantomUnreadInboxTests failures are Charlie's carved-out work (confirmed pre-existing, disregarded per Brian/Henry).

Awaiting human approval.

## Approval

- Approved: 2026-04-20 16:03
