---
area: general
type: changelog
date: 2026-04-02
---

# Task: implement-clean-rename

Implemented decision 014: renamed dydo clean to dydo agent clean. Moved command registration from root to AgentCommand in Program.cs. Updated CompletionProvider (refactored to data-driven tables, reducing CC from 38 to ~19). Updated help text, templates, tests, and generated reference docs. All 3364 tests pass (1 flaky watchdog test unrelated). Coverage gate passes (132/132 modules). No alias or deprecation per decision.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Templates\about-dynadocs.template.md — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\RoleDefinitionService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CommandDocConsistencyTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Program.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\AgentCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\CompletionProvider.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\dydo-commands.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CompleteCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\HelpCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\EndToEnd\CliEndToEndTests.cs — Modified


## Review Summary

Implemented decision 014: renamed dydo clean to dydo agent clean. Moved command registration from root to AgentCommand in Program.cs. Updated CompletionProvider (refactored to data-driven tables, reducing CC from 38 to ~19). Updated help text, templates, tests, and generated reference docs. All 3364 tests pass (1 flaky watchdog test unrelated). Coverage gate passes (132/132 modules). No alias or deprecation per decision.

## Code Review

- Reviewed by: Grace
- Date: 2026-03-31 14:31
- Result: PASSED
- Notes: LGTM. Command correctly moved from root to agent subcommand group. CompletionProvider refactor to data-driven tables is clean and reduces CC from ~38 to ~19. Tests comprehensive — 8+ new edge-case tests added. No dead code, no backwards-compat hacks, exactly per decision 014. All 3364 tests pass, coverage gate 132/132.

Awaiting human approval.

## Approval

- Approved: 2026-04-02 18:56
