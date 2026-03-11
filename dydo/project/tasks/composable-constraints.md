---
area: general
name: composable-constraints
status: human-reviewed
created: 2026-03-11T14:50:59.4118926Z
assigned: Dexter
---

# Task: composable-constraints

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Slice 2: Removed hardcoded constraint fallback. CanTakeRole() now evaluates purely through EvaluateConstraint() from RoleDefinition data. Removed CanTakeRoleFallback(), BuildRolePermissions(), and hardcoded GetRoleRestrictionMessage() switch. Constructor fallback (no role files) now uses GetBaseRoleDefinitions() with a stderr warning. All 1946 tests pass. Updated 3 test files to use RoleDefinitionService.BuildPermissionMap() instead of the removed static method. Added 3 new tests: no-role-files warning, DenialHint lookup, unconstrained role trivially allowed.

## Code Review

- Reviewed by: Jack
- Date: 2026-03-11 15:22
- Result: PASSED
- Notes: All 3 fixes verified: (1) Duplicate unknown-constraint-type check correctly removed — ValidateRoleDefinition default case handles it. KnownConstraintTypes cleaned up. (2) Stale auto-close clearing in ClaimAgent correctly placed — prevents watchdog killing human sessions. (3) Unnecessary partial keyword removed from ValidationService. All 1946 tests pass. Code is clean and minimal.

Awaiting human approval.