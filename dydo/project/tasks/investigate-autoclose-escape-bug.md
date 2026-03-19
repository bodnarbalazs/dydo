---
area: general
name: investigate-autoclose-escape-bug
status: human-reviewed
created: 2026-03-19T18:52:14.0808859Z
assigned: Charlie
---

# Task: investigate-autoclose-escape-bug

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented window close via wt API and terminal reset ESC sequence for all platforms

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-19 18:56
- Result: PASSED
- Notes: LGTM. Both fixes (window close via wt API + terminal reset ESC sequence) are clean, focused, and well-tested. No coverage regressions. 330 tests pass.

Awaiting human approval.