---
area: general
name: fix-autoclose-worktree-hang
status: human-reviewed
created: 2026-04-03T14:24:29.3718915Z
assigned: Charlie
---

# Task: fix-autoclose-worktree-hang

Implemented three fixes for auto-close worktree hang: (1) Added 30s timeout to WorktreeCommand.RunProcess and RunProcessWithExitCode — kills hung processes instead of blocking forever. (2) WatchdogService.PollAndCleanup now only clears auto-close when a non-shell process was killed or no processes remain — previously it cleared the flag when only shell processes (pwsh) were found, preventing retry. (3) WindowsTerminalLauncher setup phase now uses 'cmd /c rmdir' for junction removal instead of [IO.Directory]::Delete which follows junctions and can corrupt target contents. All three fixes have tests. Full suite passes (3431/3432, 1 pre-existing failure in ReadmeClones_ContentInSync). gap_check has a pre-existing build failure (TryEnqueue removed from QueueService but tests still reference it).

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented three fixes for auto-close worktree hang: (1) Added 30s timeout to WorktreeCommand.RunProcess and RunProcessWithExitCode — kills hung processes instead of blocking forever. (2) WatchdogService.PollAndCleanup now only clears auto-close when a non-shell process was killed or no processes remain — previously it cleared the flag when only shell processes (pwsh) were found, preventing retry. (3) WindowsTerminalLauncher setup phase now uses 'cmd /c rmdir' for junction removal instead of [IO.Directory]::Delete which follows junctions and can corrupt target contents. All three fixes have tests. Full suite passes (3431/3432, 1 pre-existing failure in ReadmeClones_ContentInSync). gap_check has a pre-existing build failure (TryEnqueue removed from QueueService but tests still reference it).

## Code Review (2026-04-03 14:58)

- Reviewed by: Jack
- Result: FAILED
- Issues: Junction rmdir regression: 'cmd /c rmdir' (without /s /q) fails on non-empty real directories. dydo/_system/roles/ (9 tracked files) and dydo/project/issues/ (6 tracked files) exist as real directories in fresh worktrees — rmdir will fail, leaving them as real dirs instead of junctions. Use 'cmd /c rmdir /s /q' in all 8 occurrences in WindowsTerminalLauncher.cs. On modern Windows, rmdir /s /q on a junction removes the junction point without following it, so it is safe for both junctions and real directories. Minor: timeout tests only exercise the override mechanism, not actual timeouts. gap_check failures (DispatchService CRAP 30.5, FileReadRetry 75%) are pre-existing.

Requires rework.

## Code Review

- Reviewed by: Grace
- Date: 2026-04-03 15:22
- Result: PASSED
- Notes: LGTM. All 8 rmdir occurrences correctly updated with /s /q flags. Tests cover both code paths. gap_check failure (DispatchService CRAP 30.2) is pre-existing and unrelated.

Awaiting human approval.