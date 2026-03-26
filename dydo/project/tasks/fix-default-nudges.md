---
area: general
name: fix-default-nudges
status: human-reviewed
created: 2026-03-25T21:02:51.4497624Z
assigned: Emma
updated: 2026-03-25T22:05:16.6440611Z
---

# Task: fix-default-nudges

Moved 5 indirect dydo invocation nudges (npx, dotnet, dotnet run, shell, python) from hard-coded BuiltInNudges in GuardCommand.cs to soft-coded DefaultNudges in ConfigFactory.cs. Removed the project-specific coverage tool nudge from defaults. Simplified CheckNudges to only use config nudges (no more BuiltInNudges list). Updated all tests to reference ConfigFactory.DefaultNudges. No plan deviations — clean removal of BuiltInNudges with no double-firing concern since they now flow through dydo.json.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Moved 5 indirect dydo invocation nudges (npx, dotnet, dotnet run, shell, python) from hard-coded BuiltInNudges in GuardCommand.cs to soft-coded DefaultNudges in ConfigFactory.cs. Removed the project-specific coverage tool nudge from defaults. Simplified CheckNudges to only use config nudges (no more BuiltInNudges list). Updated all tests to reference ConfigFactory.DefaultNudges. No plan deviations — clean removal of BuiltInNudges with no double-firing concern since they now flow through dydo.json.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-25 22:10
- Result: PASSED
- Notes: LGTM. Clean refactor: BuiltInNudges removed from GuardCommand, nudges now fully config-driven via ConfigFactory.DefaultNudges. CheckNudges simplified. Coverage tool nudge correctly removed from defaults. All 3193 tests pass, gap_check clean.

Awaiting human approval.