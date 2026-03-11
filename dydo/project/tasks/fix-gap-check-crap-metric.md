---
area: general
name: fix-gap-check-crap-metric
status: human-reviewed
created: 2026-03-11T19:46:20.2671611Z
assigned: Rose
---

# Task: fix-gap-check-crap-metric

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed DynaDocs gap_check.py CRAP metric to use per-method max CC instead of class-level sum. Single change in parse_cobertura_xml() at line ~249: now reads methods elements for per-method complexity and takes the max, falling back to class-level attribute only if no methods element exists. Verified via --skip-tests run — GuardCommand CC dropped from 341 to 52. LC fix and decision doc could not be completed due to guard permissions — Brian has been notified.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-11 19:54
- Result: PASSED
- Notes: LGTM. Correct fix: CRAP is a per-method metric, using max per-method CC instead of class-level sum is the right approach. Edge cases handled (empty methods, missing methods element). Clean, minimal change. Tests pass (2 pre-existing unrelated failures in FixCommandIntegrationTests).

Awaiting human approval.