---
area: general
name: test-additions-92-93-117
status: review-pending
created: 2026-04-27T14:02:45.4552986Z
assigned: Frank
updated: 2026-04-27T14:49:24.0587355Z
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