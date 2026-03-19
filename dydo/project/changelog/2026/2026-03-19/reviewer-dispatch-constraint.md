---
area: general
type: changelog
date: 2026-03-19
---

# Task: reviewer-dispatch-constraint

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-reviewer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified


## Review Summary

Implemented dispatch-restriction constraint type. Changes: (1) Added FromRole to InboxItem, stored at dispatch time from sender's current role. (2) Added DispatchedByRole to AgentState, populated from inbox from_role during SetRole. (3) New CanDispatch method on RoleConstraintEvaluator checks dispatch-restriction constraints. (4) DispatchService.Execute calls CanDispatch before agent selection. (5) Added TargetRole to RoleConstraint model. (6) Validation for dispatch-restriction in RoleDefinitionService. (7) Updated reviewer base role definition with the new constraint. (8) 11 new tests across 3 test files. No plan deviations. Note: reviewer.role.json needs regeneration via 'dydo roles reset' after binary rebuild — the installed dydo binary is stale. Documentation updates (guardrails.md, dispatch-and-messaging.md, roles-and-permissions.md) are out of scope for code-writer — suggest dispatching a docs-writer.

## Code Review (2026-03-19 12:02)

- Reviewed by: Frank
- Result: FAILED
- Issues: Dead code: InboxMetadataReader.GetDispatchedFromRole() added but never called. AgentRegistry has its own duplicate. Remove the dead method.

Requires rework.

## Code Review

- Reviewed by: Frank
- Date: 2026-03-19 12:13
- Result: PASSED
- Notes: LGTM. Dead method GetDispatchedFromRole() cleanly removed. Zero callers confirmed — AgentRegistry has the real implementation. All 2762 tests pass. No coverage regressions.

Awaiting human approval.

## Approval

- Approved: 2026-03-19 13:04
