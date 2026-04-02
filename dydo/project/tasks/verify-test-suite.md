---
area: general
name: verify-test-suite
status: review-failed
created: 2026-04-02T19:48:03.0221224Z
assigned: Brian
---

# Task: verify-test-suite

Fixed flaky test: Merge_Finalize_DecodesWorktreeIdFromBranchSuffix was failing due to StringBuilder corruption from concurrent xUnit parallel test classes writing to globally-redirected Console.Out. Fix: wrapped StringWriter with TextWriter.Synchronized() in CaptureAll helper. All 3406 tests pass, gap_check 132/132 modules at 100%.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed flaky test: Merge_Finalize_DecodesWorktreeIdFromBranchSuffix was failing due to StringBuilder corruption from concurrent xUnit parallel test classes writing to globally-redirected Console.Out. Fix: wrapped StringWriter with TextWriter.Synchronized() in CaptureAll helper. All 3406 tests pass, gap_check 132/132 modules at 100%.

## Code Review (2026-04-02 20:08)

- Reviewed by: Charlie
- Result: FAILED
- Issues: CaptureStdout (WorktreeCommandTests.cs:1870) uses Console.SetOut(sw) without TextWriter.Synchronized(). Same root cause (StringBuilder corruption from parallel xUnit) applies. Fix both helpers consistently.

Requires rework.