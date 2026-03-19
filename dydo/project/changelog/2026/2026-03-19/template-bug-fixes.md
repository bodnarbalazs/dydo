---
area: general
type: changelog
date: 2026-03-19
---

# Task: template-bug-fixes

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-inquisitor.template.md — Created
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-judge.template.md — Created
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-test-writer.template.md — Created
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-orchestrator.template.md — Created
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-reviewer.template.md — Created
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-planner.template.md — Created
C:\Users\User\Desktop\Projects\DynaDocs\Models\ReviewDispatchedMarker.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-code-writer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-docs-writer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-co-thinker.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Serialization\DydoJsonContext.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MarkerStore.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WorkspaceCleaner.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\MarkerStoreTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchWaitIntegrationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\InboxService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TaskApproveHandler.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified


## Review Summary

Fixed dispatch flags and legacy references in 4 mode templates. Changes: (1) mode-code-writer: --wait→--no-wait, removed legacy --feature/--quick flag references per decision 006. (2) mode-docs-writer: --wait→--no-wait. (3) mode-reviewer: --wait→--no-wait. (4) mode-planner: removed Option C which told planners to use --wait (reserved for oversight roles). All 2564 tests pass.

## Code Review

- Reviewed by: Dexter
- Date: 2026-03-16 15:32
- Result: PASSED
- Notes: LGTM. All 4 template changes are correct and surgical. --wait correctly removed from non-oversight roles (code-writer, docs-writer, reviewer, planner). Legacy --feature/--quick flags fully cleaned per decision 006. Planner Option C removal is clean. 2564/2564 tests pass. No remaining inconsistencies.

Awaiting human approval.

## Approval

- Approved: 2026-03-19 13:04
