---
area: general
type: changelog
date: 2026-05-04
---

# Task: fix-wait-race

Review commit b33a171 for fix-wait-race (#0147). Plan: dydo/agents/Mia/plan-investigate-wait-race.md. Brief: dydo/agents/Brian/brief-fix-wait-race.md. Note: human committed the work in b33a171 with message '.' bundled with unrelated changes; the fix-wait-race scope is Commands/WaitCommand.cs, Services/MessageFinder.cs, Services/MessageService.cs, DynaDocs.Tests/Integration/WaitCommandTests.cs, DynaDocs.Tests/Integration/MessageIntegrationTests.cs. Verify: WaitGeneral re-reads UnreadMessages each poll (no snapshot), MessageFinder.FindMessage gets includeIds param + helper extraction (MatchesSubject, MatchesIdFilter) to keep CC under tier-1 threshold, MessageService.DeliverInboxMessage drops Working-only conditional, 6 new tests + helper change + 3 existing test updates (deletion of obsolete WaitGeneral_SkipsMessage_AlreadyInUnreadAtStart, ClearAllUnreadMessages calls in two tests that simulate post-Read state, inverted Message_ToInactiveAgent_WithForce_UpdatesUnreadMessages), issue #0147 body filled. All 4017 tests pass. gap_check exit 0 (137/137 modules). Run dydo check (per the new docs-quality workflow). Approve or reject.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit b33a171 for fix-wait-race (#0147). Plan: dydo/agents/Mia/plan-investigate-wait-race.md. Brief: dydo/agents/Brian/brief-fix-wait-race.md. Note: human committed the work in b33a171 with message '.' bundled with unrelated changes; the fix-wait-race scope is Commands/WaitCommand.cs, Services/MessageFinder.cs, Services/MessageService.cs, DynaDocs.Tests/Integration/WaitCommandTests.cs, DynaDocs.Tests/Integration/MessageIntegrationTests.cs. Verify: WaitGeneral re-reads UnreadMessages each poll (no snapshot), MessageFinder.FindMessage gets includeIds param + helper extraction (MatchesSubject, MatchesIdFilter) to keep CC under tier-1 threshold, MessageService.DeliverInboxMessage drops Working-only conditional, 6 new tests + helper change + 3 existing test updates (deletion of obsolete WaitGeneral_SkipsMessage_AlreadyInUnreadAtStart, ClearAllUnreadMessages calls in two tests that simulate post-Read state, inverted Message_ToInactiveAgent_WithForce_UpdatesUnreadMessages), issue #0147 body filled. All 4017 tests pass. gap_check exit 0 (137/137 modules). Run dydo check (per the new docs-quality workflow). Approve or reject.

## Code Review

- Reviewed by: Charlie
- Date: 2026-05-01 16:39
- Result: PASSED
- Notes: fix-wait-race scope (Commands/WaitCommand.cs, Services/MessageFinder.cs, Services/MessageService.cs, two test files) implements Mia's plan exactly: (1) WaitGeneral re-reads state.md.UnreadMessages each poll as the inclusion set, drops the registration-time snapshot — eliminates the W1-exit/W2-register race (#0147) and preserves the #0141 deadlock fix because already-Read ids are no longer in the set; (2) MessageFinder.FindMessage gains optional includeIds with MatchesSubject/MatchesIdFilter helper extraction (keeps CC under tier-1 threshold); (3) MessageService.DeliverInboxMessage drops the Working-only conditional so file-on-disk implies id-in-UnreadMessages regardless of target status; (4) test helper CreateMessageFileReturningId now also calls AddUnreadMessage so all callers exercise the unified semantics. 6 new tests added (WaitGeneral_FiresOnSecondMessage_ArrivedDuringRearmGap, WaitGeneral_DoesNotFire_WhenInboxFileExistsButUnreadMessagesEmpty, WaitGeneral_FiresOnArrivalDuringActiveWait_NotJustPostRegistration, DeliverInboxMessage_AddsToUnreadMessages_EvenWhenTargetReleased, FindMessage_IncludeIds_OnlyMatchesListedIds, FindMessage_IncludeIds_EmptySet_ReturnsNull). Obsolete WaitGeneral_SkipsMessage_AlreadyInUnreadAtStart deleted; Message_ToInactiveAgent_WithForce inverted from DoesNotUpdate to Updates with #0147 rationale; two existing post-Read tests updated to call ClearAllUnreadMessages. Issue #0147 body filled with description, reproduction, and resolution. All 4017 tests pass; gap_check exit 0 (137/137 modules). One isolated flake on first full run (StaleDispatchDoubleClaimTests.ReserveAgent_StaleAndNoLauncher_Succeeds) — passed in isolation and on full re-run via gap_check, unrelated [Collection ProcessUtils] interference. dydo check shows 13 errors / 25 warnings — all pre-existing schema drift (inquisition type not in schema; template-additions missing titles) or bundled-but-unrelated changes (issue 0148, plan-dydo-tool-fixes), none introduced by fix-wait-race scope. Note: the human's commit b33a171 bundled the fix-wait-race work with unrelated orchestrator changes under message '.', which obscures the changelog — flagging for the human to consider.

Awaiting human approval.

## Approval

- Approved: 2026-05-04 21:52
