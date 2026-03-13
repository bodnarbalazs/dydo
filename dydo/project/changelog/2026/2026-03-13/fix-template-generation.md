---
area: general
type: changelog
date: 2026-03-13
---

# Task: fix-template-generation

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WatchdogCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TemplateGeneratorTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TemplateGenerator.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TemplateCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\FolderScaffolder.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\TemplateOverrideTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\AgentRegistryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchCommandTests.cs — Modified


## Review Summary

Fixed hardcoded template/mode lists missing 3 roles (orchestrator, inquisitor, judge). (1) TemplateGenerator.GetModeNames() now derives from RoleDefinitionService.GetBaseRoleDefinitions(). (2) TemplateGenerator.GetAllTemplateNames() likewise derives from role definitions. (3) TemplateCommand.FrameworkTemplateFiles derives from GetAllTemplateNames(). (4) FolderScaffolder adds project/inquisitions to scaffold spec. Updated 2 tests to be data-driven instead of hardcoded counts. All 1951 tests pass.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-11 17:15
- Result: PASSED
- Notes: LGTM. Clean refactoring: all hardcoded mode/template lists now derive from RoleDefinitionService.GetBaseRoleDefinitions(). Tests properly converted to data-driven [MemberData]. All 101 template-related tests pass. No unnecessary complexity, no slop.

Awaiting human approval.

## Approval

- Approved: 2026-03-13 17:32
