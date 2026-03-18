---
area: general
name: inquisition-file-coverage
status: human-reviewed
created: 2026-03-17T22:07:31.7584427Z
assigned: Emma
updated: 2026-03-18T15:03:09.3717255Z
---

# Task: inquisition-file-coverage

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented file-level inquisition coverage heatmap. Added --files, --path, --gaps-only, --since, --summary options to dydo inquisition coverage. Created FileCoverageService with session grouping, per-file scoring (cap 3 per inquisition group), git-based decay, and Markdown rendering. Output auto-routes to agent workspace or project path. 20 unit tests + 11 integration tests, all passing. Coverage gap check passes. No plan deviations.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-18 15:08
- Result: PASSED
- Notes: LGTM. Both fixes correct: (1) Dispose re-implements IDisposable on derived class with new+base.Dispose(), prevents test pollution. (2) StaleCount now accurately counts only decay-affected files. 31/31 tests pass, 126/126 coverage modules pass. Code is clean, no slop.

Awaiting human approval.