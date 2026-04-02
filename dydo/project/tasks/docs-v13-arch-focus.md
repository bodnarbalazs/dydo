---
area: general
name: docs-v13-arch-focus
status: human-reviewed
created: 2026-04-01T09:45:39.0593287Z
assigned: Grace
updated: 2026-04-01T09:47:43.4369881Z
---

# Task: docs-v13-arch-focus

Architecture.md refactored: stripped feature descriptions from Issue Tracker, Inquisition Coverage, Custom Nudges, and Conditional Must-Reads sections down to architectural mechanisms only. Kept Worktree Dispatch, Dispatch Queue, and Watchdog sections as-is. Added links to reference pages for stripped content. Original architectural sections (Guard, Role, Dispatch, Audit) untouched.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Architecture.md refactored: stripped feature descriptions from Issue Tracker, Inquisition Coverage, Custom Nudges, and Conditional Must-Reads sections down to architectural mechanisms only. Kept Worktree Dispatch, Dispatch Queue, and Watchdog sections as-is. Added links to reference pages for stripped content. Original architectural sections (Guard, Role, Dispatch, Audit) untouched.

## Code Review

- Reviewed by: Iris
- Date: 2026-04-01 09:52
- Result: PASSED
- Notes: LGTM. Architecture.md correctly refactored to mechanism-level descriptions. All 4 stripped sections (Custom Nudges, Conditional Must-Reads, Issue Tracker, Inquisition Coverage) now describe architectural mechanisms without feature details. All cross-reference links verified. 3365 tests pass, gap_check clean.

Awaiting human approval.