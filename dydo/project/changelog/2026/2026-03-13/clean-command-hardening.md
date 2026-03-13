---
area: platform
type: changelog
date: 2026-03-13
---

# Task: clean-command-hardening

Extend CleanWorkspace to remove .waiting, .reply-pending, .auto-close markers and audit stale wait PIDs

## Progress

- [ ] (Not started)

## Files Changed

C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/coverage/report.py — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\coverage\coverage.runsettings — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\coverage\tier_registry.json — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\coverage\gap_check.py — Created
C:\Users\User\Desktop\Projects\DynaDocs\Models\RoleDefinition.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Models\RoleConstraint.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\IRoleDefinitionService.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\RoleDefinitionService.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Commands\RolesCommand.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\RoleDefinitionServiceTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConstraintEvaluationTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Templates\agent-workflow.template.md — Created
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-test-writer.template.md — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WatchdogCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\ProcessUtils.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ProcessUtilsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TemplateGeneratorTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TemplateGenerator.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TemplateCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\FolderScaffolder.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\TemplateOverrideTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\AgentRegistryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\IntegrationTestBase.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\CleanCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\WorkspaceAndCleanTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Serialization\DydoJsonContext.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\PathsConfig.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Program.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-co-thinker.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\AgentCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\CompleteCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\FolderScaffolderTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigurablePathsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\InitCheckIntegrationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CompleteCommandTests.cs — Modified


## Review Summary

Implemented Slice 2: CleanWorkspace now removes .waiting/, .reply-pending/, and .auto-close artifacts. Added wait marker audit that counts markers before/after clean and reports (format: 'Audit: found N stale wait marker(s), cleaned M'). Audit runs in CleanAgent and CleanAll. No plan deviations. Omitted Clean_AuditPreservesAliveListeners test since WaitMarker lacks pid/listening fields (Slice 1 not landed). Pre-existing test compilation failure (ProcessUtils.PowerShellResolverOverride internal visibility) prevents running tests — unrelated to these changes.

## Code Review (2026-03-09 13:00)

- Reviewed by: Henry
- Result: FAILED
- Issues: Two issues: (1) Audit scope bug — CountWaitMarkers counts ALL agents globally but CleanAgent only cleans one, inflating the 'found N' number. (2) Three what-comments in CleanWorkspace restate the obvious and violate coding standards.

Requires rework.

## Code Review

- Reviewed by: Henry
- Date: 2026-03-09 13:10
- Result: PASSED
- Notes: LGTM. Audit scoped correctly per agent, what-comments removed, tests valid.

Awaiting human approval.

## Approval

- Approved: 2026-03-13 17:32
