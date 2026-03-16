---
area: general
name: template-bug-fixes
status: human-reviewed
created: 2026-03-16T14:18:39.3689618Z
assigned: Charlie
updated: 2026-03-16T15:29:08.4497852Z
---

# Task: template-bug-fixes

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed dispatch flags and legacy references in 4 mode templates. Changes: (1) mode-code-writer: --wait→--no-wait, removed legacy --feature/--quick flag references per decision 006. (2) mode-docs-writer: --wait→--no-wait. (3) mode-reviewer: --wait→--no-wait. (4) mode-planner: removed Option C which told planners to use --wait (reserved for oversight roles). All 2564 tests pass.

## Code Review

- Reviewed by: Dexter
- Date: 2026-03-16 15:32
- Result: PASSED
- Notes: LGTM. All 4 template changes are correct and surgical. --wait correctly removed from non-oversight roles (code-writer, docs-writer, reviewer, planner). Legacy --feature/--quick flags fully cleaned per decision 006. Planner Option C removal is clean. 2564/2564 tests pass. No remaining inconsistencies.

Awaiting human approval.