---
area: general
name: guardrails-h27-entry
status: human-reviewed
created: 2026-03-18T19:42:50.2660568Z
assigned: Dexter
updated: 2026-03-18T19:55:38.1602763Z
---

# Task: guardrails-h27-entry

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

H27 entry added to dydo/reference/guardrails.md in the Access Control section. Blocks EnterPlanMode/ExitPlanMode tools for dydo agents. Also updated the Extensibility section's hard-coded list to include H27. Verify accuracy against Commands/GuardCommand.cs lines 179-193.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-18 20:04
- Result: PASSED
- Notes: LGTM. H27 entry is accurate: trigger, message, section placement, and extensibility reference all verified against GuardCommand.cs:179-193. Two additions, both correct. 3 test failures and 2 CRAP regressions are pre-existing / from other in-flight work, not related to this doc change.

Awaiting human approval.