---
area: general
type: changelog
date: 2026-04-29
---

# Task: fix-inbox-pollution-released-agents

Implement Adele's brief / Leo's plan. Two pollution paths closed:

1. Commands/ReviewCommand.cs RouteVerdictMessages — dispatcher write gated on Status==Working; FindNearestCanOrchestrateAncestor renamed to FindNearestWorkingCanOrchestrateAncestor and its walk now skips released intermediates instead of stopping on the first non-Working node.
2. Services/MessageService.cs CheckTargetActive — reply-pending bypass dropped; sends to non-Working targets hard-reject. BuildInactiveTargetMessage gained an optional pendingReplyTask hint pointing to --force or active orchestrator. --force escape hatch unchanged.

Tests: full suite green (3883 passed, 0 failed). Coverage gap_check: 100% module pass.

Files changed:
- Commands/ReviewCommand.cs
- Services/MessageService.cs
- DynaDocs.Tests/Integration/ReviewerVerdictRoutingTests.cs (3 new tests + 2 existing happy-path tests updated to set status: human-reviewed since the prior code did not check status; existing tests would now fail without the update)
- DynaDocs.Tests/Integration/MessageIntegrationTests.cs (2 reply-pending bypass tests flipped from Allows to Fails; 1 new hint test)

Plan deviation: dydo/understand/dispatch-and-messaging.md doc update not made — code-writer role guard blocks dydo/** writes. Needs docs-writer follow-up. The relevant bullet is at line 126 'Inactive target warning' — reword to reflect hard-reject + --force escape.

No worktree per orchestrator. Commit on master: 8c94988.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implement Adele's brief / Leo's plan. Two pollution paths closed:

1. Commands/ReviewCommand.cs RouteVerdictMessages — dispatcher write gated on Status==Working; FindNearestCanOrchestrateAncestor renamed to FindNearestWorkingCanOrchestrateAncestor and its walk now skips released intermediates instead of stopping on the first non-Working node.
2. Services/MessageService.cs CheckTargetActive — reply-pending bypass dropped; sends to non-Working targets hard-reject. BuildInactiveTargetMessage gained an optional pendingReplyTask hint pointing to --force or active orchestrator. --force escape hatch unchanged.

Tests: full suite green (3883 passed, 0 failed). Coverage gap_check: 100% module pass.

Files changed:
- Commands/ReviewCommand.cs
- Services/MessageService.cs
- DynaDocs.Tests/Integration/ReviewerVerdictRoutingTests.cs (3 new tests + 2 existing happy-path tests updated to set status: human-reviewed since the prior code did not check status; existing tests would now fail without the update)
- DynaDocs.Tests/Integration/MessageIntegrationTests.cs (2 reply-pending bypass tests flipped from Allows to Fails; 1 new hint test)

Plan deviation: dydo/understand/dispatch-and-messaging.md doc update not made — code-writer role guard blocks dydo/** writes. Needs docs-writer follow-up. The relevant bullet is at line 126 'Inactive target warning' — reword to reflect hard-reject + --force escape.

No worktree per orchestrator. Commit on master: 8c94988.

## Code Review

- Reviewed by: Mia
- Date: 2026-04-28 21:19
- Result: PASSED
- Notes: Implementation matches plan exactly. RouteVerdictMessages dispatcher gate + FindNearestWorkingCanOrchestrateAncestor walk are correct; CheckTargetActive hard-reject + pendingReplyTask hint flow are clean. Tests (3 new routing + 3 message integration) cover the pollution paths. Worktree runner: 3883 pass / 0 fail. gap_check: 136/136 modules pass. Pre-existing [review-debug] lines in ReviewCommand.cs:131,135 are noise but pre-date this task (Mar 22) — flagged not blocked. Doc update on dydo/understand/dispatch-and-messaging.md line 126 deferred to docs-writer per plan deviation.

Awaiting human approval.

## Approval

- Approved: 2026-04-29 12:04
