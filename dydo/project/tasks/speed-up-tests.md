---
area: general
name: speed-up-tests
status: human-reviewed
created: 2026-03-27T17:44:03.7941640Z
assigned: Brian
updated: 2026-03-27T19:27:49.3584577Z
---

# Task: speed-up-tests

Created DynaDocs.Tests/coverage/run_tests.py: worktree-isolated test runner. Updated gap_check.py to delegate test execution to it. Added retry loop for Windows worktree cleanup. Updated template additions (extra-verify.md, extra-review-steps.md) to guide agents toward run_tests.py. Added warn nudge in dydo.json for bare dotnet test commands. No plan existed — task emerged from co-thinker session with the user.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Created DynaDocs.Tests/coverage/run_tests.py: worktree-isolated test runner. Updated gap_check.py to delegate test execution to it. Added retry loop for Windows worktree cleanup. Updated template additions (extra-verify.md, extra-review-steps.md) to guide agents toward run_tests.py. Added warn nudge in dydo.json for bare dotnet test commands. No plan existed — task emerged from co-thinker session with the user.

## Code Review (2026-03-27 19:15)

- Reviewed by: Dexter
- Result: FAILED
- Issues: FAIL: _run wrapper in run_tests.py:27-28 is unnecessary slop — wraps subprocess.run with zero behavior. Delete it, call subprocess.run directly. Note: create_worktree sys.exit(1) bypasses gap_check graceful degradation; consider returning None.

Requires rework.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-27 19:35
- Result: PASSED
- Notes: LGTM. Both review issues fixed: _run wrapper deleted, subprocess.run inlined at 3 call sites. create_worktree returns None on failure, run_tests handles gracefully. 3291 tests pass, gap_check 131/131 clean.

Awaiting human approval.