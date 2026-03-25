---
area: general
type: changelog
date: 2026-03-25
---

# Task: fix-worktree-merge-flow

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ProcessUtilsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\WorktreeDispatchTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\FileCoverageService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WatchdogServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-code-writer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-reviewer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-orchestrator.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Utils\PathUtils.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\InitCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TemplateCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorkspaceCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\PathUtilsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\AgentRegistryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCompatTests.cs — Modified


## Review Summary

Updated 3 agent mode templates to document the worktree merge flow. Code-writer template: added Worktree Merge section in Work that tells merge-dispatched agents to run 'dydo worktree merge' instead of git merge, and explains conflict resolution via --finalize. Reviewer template: added Worktree merge dispatch section after review-passes explaining the .needs-merge marker and the required merge dispatch before release. Orchestrator template: added how-merge-works paragraph to Merge Coordination explaining the end-to-end flow. No plan existed — followed the investigation brief. Key decision: docs-only change since the code (WorktreeCommand.cs, ReviewCommand.cs, DispatchService.cs) already implements the full merge flow correctly. NOTE: dydo/reference/dydo-commands.md needs a Worktree Commands section added (dydo worktree merge, cleanup, init-settings) but code-writer role cannot edit dydo/ docs — needs docs-writer dispatch.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-20 12:39
- Result: PASSED
- Notes: LGTM. All 3 template changes accurately document the worktree merge flow as implemented in WorktreeCommand.cs, ReviewCommand.cs, and DispatchService.cs. Every claim verified against source code. Tests pass (3 pre-existing failures unrelated to this change). Coverage gate failures are all pre-existing — docs-only change cannot regress coverage.

Awaiting human approval.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-20 12:56
- Result: PASSED
- Notes: LGTM. All 3 template changes accurately document the worktree merge flow. Every claim verified against WorktreeCommand.cs, ReviewCommand.cs, and DispatchService.cs. Tests pass (2 pre-existing failures unrelated). Coverage gate failures all pre-existing — docs-only change cannot regress coverage.

Awaiting human approval.

## Approval

- Approved: 2026-03-25 17:25
