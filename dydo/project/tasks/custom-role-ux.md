---
area: general
name: custom-role-ux
status: review-failed
created: 2026-03-11T14:51:19.7778134Z
assigned: Frank
---

# Task: custom-role-ux

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Slice 4 implemented: (1) dydo roles create <name> - scaffolds minimal role JSON in dydo/_system/roles/, rejects duplicates and base-role name conflicts, runs validation on new file. (2) dydo validate - runs ValidationService.ValidateSystem() checking dydo.json, role files (JSON parse, schema, constraints, path sets, template refs), and agent states. Exit 0 if no errors, 1 if errors. (3) Daily validation in GuardCommand - non-blocking, 24h cooldown via .last-validation timestamp, warns via stderr. 17 new tests all passing. No plan deviations. Note: test-deploy.role.json left in roles dir from manual testing (guard blocked deletion).

## Code Review (2026-03-11 15:16)

- Reviewed by: Dexter
- Result: FAILED
- Issues: 3 issues: (1) BUG: CommandSmokeTests second test missing WatchdogCommand.Create() — same bug category as Slice 1 review failure. (2) SLOP: ValidationService.cs unnecessary 'partial' keyword. (3) SLOP: ValidationService.cs unused 'using System.Text.RegularExpressions' import.

Requires rework.