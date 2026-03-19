---
area: general
type: changelog
date: 2026-03-19
---

# Task: guard-python-invocation

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Models\DispatchMarker.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\InboxMetadataReader.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\BashCommandAnalyzer.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\RoleConstraint.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\RoleDefinition.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Serialization\DydoJsonContext.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MarkerStore.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\IAgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\RoleConstraintEvaluator.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\RoleDefinitionService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WorkspaceCleaner.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CheckAgentValidatorTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\MarkerStoreTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\RoleConstraintEvaluatorTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchWaitIntegrationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\RoleDefinitionServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\LinuxTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MacTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\AgentRegistryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CommandDocConsistencyTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TemplateGeneratorTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigurablePathsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-reviewer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TemplateGenerator.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/Commands/GuardCommand.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Integration/GuardIntegrationTests.cs — Modified


## Review Summary

Added python/python3/py to the indirect dydo invocation guard. New regex catches python-based attempts to run dydo and redirects agents to use dydo directly. All 59 indirect dydo tests pass including new python cases and false-positive safety for legitimate python commands.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-16 18:59
- Result: PASSED
- Notes: LGTM. Regex mirrors established shell pattern, all 59 tests pass, false-positive safety is solid. Clean, minimal change.

Awaiting human approval.

## Approval

- Approved: 2026-03-19 13:03
