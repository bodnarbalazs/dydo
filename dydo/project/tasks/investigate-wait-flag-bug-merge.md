---
area: general
name: investigate-wait-flag-bug-merge
status: human-reviewed
created: 2026-04-08T15:56:06.4583733Z
assigned: Brian
updated: 2026-04-08T16:13:55.5882890Z
---

# Task: investigate-wait-flag-bug-merge

Merged worktree/inquisition-template-system into master (branch was already merged, finalized cleanup). Fixed pre-existing test failure in WhoamiConcurrencyTests caused by DYDO_AGENT env var leaking into test runner — stripped DYDO_ env vars in run_tests.py subprocess environment. All 3511 tests pass, gap_check green.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Merged worktree/inquisition-template-system into master (branch was already merged, finalized cleanup). Fixed pre-existing test failure in WhoamiConcurrencyTests caused by DYDO_AGENT env var leaking into test runner — stripped DYDO_ env vars in run_tests.py subprocess environment. All 3511 tests pass, gap_check green.

## Code Review

- Reviewed by: Frank
- Date: 2026-04-08 16:18
- Result: PASSED
- Notes: LGTM. Clean env var stripping fix — 3 lines, correct dict comprehension, good comment. All 3511 tests pass, gap_check green (135/135 modules).

Awaiting human approval.