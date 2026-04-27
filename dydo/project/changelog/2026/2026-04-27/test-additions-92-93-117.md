---
area: general
type: changelog
date: 2026-04-27
---

# Task: test-additions-92-93-117

Review test additions for #0092 / #0093 / #0117. See git log master..HEAD (commits 7976dc5, 649b95f, 980104a).

#0092: 6 direct unit tests for WorktreeCommand.DeleteDirectoryJunctionSafe — missing-path no-op, empty/regular/nested deletion, and the core junction scenarios (top-level + deeply nested) verifying the function unlinks reparse points without following them. Cross-platform via mklink /J on Windows and ln -s elsewhere.

#0093: Two Theory tests for TerminalLauncher.ValidateWorktreeId — backslash branch (worktree\evil, C:\foo) and path-traversal branch (., .., a/.., a/../b, a/./b). Each asserts on the discriminating substring of the error message.

#0117: StartDummyProcess in WatchdogServiceTests now uses sleep on Linux/macOS (kept ping on Windows where it's reliable). Plus Stop_ReturnsTrue_WhenProcessIsRunning_TightSuccession runs the assertion 10x to catch any future regression deterministically. Verified locally with 3 consecutive WatchdogServiceTests runs (54/54 each).

Test count: 3823 → 3840 (14 mine + 3 Henry's #0118 refactor that landed on top).

gap_check: 136/136 tier-pass, exit 0.

Known suite flake: DynaDocs.Tests.Utils.FileReadRetryTests.Read_ExclusivelyLockedFile_RetriesAndSucceeds is intermittent under suite load (timing race between the test's Thread.Sleep(80) lock release and FileReadRetry's 50/150 ms retry backoff). Passes in isolation. Brian acknowledged and is filing it as a separate issue — out of scope for this task.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review test additions for #0092 / #0093 / #0117. See git log master..HEAD (commits 7976dc5, 649b95f, 980104a).

#0092: 6 direct unit tests for WorktreeCommand.DeleteDirectoryJunctionSafe — missing-path no-op, empty/regular/nested deletion, and the core junction scenarios (top-level + deeply nested) verifying the function unlinks reparse points without following them. Cross-platform via mklink /J on Windows and ln -s elsewhere.

#0093: Two Theory tests for TerminalLauncher.ValidateWorktreeId — backslash branch (worktree\evil, C:\foo) and path-traversal branch (., .., a/.., a/../b, a/./b). Each asserts on the discriminating substring of the error message.

#0117: StartDummyProcess in WatchdogServiceTests now uses sleep on Linux/macOS (kept ping on Windows where it's reliable). Plus Stop_ReturnsTrue_WhenProcessIsRunning_TightSuccession runs the assertion 10x to catch any future regression deterministically. Verified locally with 3 consecutive WatchdogServiceTests runs (54/54 each).

Test count: 3823 → 3840 (14 mine + 3 Henry's #0118 refactor that landed on top).

gap_check: 136/136 tier-pass, exit 0.

Known suite flake: DynaDocs.Tests.Utils.FileReadRetryTests.Read_ExclusivelyLockedFile_RetriesAndSucceeds is intermittent under suite load (timing race between the test's Thread.Sleep(80) lock release and FileReadRetry's 50/150 ms retry backoff). Passes in isolation. Brian acknowledged and is filing it as a separate issue — out of scope for this task.

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-27 15:02
- Result: PASSED
- Notes: PASS. Tests for #0092/#0093/#0117 are well-targeted and meaningful. #0092: 6 tests cover the contract (missing/empty/files/nested-recursive + junction at top-level and depth-2 verifying targets are preserved); cross-platform via CreateJunctionOrSymlink (mklink /J / ln -s). #0093: 2 Theory tests close the backslash + path-traversal branches under _RejectsUnsafeCharacters, asserting on discriminating error substrings. #0117: platform-split spawn (sleep 30 on POSIX, ping retained on Windows) + 10-iteration TightSuccession regression guard. Comments explain *why*, code matches existing test style. Suite: 3853/3853 pass on rerun. gap_check exit 0, 136/136 tier-pass. Note: first suite run hit an unrelated flake — WatchdogServiceTests.EnsureRunning_SpawnedFromWorktree_SetsWorkingDirectoryToMainProjectRoot failed at Dispose with file-in-use on wt-abc; passed clean on rerun. Different from the #0119 FileReadRetry flake.

Awaiting human approval.

## Approval

- Approved: 2026-04-27 15:31
