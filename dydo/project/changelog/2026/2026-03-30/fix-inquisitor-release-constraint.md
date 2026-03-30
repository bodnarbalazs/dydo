---
area: general
type: changelog
date: 2026-03-30
---

# Task: fix-inquisitor-release-constraint

Added RequireAll property to RoleConstraint (default true, preserving ALL semantics). When false, requires-dispatch accepts ANY of the listed roles. Updated inquisitor constraint to requiredRoles=[judge, inquisitor] with requireAll=false, so dispatching either a judge or another inquisitor satisfies the release requirement. Updated RoleDefinitionService (source of truth) and inquisitor.role.json. 5 new tests: 3 unit (RequireAll ANY/ALL/default), 1 integration (inquisitor re-dispatch release), 1 round-trip (RequireAll serialization).

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\RoleConstraintEvaluatorTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\RoleConstraint.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\RoleConstraintEvaluator.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\RoleDefinitionService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchWaitIntegrationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\RoleDefinitionServiceTests.cs — Modified


## Review Summary

Added RequireAll property to RoleConstraint (default true, preserving ALL semantics). When false, requires-dispatch accepts ANY of the listed roles. Updated inquisitor constraint to requiredRoles=[judge, inquisitor] with requireAll=false, so dispatching either a judge or another inquisitor satisfies the release requirement. Updated RoleDefinitionService (source of truth) and inquisitor.role.json. 5 new tests: 3 unit (RequireAll ANY/ALL/default), 1 integration (inquisitor re-dispatch release), 1 round-trip (RequireAll serialization).

## Code Review

- Reviewed by: Grace
- Date: 2026-03-30 16:38
- Result: PASSED
- Notes: LGTM. RequireAll property is clean, backward-compatible (defaults true), and correctly implements ANY semantics. Evaluator branching is clear. dispatchedByRole bypass interacts correctly with expanded RequiredRoles. RoleDefinitionService and JSON in sync. 5 well-targeted tests. All 3312 tests pass, coverage gate 131/131 modules.

Awaiting human approval.

## Approval

- Approved: 2026-03-30 17:16
