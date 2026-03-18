---
area: general
name: requires-dispatch-constraint
status: human-reviewed
created: 2026-03-18T15:55:34.0863521Z
assigned: Emma
updated: 2026-03-18T18:06:22.6427281Z
---

# Task: requires-dispatch-constraint

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented requires-dispatch constraint system per plan and decision 012. Replaced hardcoded H25 release check and wait-privilege array with data-driven role constraints. Added CanOrchestrate capability, generalized dispatch markers, updated all affected services/tests. 110 tests pass. One plan deviation: could not delete ReviewDispatchedMarker.cs (guard blocks rm for code-writer) — file is dead code, safe to delete manually.

## Code Review

- Reviewed by: Frank
- Date: 2026-03-18 18:13
- Result: PASSED
- Notes: LGTM. Clean, correct implementation of decision 012. All data-driven constraints work as specified. Tests are comprehensive and meaningful. Template updates align with the new constraint system. Known issue: ReviewDispatchedMarker.cs is dead code (guard prevented code-writer from deleting it) — user should delete manually.

Awaiting human approval.