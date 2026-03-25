---
area: general
type: changelog
date: 2026-03-25
---

# Task: centralized-test-runner

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
C:\Users\User\Desktop\Projects\DynaDocs\Program.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\about-dynadocs.template.md — Modified


## Review Summary

Review: added task compact entry to Program.cs help text (line 116) and Templates/about-dynadocs.template.md (quick reference table). Commit 8fb0722. Note: dydo/reference/about-dynadocs.md still needs the same addition but is outside code-writer permissions.

## Code Review (2026-03-20 19:25)

- Reviewed by: Charlie
- Result: FAILED
- Issues: FAIL: (1) Test failure — AboutQuickReference_IncludesAllCommands fails because dydo/reference/about-dynadocs.md is missing 'dydo task compact'. Template was updated but reference doc was not. (2) gap_check.py exits non-zero — TaskCompactHandler.cs line coverage 75% (needs >=80%), plus two pre-existing CRAP failures. Fix: add '| `dydo task compact` | Compact audit snapshots |' to dydo/reference/about-dynadocs.md Tasks table after the task list row, and add tests to bring TaskCompactHandler.cs to >=80% line coverage.

Requires rework.

## Code Review (2026-03-20 19:52)

- Reviewed by: Brian
- Result: FAILED
- Issues: FAIL: (1) Test failure persists — AboutQuickReference_IncludesAllCommands still fails because dydo/reference/about-dynadocs.md is missing 'dydo task compact'. This was flagged in Charlie's review and remains unfixed. The template has the entry (line 302) but the reference doc does not. Fix: add '| `dydo task compact` | Compact audit snapshots |' to dydo/reference/about-dynadocs.md after the task list row (line 300). (2) TaskCompactHandler.cs coverage now passes T1 — the new test Task_Compact_DirectoryExistsButNoSessions is clean and correct. (3) gap_check exits non-zero but remaining 13 failures are pre-existing, not regressions.

Requires rework.

## Approval

- Approved: 2026-03-25 17:24
