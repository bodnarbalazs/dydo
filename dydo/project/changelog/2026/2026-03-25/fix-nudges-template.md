---
area: general
type: changelog
date: 2026-03-25
---

# Task: fix-nudges-template

Added default nudges to ConfigFactory (coverage tool warn nudge). CreateDefault now includes them, and template update merges missing defaults via EnsureDefaultNudges. 4 new tests cover: CreateDefault includes nudges, EnsureDefaultNudges adds to empty list, skips duplicates, preserves custom nudges. All 3186 tests pass, coverage gate 129/129. No plan deviations.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Services\ConfigFactory.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigFactoryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TemplateCommand.cs — Modified


## Review Summary

Added default nudges to ConfigFactory (coverage tool warn nudge). CreateDefault now includes them, and template update merges missing defaults via EnsureDefaultNudges. 4 new tests cover: CreateDefault includes nudges, EnsureDefaultNudges adds to empty list, skips duplicates, preserves custom nudges. All 3186 tests pass, coverage gate 129/129. No plan deviations.

## Code Review (2026-03-25 17:00)

- Reviewed by: Dexter
- Result: FAILED
- Issues: Shallow copy bug in CreateDefault line 38: new List<NudgeConfig>(DefaultNudges) shares mutable NudgeConfig references with the static DefaultNudges. If any code mutates a NudgeConfig property on the returned config, it corrupts the defaults. EnsureDefaultNudges correctly deep-copies (new NudgeConfig {...}), but CreateDefault does not. Fix: deep-copy in CreateDefault to match EnsureDefaultNudges.

Requires rework.

## Code Review

- Reviewed by: Dexter
- Date: 2026-03-25 17:16
- Result: PASSED
- Notes: LGTM. Deep copy in CreateDefault now matches EnsureDefaultNudges pattern. All 3 NudgeConfig properties copied. Test verifies mutation isolation. 3187 tests pass, coverage gate 129/129.

Awaiting human approval.

## Approval

- Approved: 2026-03-25 17:25
