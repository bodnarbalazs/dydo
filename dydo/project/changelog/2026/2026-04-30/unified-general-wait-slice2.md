---
area: general
type: changelog
date: 2026-04-30
---

# Task: unified-general-wait-slice2

Slice 2 of unified-general-wait (Decision 021): generalised the missing-general-wait guard from orchestrator-only to all roles with a role set, dropped dispatcher's per-task wait registration, reshaped --wait into a release-block on the dispatched agent via a new DispatchWaitMarker.

Key changes:
- Commands/GuardCommand.cs: OrchestratorMissingGeneralWait → MissingGeneralWait. Drops the role short-circuit; gate is 'role is set' (stage 2+). Updated block message ('Agent must keep…').
- Models/DispatchWaitMarker.cs (new): {Task, DispatcherAgent, DispatcherRole, CreatedAt, RepliedAt?}.
- Serialization/DydoJsonContext.cs: registered new model for AOT.
- Services/AgentRegistry.cs: added CreateDispatchWaitMarker, GetDispatchWaitMarkers, MarkDispatchWaitReplied, GetUnrepliedDispatchWait, HasUnrepliedDispatchWait, ClearAllDispatchWaitMarkers; storage at dydo/agents/<callee>/dispatch-waits/<task>.json (temp-then-rename); release path passes dispatch-wait context to CanRelease; added 'dispatch-waits' to SystemManagedEntries so the marker survives claim like .reply-pending.
- Services/IAgentRegistry.cs: matching interface members.
- Services/DispatchService.cs: removed dispatcher-side CreateWaitMarker; on --wait writes a DispatchWaitMarker into the callee's workspace (extracted to WriteDispatchWaitMarkerIfNeeded helper to keep WriteAndLaunch CC under the T1 CRAP threshold); rewrote the post-dispatch console hint to describe the new release-block semantics; PrintReleaseHint now ignores _-prefixed sentinel waits when deciding whether to nudge.
- Services/RoleConstraintEvaluator.cs: CanRelease takes an optional getUnrepliedDispatchWait Func and blocks first if a marker exists, naming the dispatcher and expected subject.
- Services/MessageService.cs: outgoing dydo msg stamps RepliedAt when subject matches an open marker on the sender's task and the recipient is the marker's dispatcher.

Tests:
- New: PendingStateGuardTests.Guard_NonOrchestrator_RoleSet_NoGeneralWait_Blocks, Guard_NonOrchestrator_GeneralWaitActive_Passes, Guard_Orchestrator_StillBlocksWithoutGeneralWait, Guard_BlocksOrchestrator_OnceRoleSet_WithoutGeneralWait.
- New: DispatchWaitIntegrationTests.Dispatch_Wait_DoesNotCreateTaskWaitMarker, Dispatch_Wait_WritesDispatchWaitMarkerOnCallee, Dispatch_NoWait_DoesNotWriteCalleeMarker, Release_BlockedWhileDispatchWaitMarkerActive, Release_AllowedAfterMessageBack, Dispatch_Wait_NonOrchestratorRole_StillRejected, Dispatch_Wait_WithoutClaim_WritesMarkerWithNullDispatcherRole.
- New: RoleConstraintEvaluatorTests.CanRelease_WithDispatchWaitMarker_NoReply_Blocks, CanRelease_WithDispatchWaitMarker_RepliedStamp_Allows, CanRelease_NoDispatchWaitMarker_Allows.
- IntegrationTestBase.SetRoleAsync now auto-registers a listening _general-wait marker by default (mirrors Decision 021 'register-after-role'); tests that exercise the missing-general-wait block opt out via registerGeneralWait: false. This avoided having to hand-edit ~110 unrelated tests.
- Updated assertions in tests that previously expected the orchestrator-only block message and tests that asserted Single() over wait markers (now also contain the auto-injected sentinel).

Out of scope (per brief):
- Templates (Slice 3, already in working tree).
- Models/AgentState.cs (no schema change needed).
- Watchdog orphan-cleanup for DispatchWaitMarker (documented mitigation only, follow-up if needed).

Verification:
- python DynaDocs.Tests/coverage/run_tests.py: 3991 passed, 0 failed.
- python DynaDocs.Tests/coverage/gap_check.py: 137/137 modules pass tier requirements.

Notable decisions:
- DispatchWaitMarker coexists with the existing reply-pending mechanism rather than replacing it. Reply-pending is driven by inbox-item.replyRequired; dispatch-wait is the explicit --wait contract with DispatcherRole metadata, stamped on send rather than removed. Both must be satisfied to release. This matches the brief's explicit instruction to add a new model.
- 'dispatch-waits' added to SystemManagedEntries (alongside .reply-pending) so the marker survives ClaimAgent's archive sweep — without this, a callee that gets re-dispatched after a previous claim would lose its release-block obligation.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Slice 2 of unified-general-wait (Decision 021): generalised the missing-general-wait guard from orchestrator-only to all roles with a role set, dropped dispatcher's per-task wait registration, reshaped --wait into a release-block on the dispatched agent via a new DispatchWaitMarker.

