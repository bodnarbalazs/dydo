---
area: general
type: changelog
date: 2026-03-25
---

# Task: template-and-enforcement

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


## Review Summary

Implemented conditional must-read enforcement (Decision 013), brief injection into task files, two new framework-owned guide templates, and template updates. 13 files changed. 37 new tests (20 unit + 17 integration), all passing. No regressions in existing 153 guard tests, 77 dispatch tests, or 188 template tests. Pre-existing TerminalLauncher test failures (38) unrelated to changes.

## Code Review

- Reviewed by: Frank
- Date: 2026-03-23 16:07
- Result: PASSED
- Notes: LGTM. All code follows coding standards, logic is correct with proper edge case handling, 37 new tests are comprehensive and meaningful, no security vulnerabilities, no unnecessary complexity, changes match Decision 013 requirements exactly. Worktree creation refactor is a clear architectural win. gap_check has 13 pre-existing failures in unrelated modules — zero regressions from this changeset.

Awaiting human approval.

## Approval

- Approved: 2026-03-25 17:25
