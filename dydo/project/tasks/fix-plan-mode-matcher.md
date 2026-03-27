---
area: general
name: fix-plan-mode-matcher
status: human-reviewed
created: 2026-03-26T22:29:35.7103024Z
assigned: Brian
updated: 2026-03-26T22:37:58.8674672Z
---

# Task: fix-plan-mode-matcher

Fixed: Added EnterPlanMode|ExitPlanMode to the PreToolUse hook matcher regex in InitCommand.cs (line 340). The guard's plan-mode block (layer 2.6) was already implemented but never triggered because these tools weren't in the matcher. Added test Init_Claude_MatcherIncludesPlanModeTools. Note: .claude/settings.local.json could not be updated directly (outside code-writer permissions) — re-running dydo init claude --join will regenerate it with the fix. No plan deviations.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed: Added EnterPlanMode|ExitPlanMode to the PreToolUse hook matcher regex in InitCommand.cs (line 340). The guard's plan-mode block (layer 2.6) was already implemented but never triggered because these tools weren't in the matcher. Added test Init_Claude_MatcherIncludesPlanModeTools. Note: .claude/settings.local.json could not be updated directly (outside code-writer permissions) — re-running dydo init claude --join will regenerate it with the fix. No plan deviations.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-26 23:12
- Result: PASSED
- Notes: LGTM. Code is clean, tests pass. The one-line matcher fix correctly enables the existing guard layer 2.6 to block plan mode tools. gap_check passes (131/131 modules).

Awaiting human approval.