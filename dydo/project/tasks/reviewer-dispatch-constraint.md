---
area: general
name: reviewer-dispatch-constraint
status: review-failed
created: 2026-03-18T22:05:26.5638835Z
assigned: Frank
updated: 2026-03-19T11:54:02.2185270Z
---

# Task: reviewer-dispatch-constraint

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented dispatch-restriction constraint type. Changes: (1) Added FromRole to InboxItem, stored at dispatch time from sender's current role. (2) Added DispatchedByRole to AgentState, populated from inbox from_role during SetRole. (3) New CanDispatch method on RoleConstraintEvaluator checks dispatch-restriction constraints. (4) DispatchService.Execute calls CanDispatch before agent selection. (5) Added TargetRole to RoleConstraint model. (6) Validation for dispatch-restriction in RoleDefinitionService. (7) Updated reviewer base role definition with the new constraint. (8) 11 new tests across 3 test files. No plan deviations. Note: reviewer.role.json needs regeneration via 'dydo roles reset' after binary rebuild — the installed dydo binary is stale. Documentation updates (guardrails.md, dispatch-and-messaging.md, roles-and-permissions.md) are out of scope for code-writer — suggest dispatching a docs-writer.

## Code Review (2026-03-19 12:02)

- Reviewed by: Frank
- Result: FAILED
- Issues: Dead code: InboxMetadataReader.GetDispatchedFromRole() added but never called. AgentRegistry has its own duplicate. Remove the dead method.

Requires rework.