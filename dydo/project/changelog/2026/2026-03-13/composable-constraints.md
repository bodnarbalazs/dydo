---
area: general
type: changelog
date: 2026-03-13
---

# Task: composable-constraints

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\RolesCreateCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CheckAgentValidatorTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\dydo-commands.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TemplateCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\InquisitionCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\ShellCompletionInstaller.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WorkspaceCleaner.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\ProcessUtils.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TemplateGenerator.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\TemplateOverrideTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TemplateGeneratorTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\RoleBehaviorTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigurablePathsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\RoleDefinitionServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConstraintEvaluationTests.cs — Modified


## Review Summary

Slice 2: Removed hardcoded constraint fallback. CanTakeRole() now evaluates purely through EvaluateConstraint() from RoleDefinition data. Removed CanTakeRoleFallback(), BuildRolePermissions(), and hardcoded GetRoleRestrictionMessage() switch. Constructor fallback (no role files) now uses GetBaseRoleDefinitions() with a stderr warning. All 1946 tests pass. Updated 3 test files to use RoleDefinitionService.BuildPermissionMap() instead of the removed static method. Added 3 new tests: no-role-files warning, DenialHint lookup, unconstrained role trivially allowed.

## Code Review

- Reviewed by: Jack
- Date: 2026-03-11 15:22
- Result: PASSED
- Notes: All 3 fixes verified: (1) Duplicate unknown-constraint-type check correctly removed — ValidateRoleDefinition default case handles it. KnownConstraintTypes cleaned up. (2) Stale auto-close clearing in ClaimAgent correctly placed — prevents watchdog killing human sessions. (3) Unnecessary partial keyword removed from ValidationService. All 1946 tests pass. Code is clean and minimal.

Awaiting human approval.

## Approval

- Approved: 2026-03-13 17:32
