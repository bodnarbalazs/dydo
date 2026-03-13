---
area: general
type: changelog
date: 2026-03-13
---

# Task: project-issues-integration

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\AgentClaimValidatorTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigFactoryTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CleanCommandTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WatchdogCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WatchdogServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\AgentRegistryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ShellCompletionInstallerTests.cs — Modified


## Review Summary

Implemented issue command integration into scaffolding, templates, and docs. 13 files changed: new _issues.template.md, FolderScaffolder (folder + docfile), TemplateGenerator (GenerateIssuesMetaMd), RoleDefinitionService (judge writable paths), 5 mode templates (judge: issue-filing step; test-writer: report-back pattern; code-writer/reviewer/orchestrator: out-of-scope issues note), dydo-commands template (Issue Commands section + judge permissions), 2 test files updated for judge writable path assertions. No plan deviations. All tests pass (3 pre-existing failures unrelated).

## Code Review

- Reviewed by: Frank
- Date: 2026-03-11 22:34
- Result: PASSED
- Notes: LGTM. Issue integration is correct: new _issues.template.md, FolderScaffolder adds issues folder + major justified refactoring (325->111 lines), TemplateGenerator adds GenerateIssuesMetaMd and dynamic mode/template name derivation, RoleDefinitionService adds judge write access to issues, 5 mode templates updated with appropriate issue filing guidance, dydo-commands docs updated. Tests verify key behaviors. All templates consistent between embedded and overrides. 2 pre-existing FixCommand test failures unrelated.

Awaiting human approval.

## Approval

- Approved: 2026-03-13 17:32
