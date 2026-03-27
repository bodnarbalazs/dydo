---
area: general
type: changelog
date: 2026-03-27
---

# Task: gap-check-skip-optimization

Update 4 doc files to reflect gap_check.py flag changes: --skip-tests and --force-stale removed, --force-run added, auto-skip is now default behavior. Files: (1) dydo/_system/template-additions/extra-verify.md line 7 — remove --skip-tests guidance, note auto-skip. (2) dydo/_system/template-additions/extra-review-steps.md line 7 — same. (3) dydo/guides/testing-strategy.md line 106 — replace --skip-tests example with --force-run. (4) dydo/reference/coverage-tools.md lines 18-24 — rewrite flags section: remove --skip-tests/--force-stale, add --force-run, document auto-skip. See plan at agents/Emma/plan-gap-check-skip-optimization.md.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\coverage\gap_check.py — Modified


## Review Summary

Implemented auto-skip optimization for gap_check.py. Changes: (1) Extracted _find_changed_files_since() helper from check_coverage_staleness(). (2) Rewrote check_coverage_staleness() to return (is_fresh, message) tuple. (3) Replaced --skip-tests and --force-stale flags with --force-run. (4) Rewrote main flow: auto-skips tests when no source/test changes detected, runs them otherwise. (5) Updated docstring. (6) Dexter updated 4 doc files (extra-verify.md, extra-review-steps.md, testing-strategy.md, coverage-tools.md). No plan deviations. See plan at agents/Emma/plan-gap-check-skip-optimization.md.

## Code Review

- Reviewed by: Dexter
- Date: 2026-03-27 13:11
- Result: PASSED
- Notes: LGTM. Auto-skip logic is clean and correct. Helper extraction improves readability. All 4 doc files accurate. gap_check exits 0, 131/131 modules pass.

Awaiting human approval.

## Approval

- Approved: 2026-03-27 13:14
