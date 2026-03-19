---
area: general
type: changelog
date: 2026-03-19
---

# Task: orchestrator-worktree-guidance

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardIntegrationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\InboxItem.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\AgentState.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\RoleConstraint.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\InboxMetadataReader.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentStateStore.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\RoleConstraintEvaluator.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\RoleDefinitionService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\RoleConstraintEvaluatorTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\RoleDefinitionServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\AgentStateStoreTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\LinuxTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MacTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\BashCommandAnalyzer.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\WorktreeDispatchTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\BashCommandAnalyzerTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-orchestrator.template.md — Modified
C:/Users/User/Desktop/Projects/DynaDocs/Templates/mode-orchestrator.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\dydo-commands.template.md — Modified


## Review Summary

Updated Templates/mode-orchestrator.template.md with worktree guidance per decision 011. Four changes: (1) Worktree note in Slice section, (2) --worktree in Dispatch example, (3) When to Use --worktree subsection after Dispatch, (4) Merge Coordination subsection after Monitor. Note: could not edit dydo/_system/templates/ copy due to code-writer role permissions — that needs separate handling.

## Code Review (2026-03-16 17:29)

- Reviewed by: Charlie
- Result: FAILED
- Issues: Default dispatch example (line 82) includes --worktree, contradicting the When to Use subsection below which says not to use it for sequential/single/non-code dispatches. Fix dispatched to code-writer.

Requires rework.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-16 17:36
- Result: PASSED
- Notes: LGTM. Previous issue fixed — default dispatch example no longer includes --worktree. Worktree guidance properly separated into dedicated subsection with correct do/don't criteria per decision 011. Merge coordination section accurate. All 2621 tests pass. Note: dydo/_system/templates/ override copy is stale and needs separate sync.

Awaiting human approval.

## Approval

- Approved: 2026-03-19 13:04
