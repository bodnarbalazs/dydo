---
area: general
name: fix-inquisitor-release-constraint
status: human-reviewed
created: 2026-03-30T15:00:57.9576901Z
assigned: Iris
updated: 2026-03-30T16:32:55.3464670Z
---

# Task: fix-inquisitor-release-constraint

Added RequireAll property to RoleConstraint (default true, preserving ALL semantics). When false, requires-dispatch accepts ANY of the listed roles. Updated inquisitor constraint to requiredRoles=[judge, inquisitor] with requireAll=false, so dispatching either a judge or another inquisitor satisfies the release requirement. Updated RoleDefinitionService (source of truth) and inquisitor.role.json. 5 new tests: 3 unit (RequireAll ANY/ALL/default), 1 integration (inquisitor re-dispatch release), 1 round-trip (RequireAll serialization).

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Added RequireAll property to RoleConstraint (default true, preserving ALL semantics). When false, requires-dispatch accepts ANY of the listed roles. Updated inquisitor constraint to requiredRoles=[judge, inquisitor] with requireAll=false, so dispatching either a judge or another inquisitor satisfies the release requirement. Updated RoleDefinitionService (source of truth) and inquisitor.role.json. 5 new tests: 3 unit (RequireAll ANY/ALL/default), 1 integration (inquisitor re-dispatch release), 1 round-trip (RequireAll serialization).

## Code Review

- Reviewed by: Grace
- Date: 2026-03-30 16:38
- Result: PASSED
- Notes: LGTM. RequireAll property is clean, backward-compatible (defaults true), and correctly implements ANY semantics. Evaluator branching is clear. dispatchedByRole bypass interacts correctly with expanded RequiredRoles. RoleDefinitionService and JSON in sync. 5 well-targeted tests. All 3312 tests pass, coverage gate 131/131 modules.

Awaiting human approval.