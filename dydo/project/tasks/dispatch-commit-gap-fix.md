---
area: general
name: dispatch-commit-gap-fix
status: human-reviewed
created: 2026-04-15T17:26:19.5726758Z
assigned: Brian
updated: 2026-04-15T17:33:22.9234205Z
---

# Task: dispatch-commit-gap-fix

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed rev-list --count malformed invocation (removed stray '--'), added real-git integration tests that catch flag-shape bugs, surface non-zero git status exit instead of silently treating as clean.

## Code Review

- Reviewed by: Brian
- Date: 2026-04-15 17:33
- Result: PASSED
- Notes: PASS. Blocker #1 (rev-list '--' bug) fixed in Commands/WorktreeCommand.cs:374. Blocker #2 resolved: DynaDocs.Tests/Commands/WorktreeMergeSafetyIntegrationTests.cs adds 5 tests against a real temp git repo (not mocking RunProcessCapture). Happy-path ExecuteMerge test fails without the rev-list fix (git exit 129), so it's a true regression guard. Minor #3 fixed: non-zero 'git status --porcelain' exit now raises a safety error. Minor #4 intentionally skipped — fine. Build clean, 3706/3706 tests pass, gap_check 135/135 at 100%.

Awaiting human approval.