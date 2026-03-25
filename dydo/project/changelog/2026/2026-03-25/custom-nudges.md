---
area: general
type: changelog
date: 2026-03-25
---

# Task: custom-nudges

Implemented custom nudges in dydo.json. Added NudgeConfig model, nudge matching in guard (block/warn severities with marker files), migrated 5 hard-coded indirect dydo invocation checks to built-in nudges, added regex validation in dydo validate, marker cleanup on release. All 3179 tests pass, coverage gate clear. No plan deviations except making shell/python nudge patterns use capture groups for invoker names in messages.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Models\NudgeConfig.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\ConfigFactory.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigFactoryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TemplateCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\GuardCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\DydoConfig.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Serialization\DydoJsonContext.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\ValidationService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCompatTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ValidationServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigServiceTests.cs — Modified


## Review Summary

Implemented custom nudges in dydo.json. Added NudgeConfig model, nudge matching in guard (block/warn severities with marker files), migrated 5 hard-coded indirect dydo invocation checks to built-in nudges, added regex validation in dydo validate, marker cleanup on release. All 3179 tests pass, coverage gate clear. No plan deviations except making shell/python nudge patterns use capture groups for invoker names in messages.

## Code Review (2026-03-24 23:56)

- Reviewed by: Dexter
- Result: FAILED
- Issues: Warn-then-allow marker flow in CheckNudges has zero tests. Need tests verifying: (1) warn nudge blocks first encounter and creates .nudge-<hash> marker, (2) warn nudge allows second encounter and deletes marker, (3) markers are per-pattern. Everything else is clean — model, migration, validation, serialization, release cleanup all good.

Requires rework.

## Code Review

- Reviewed by: Dexter
- Date: 2026-03-25 14:54
- Result: PASSED
- Notes: LGTM. Previous concern fully addressed: 3 tests for warn-then-allow marker flow added. All 3182 tests pass, coverage gate 129/129. Code is clean — unified nudge system is simpler than the 5 GeneratedRegex methods it replaced.

Awaiting human approval.

## Approval

- Approved: 2026-03-25 17:24
