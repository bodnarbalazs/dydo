---
area: general
name: t1-slice-a
status: human-reviewed
created: 2026-03-14T15:20:27.1188390Z
assigned: Charlie
updated: 2026-03-14T17:52:33.1663455Z
---

# Task: t1-slice-a

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Raised T1 coverage for ProcessUtils.CommandLine.cs (31.5%→81.0% line, 27.3%→71.4% branch) and ProcessUtils.Ancestry.cs (52.7%→92.3% line, 32.1%→67.9% branch). Extracted parsing logic (ParseNewlineSeparatedPids, ParsePsEoPidArgs, ParseProcStatusForPpid, ParsePsPpidOutput) from platform-specific methods into testable internal methods. Added RunProcess shared helper to consolidate process-launching boilerplate (5 occurrences → Rule of Three). Made platform-specific methods internal for error-path testing on Windows. 60 tests, all green.

## Code Review

- Reviewed by: Grace
- Date: 2026-03-14 17:57
- Result: PASSED
- Notes: Clean refactoring. RunProcess consolidates 5 boilerplate instances. Four parsing extractions preserve behavior exactly. 33 new tests with thorough edge-case coverage. No bugs, no security issues, no unnecessary complexity.

Awaiting human approval.