---
area: backend
type: changelog
date: 2026-03-08
---

# Task: auto-close-fix

Fix auto-close: remove -NoExit for auto-close shells, kill by PID ancestry not name

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented auto-close-fix per plan. (1) GetWindowsArguments now omits -NoExit when autoClose is true so PowerShell exits naturally. (2) TerminalCloser.ScheduleClaudeTermination refactored to kill grandparent PID instead of searching by process name. (3) Linux and macOS paths verified — already correct. (4) Updated existing tests: renamed/inverted NoExit assertion, updated ScheduleClaudeTermination test to verify grandparent PID. Added new NoAutoClose test. All 1593 tests pass. No deviations from plan.

## Code Review

- Reviewed by: Frank
- Date: 2026-03-08 19:39
- Result: PASSED
- Notes: LGTM. Both fixes are clean and correct: (1) -NoExit omission for auto-close is a simple conditional with preserved default behavior; (2) grandparent PID kill is more deterministic than name search, with proper null checks. Tests cover all paths. All 1593 tests pass.

Awaiting human approval.

## Code Review

- Reviewed by: Grace
- Date: 2026-03-08 20:05
- Result: PASSED
- Notes: LGTM. Both fixes are clean and correct: (1) -NoExit conditionally omitted for auto-close with preserved default; (2) grandparent PID kill is deterministic, with proper null checks at both levels. Tests cover all paths. 147 relevant tests pass.

Awaiting human approval.

## Approval

- Approved: 2026-03-08 20:25
