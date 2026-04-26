---
area: general
type: changelog
date: 2026-04-26
---

# Task: reviewer-verdict-routing

Implemented Frank's design (option A variant). Items: (1) dydo review complete --status pass now auto-sends a verdict message to the dispatcher AND CCs the nearest canOrchestrate ancestor via DispatchedBy chain walk (ReviewCommand.RouteVerdictMessages, FindNearestCanOrchestrateAncestor). MessageService.DeliverInboxMessage is the new public helper that bypasses ownership/active checks for system-initiated delivery. (2) MessageService.BuildInactiveTargetMessage now appends 'Agents waiting on subject <X>' when the recipient is released and other agents have a matching wait marker (AgentRegistry.GetAgentsWaitingForSubject). (3) MessageService.WarnOnSubjectMismatch emits a stderr warning when recipient is active, has only specific wait markers, and none matches the subject. 5 reproducer tests in DynaDocs.Tests/Integration/ReviewerVerdictRoutingTests.cs — red before, green after. Full suite 3776/3776 green, gap_check 100% modules passing. No plan deviations. Key decision: reviewer's auto-generated verdict message clears the reply-pending marker for the dispatcher, matching existing manual dydo msg semantics. The review-complete trigger point (rather than a later hook) keeps the behavior synchronous with the status update and avoids racing with the reviewer's subsequent release. Files touched: Commands/ReviewCommand.cs, Services/MessageService.cs, Services/AgentRegistry.cs (new GetAgentsWaitingForSubject helper), DynaDocs.Tests/Integration/ReviewerVerdictRoutingTests.cs (new).

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented Frank's design (option A variant). Items: (1) dydo review complete --status pass now auto-sends a verdict message to the dispatcher AND CCs the nearest canOrchestrate ancestor via DispatchedBy chain walk (ReviewCommand.RouteVerdictMessages, FindNearestCanOrchestrateAncestor). MessageService.DeliverInboxMessage is the new public helper that bypasses ownership/active checks for system-initiated delivery. (2) MessageService.BuildInactiveTargetMessage now appends 'Agents waiting on subject <X>' when the recipient is released and other agents have a matching wait marker (AgentRegistry.GetAgentsWaitingForSubject). (3) MessageService.WarnOnSubjectMismatch emits a stderr warning when recipient is active, has only specific wait markers, and none matches the subject. 5 reproducer tests in DynaDocs.Tests/Integration/ReviewerVerdictRoutingTests.cs — red before, green after. Full suite 3776/3776 green, gap_check 100% modules passing. No plan deviations. Key decision: reviewer's auto-generated verdict message clears the reply-pending marker for the dispatcher, matching existing manual dydo msg semantics. The review-complete trigger point (rather than a later hook) keeps the behavior synchronous with the status update and avoids racing with the reviewer's subsequent release. Files touched: Commands/ReviewCommand.cs, Services/MessageService.cs, Services/AgentRegistry.cs (new GetAgentsWaitingForSubject helper), DynaDocs.Tests/Integration/ReviewerVerdictRoutingTests.cs (new).

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-20 18:53
- Result: PASSED
- Notes: LGTM. Diff is surgical: AgentRegistry.cs untouched as promised; DeliverInboxMessage cleanly extracted with Execute composing it; GetAgentsWaitingForSubject is a private static in MessageService composed from existing public registry APIs; decision doc 019 documents the rationale (PASS-only CC, review-complete trigger point, DeliverInboxMessage scoping). Full suite 3776/3776 green (one xUnit cross-collection CWD flake in WatchdogServiceTests.Dispose on first run did not reproduce on force-run — pre-existing parallelism issue in PathUtilsDiscoveryTests, unrelated to this diff). gap_check 136/136 modules passing, exit 0. The 5 reproducer tests cover ancestor-CC, no-ancestor no-CC, FAIL no-CC, released-target waiters listing, and subject-mismatch warning. Ancestor walk correctly terminates when dispatcher itself has canOrchestrate.

Awaiting human approval.

## Approval

- Approved: 2026-04-26 19:39
