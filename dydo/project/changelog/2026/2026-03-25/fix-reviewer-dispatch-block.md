---
area: general
type: changelog
date: 2026-03-25
---

# Task: fix-reviewer-dispatch-block

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Templates\how-to-merge-worktrees.template.md — Created
C:\Users\User\Desktop\Projects\DynaDocs\Templates\how-to-review-worktree-merges.template.md — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\ConditionalMustReadTests.cs — Created
C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Models/HookInputTests.cs — Created
C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Models/TaskFileTests.cs — Created
C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Models/ToolInputDataTests.cs — Created
C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Services/BashAnalysisResultTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\InboxItemParserTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MustReadTracker.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TemplateGenerator.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\FolderScaffolder.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TemplateCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-code-writer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-orchestrator.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\MustReadTrackerTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\BashCommandAnalyzer.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Utils\PathUtils.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\BashCommandAnalyzerTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\TaskTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\PathPermissionChecker.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\PathUtilsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\PathPermissionCheckerTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Program.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\about-dynadocs.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\InboxItemParser.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\AgentRegistryTests.cs — Modified


## Review Summary

Fixed DispatchedByRole not persisting through AgentRegistry.WriteStateFile. Root cause: AgentRegistry has its own duplicate WriteStateFile (line 1311) and StateFieldParsers (line 1378), both missing dispatched-by-role. The fix was previously applied only to AgentStateStore but not to AgentRegistry. Added dispatched-by-role to both WriteStateFile template and StateFieldParsers in AgentRegistry. Also added from_role to InboxItemParser.StringFieldSetters. 3 new tests in AgentRegistryTests verify persistence and round-trip. 3 new tests in InboxItemParserTests verify from_role parsing. All 2868 existing tests pass.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-19 21:16
- Result: PASSED
- Notes: LGTM. dispatched-by-role correctly added to AgentRegistry WriteStateFile template and StateFieldParsers, mirroring AgentStateStore. from_role correctly wired through InboxItemParser. 6 focused tests cover persistence, round-trip, and null cases. gap_check failures are all pre-existing (0 overlap with changed files). One unrelated SystemManagedEntries change noted but benign.

Awaiting human approval.

## Approval

- Approved: 2026-03-25 17:25
