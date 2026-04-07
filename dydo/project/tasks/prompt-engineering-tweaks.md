---
area: general
name: prompt-engineering-tweaks
status: human-reviewed
created: 2026-04-07T19:17:32.5190986Z
assigned: Charlie
updated: 2026-04-07T20:30:48.9624706Z
---

# Task: prompt-engineering-tweaks

Review two template changes: (1) Templates/mode-judge.template.md — added structured Ruling Format subsection in step 4 requiring files-examined, independent-verification, and alternative-explanations fields. Updated step 5 examples to use the new format. (2) Templates/mode-orchestrator.template.md — added one bullet in Monitor section prompting issue resolution proposal to the user. No code changes, templates only.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review two template changes: (1) Templates/mode-judge.template.md — added structured Ruling Format subsection in step 4 requiring files-examined, independent-verification, and alternative-explanations fields. Updated step 5 examples to use the new format. (2) Templates/mode-orchestrator.template.md — added one bullet in Monitor section prompting issue resolution proposal to the user. No code changes, templates only.

## Code Review

- Reviewed by: Iris
- Date: 2026-04-07 21:19
- Result: PASSED
- Notes: LGTM. Both template changes are clean and purposeful. Judge ruling format prevents rubber-stamping with required independent-verification and alternative-explanations fields. Orchestrator issue-resolution bullet closes the lifecycle gap. All 3483 tests pass, gap_check green.

Awaiting human approval.