---
area: general
name: guard-python-invocation
status: human-reviewed
created: 2026-03-16T18:52:58.7282592Z
assigned: Dexter
updated: 2026-03-16T18:56:29.4039988Z
---

# Task: guard-python-invocation

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Added python/python3/py to the indirect dydo invocation guard. New regex catches python-based attempts to run dydo and redirects agents to use dydo directly. All 59 indirect dydo tests pass including new python cases and false-positive safety for legitimate python commands.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-16 18:59
- Result: PASSED
- Notes: LGTM. Regex mirrors established shell pattern, all 59 tests pass, false-positive safety is solid. Clean, minimal change.

Awaiting human approval.