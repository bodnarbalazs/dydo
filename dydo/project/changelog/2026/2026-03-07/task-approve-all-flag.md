---
area: general
type: changelog
date: 2026-03-07
---

# Task: task-approve-all-flag

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Added --all / -a flag to dydo task approve as cross-platform alternative to wildcard *. Made name argument optional when --all is set, kept * for backward compat. Updated docs (templates + reference). 4 new tests, all 1282 tests pass. Skipped shell-expansion hint (plan item 2) — deemed unnecessary since --all is now the primary documented approach.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-06 23:19
- Result: PASSED
- Notes: LGTM. --all/-a flag is clean, backward compat with * preserved, filename sanitization is good cross-cutting hardening. 4 integration tests + 7 unit tests, all pass. Docs updated correctly.

Awaiting human approval.

## Approval

- Approved: 2026-03-07 15:00
