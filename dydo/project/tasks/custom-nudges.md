---
area: general
name: custom-nudges
status: human-reviewed
created: 2026-03-23T20:58:23.1060469Z
assigned: Brian
updated: 2026-03-25T14:32:21.4369635Z
---

# Task: custom-nudges

Implemented custom nudges in dydo.json. Added NudgeConfig model, nudge matching in guard (block/warn severities with marker files), migrated 5 hard-coded indirect dydo invocation checks to built-in nudges, added regex validation in dydo validate, marker cleanup on release. All 3179 tests pass, coverage gate clear. No plan deviations except making shell/python nudge patterns use capture groups for invoker names in messages.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

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