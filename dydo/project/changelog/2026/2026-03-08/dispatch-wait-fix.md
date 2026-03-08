---
area: backend
type: changelog
date: 2026-03-08
---

# Task: dispatch-wait-fix

Make dispatch --wait non-blocking, enforce dydo wait runs in background

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented dispatch-wait-fix per plan. Removed poll loop from DispatchCommand, added RunInBackground to ToolInputData, added guard enforcement for dydo wait in foreground, updated templates, wrote tests. All 1590 tests pass. No deviations.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-08 19:28
- Result: PASSED
- Notes: LGTM. Poll loop cleanly removed, guard enforcement correct, templates updated, tests thorough. All 1592 tests pass.

Awaiting human approval.

## Code Review

- Reviewed by: Grace
- Date: 2026-03-08 19:45
- Result: PASSED
- Notes: LGTM. Poll loop cleanly removed, guard enforcement correct, role nudge guardrail well-designed, terminal fixes sound, templates updated. All 1593 tests pass. No issues found.

Awaiting human approval.

## Approval

- Approved: 2026-03-08 20:25