Key changes:
- Commands/GuardCommand.cs: OrchestratorMissingGeneralWait → MissingGeneralWait. Drops the role short-circuit; gate is 'role is set' (stage 2+). Updated block message ('Agent must keep…').
- Models/DispatchWaitMarker.cs (new): {Task, DispatcherAgent, DispatcherRole, CreatedAt, RepliedAt?}.
- Serialization/DydoJsonContext.cs: registered new model for AOT.
- Services/AgentRegistry.cs: added CreateDispatchWaitMarker, GetDispatchWaitMarkers, MarkDispatchWaitReplied, GetUnrepliedDispatchWait, HasUnrepliedDispatchWait, ClearAllDispatchWaitMarkers; storage at dydo/agents/<callee>/dispatch-waits/<task>.json (temp-then-rename); release path passes dispatch-wait context to CanRelease; added 'dispatch-waits' to SystemManagedEntries so the marker survives claim like .reply-pending.
- Services/IAgentRegistry.cs: matching interface members.
- Services/DispatchService.cs: removed dispatcher-side CreateWaitMarker; on --wait writes a DispatchWaitMarker into the callee's workspace (extracted to WriteDispatchWaitMarkerIfNeeded helper to keep WriteAndLaunch CC under the T1 CRAP threshold); rewrote the post-dispatch console hint to describe the new release-block semantics; PrintReleaseHint now ignores _-prefixed sentinel waits when deciding whether to nudge.
- Services/RoleConstraintEvaluator.cs: CanRelease takes an optional getUnrepliedDispatchWait Func and blocks first if a marker exists, naming the dispatcher and expected subject.
- Services/MessageService.cs: outgoing dydo msg stamps RepliedAt when subject matches an open marker on the sender's task and the recipient is the marker's dispatcher.

Tests:
- New: PendingStateGuardTests.Guard_NonOrchestrator_RoleSet_NoGeneralWait_Blocks, Guard_NonOrchestrator_GeneralWaitActive_Passes, Guard_Orchestrator_StillBlocksWithoutGeneralWait, Guard_BlocksOrchestrator_OnceRoleSet_WithoutGeneralWait.
- New: DispatchWaitIntegrationTests.Dispatch_Wait_DoesNotCreateTaskWaitMarker, Dispatch_Wait_WritesDispatchWaitMarkerOnCallee, Dispatch_NoWait_DoesNotWriteCalleeMarker, Release_BlockedWhileDispatchWaitMarkerActive, Release_AllowedAfterMessageBack, Dispatch_Wait_NonOrchestratorRole_StillRejected, Dispatch_Wait_WithoutClaim_WritesMarkerWithNullDispatcherRole.
- New: RoleConstraintEvaluatorTests.CanRelease_WithDispatchWaitMarker_NoReply_Blocks, CanRelease_WithDispatchWaitMarker_RepliedStamp_Allows, CanRelease_NoDispatchWaitMarker_Allows.
- IntegrationTestBase.SetRoleAsync now auto-registers a listening _general-wait marker by default (mirrors Decision 021 'register-after-role'); tests that exercise the missing-general-wait block opt out via registerGeneralWait: false. This avoided having to hand-edit ~110 unrelated tests.
- Updated assertions in tests that previously expected the orchestrator-only block message and tests that asserted Single() over wait markers (now also contain the auto-injected sentinel).

Out of scope (per brief):
- Templates (Slice 3, already in working tree).
- Models/AgentState.cs (no schema change needed).
- Watchdog orphan-cleanup for DispatchWaitMarker (documented mitigation only, follow-up if needed).

Verification:
- python DynaDocs.Tests/coverage/run_tests.py: 3991 passed, 0 failed.
- python DynaDocs.Tests/coverage/gap_check.py: 137/137 modules pass tier requirements.

Notable decisions:
- DispatchWaitMarker coexists with the existing reply-pending mechanism rather than replacing it. Reply-pending is driven by inbox-item.replyRequired; dispatch-wait is the explicit --wait contract with DispatcherRole metadata, stamped on send rather than removed. Both must be satisfied to release. This matches the brief's explicit instruction to add a new model.
- 'dispatch-waits' added to SystemManagedEntries (alongside .reply-pending) so the marker survives ClaimAgent's archive sweep — without this, a callee that gets re-dispatched after a previous claim would lose its release-block obligation.

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-30 12:30
- Result: PASSED
- Notes: PASS. Implementation maps cleanly to Decision 021. Guard MissingGeneralWait now gates on role-set, dispatch --wait writes a callee-side DispatchWaitMarker (atomic temp-then-rename, written before terminal launch), release blocks first on the dispatch-wait obligation with an actionable error, MessageService stamps RepliedAt on subject+recipient match. dispatch-waits added to SystemManagedEntries so markers survive claim. PrintReleaseHint filters _-prefixed sentinels. Tests cover the universal-block path, orchestrator regression, marker create/no-create, full release lifecycle (blocked then allowed-after-msg), null-role branch, and unchanged privilege gate. Coverage gate green: gap_check exit 0, 137/137 modules, 3991 tests passing. Minor non-blocking nits in review-notes.md (naming convention: .reply-pending vs dispatch-waits).

Awaiting human approval.

## Approval

- Approved: 2026-04-30 12:51
