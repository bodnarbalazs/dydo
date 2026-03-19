---
area: general
type: changelog
date: 2026-03-19
---

# Task: baton-passing-docs

(No description)

## Progress

- [x] Updated guardrails.md — H15 updated for baton-passing / reply_required decoupling, H25 added for dispatched code-writer review enforcement
- [x] Updated code-writer role doc — dispatch pattern changed to `--no-wait`, H25 reference added, baton-passing explained
- [x] Updated reviewer role doc — dispatch changed to `--no-wait`, on-pass section documents reply obligation messaging
- [x] Updated planner role doc — removed `--wait` option, removed dispatch-and-wait transition option
- [x] Added summary paragraph to decision 010, ran `dydo fix` to regenerate indexes
- [ ] Template files (`Templates/mode-*.template.md`) — read-only for docs-writer, need code-writer to update

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
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\IntegrationTestBase.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/Commands/GuardCommand.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Integration/GuardIntegrationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardIntegrationTests.cs — Modified


## Review Summary

(Pending)

## Notes for Brian

Template files (`Templates/mode-code-writer.template.md`, `mode-reviewer.template.md`, `mode-planner.template.md`) need matching updates but are outside docs-writer write permissions. Specific changes needed:
- **code-writer template**: Remove "Reporting back to origin?" manual messaging section (baton-passing makes this unnecessary when dispatching reviewer for same task)
- **planner template**: Add `--auto-close` to the dispatch example (line 129)
- **reviewer template**: Add upstream messaging in the PASS completion section (reply obligation for last-in-chain agents)

## Approval

- Approved: 2026-03-19 13:03
