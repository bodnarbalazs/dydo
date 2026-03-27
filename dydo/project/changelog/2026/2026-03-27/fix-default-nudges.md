---
area: general
type: changelog
date: 2026-03-27
---

# Task: fix-default-nudges

Moved 5 indirect dydo invocation nudges (npx, dotnet, dotnet run, shell, python) from hard-coded BuiltInNudges in GuardCommand.cs to soft-coded DefaultNudges in ConfigFactory.cs. Removed the project-specific coverage tool nudge from defaults. Simplified CheckNudges to only use config nudges (no more BuiltInNudges list). Updated all tests to reference ConfigFactory.DefaultNudges. No plan deviations — clean removal of BuiltInNudges with no double-firing concern since they now flow through dydo.json.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\coverage\gap_check.py — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WorkspaceArchiver.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchWaitIntegrationTests.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/Commands/GuardCommand.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Integration/GuardIntegrationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\ConfigFactory.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\GuardCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCompatTests.cs — Modified


## Review Summary

Moved 5 indirect dydo invocation nudges (npx, dotnet, dotnet run, shell, python) from hard-coded BuiltInNudges in GuardCommand.cs to soft-coded DefaultNudges in ConfigFactory.cs. Removed the project-specific coverage tool nudge from defaults. Simplified CheckNudges to only use config nudges (no more BuiltInNudges list). Updated all tests to reference ConfigFactory.DefaultNudges. No plan deviations — clean removal of BuiltInNudges with no double-firing concern since they now flow through dydo.json.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-25 22:10
- Result: PASSED
- Notes: LGTM. Clean refactor: BuiltInNudges removed from GuardCommand, nudges now fully config-driven via ConfigFactory.DefaultNudges. CheckNudges simplified. Coverage tool nudge correctly removed from defaults. All 3193 tests pass, gap_check clean.

Awaiting human approval.

## Approval

- Approved: 2026-03-27 13:14
