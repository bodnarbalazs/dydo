---
area: general
type: changelog
date: 2026-05-08
---

# Task: ship-pre-tag-final-fixes

Review commit 3c34dd2 for v1.4.7 pre-tag. Two small fixes — review independently:

FIX 1 (issue 0179) — worktree cleanup log honesty
- Commands/WorktreeCommand.cs:312 add RemoveZombieDirectoryOverride test seam
- Commands/WorktreeCommand.cs:746 RemoveZombieDirectory now returns bool (true if dir is gone, false on Windows file-lock — WARNING preserved on stderr)
- Commands/WorktreeCommand.cs:737-754 TeardownWorktree returns !Directory.Exists(worktreePath) — disk truth, not derived from inner bool, so RemoveGitWorktree's --force success after RemoveZombieDirectory failure also reports correctly
- Commands/WorktreeCommand.cs:331-339 ExecuteCleanup branches: success → 'cleaned up'; failure → 'marker removed; directory remains (in use by another process — see warning above). Will retry on next prune.'
- Commands/WorktreeCommand.cs:980-994 FinalizeMerge branches identically when refsRemaining==0 and branchDeleteExit==0
- Commands/WorktreeCommand.cs:1018-1024 ExecutePrune does NOT increment orphansRemoved on failure, prints '  Worktree X: directory remains ...' line

FIX 2 (issue 0177) — warn-nudge for open-ended bash polls
- Services/ConfigFactory.cs:78-82 added DefaultNudges entry: pattern \buntil\s+\[, severity warn. Idempotent via existing EnsureDefaultNudges (matches by Pattern). Ships with every project (CreateDefault writes it; EnsureDefaultNudges seeds existing dydo.json).

TESTS (+11 cases / 5 methods, 4194 passing total)
- DynaDocs.Tests/Commands/WorktreeCommandTests.cs:1947 Cleanup_DirectoryRemovalFails_DoesNotPrintCleanedUp (the regression test the brief asked for)
- DynaDocs.Tests/Commands/WorktreeCommandTests.cs:1979 Prune_DirectoryRemovalFails_DoesNotCountAsRemoved
- DynaDocs.Tests/Services/ConfigFactoryTests.cs:296 DefaultNudges_MatchesOpenEndedUntilLoop_AsWarn (3 InlineData)
- DynaDocs.Tests/Services/ConfigFactoryTests.cs:309 DefaultNudges_DoesNotMatchValidPollingPatterns (5 InlineData — for-loop, gh run watch, dydo wait, dydo wait --task, while-loop)
- DynaDocs.Tests/Services/ConfigFactoryTests.cs:321 DefaultNudges_UntilLoopNudge_IsIdempotent_InEnsureDefaultNudges

KEY DECISIONS
1. Brief proposed mocking RemoveZombieDirectory returning false — added an explicit override hook for that exact test seam.
2. Brief proposed bool returned through TeardownWorktree. Final return uses !Directory.Exists at the end of teardown rather than RemoveZombieDirectory's bool, so the user-facing log reflects disk truth even if RemoveGitWorktree's --force succeeds after RemoveZombieDirectory fails.
3. ExecutePrune now correctly counts only worktrees actually removed — '  Worktree X: directory remains ...' printed for the partial case so the aggregate 'Pruned N orphaned' line stays accurate.
4. Manual end-to-end smoke (drop pattern into a fresh dispatch brief) was not run — would have required spinning up a child agent with the new binary on PATH, and the unit tests already verify (a) regex compiles, (b) matches the bad pattern, (c) does NOT match valid alternatives, (d) is idempotent in EnsureDefaultNudges. The integration plumbing in GuardCommand.CheckNudges is unchanged.

VERIFICATION
- dotnet build clean (0 warnings, 0 errors).
- python DynaDocs.Tests/coverage/run_tests.py → Passed 4194 / Failed 0 (delta +11 from baseline 4183).
- python DynaDocs.Tests/coverage/gap_check.py --force-run → 141/141 modules at tier (100.0%).

CONTEXT
Pre-tag fixes for v1.4.7. v1.4.6 already shipped PR1+PR2+PR3. After your review lands, balazs is clear to push v1.4.7.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit 3c34dd2 for v1.4.7 pre-tag. Two small fixes — review independently:

FIX 1 (issue 0179) — worktree cleanup log honesty
- Commands/WorktreeCommand.cs:312 add RemoveZombieDirectoryOverride test seam
- Commands/WorktreeCommand.cs:746 RemoveZombieDirectory now returns bool (true if dir is gone, false on Windows file-lock — WARNING preserved on stderr)
- Commands/WorktreeCommand.cs:737-754 TeardownWorktree returns !Directory.Exists(worktreePath) — disk truth, not derived from inner bool, so RemoveGitWorktree's --force success after RemoveZombieDirectory failure also reports correctly
- Commands/WorktreeCommand.cs:331-339 ExecuteCleanup branches: success → 'cleaned up'; failure → 'marker removed; directory remains (in use by another process — see warning above). Will retry on next prune.'
- Commands/WorktreeCommand.cs:980-994 FinalizeMerge branches identically when refsRemaining==0 and branchDeleteExit==0
- Commands/WorktreeCommand.cs:1018-1024 ExecutePrune does NOT increment orphansRemoved on failure, prints '  Worktree X: directory remains ...' line

FIX 2 (issue 0177) — warn-nudge for open-ended bash polls
- Services/ConfigFactory.cs:78-82 added DefaultNudges entry: pattern \buntil\s+\[, severity warn. Idempotent via existing EnsureDefaultNudges (matches by Pattern). Ships with every project (CreateDefault writes it; EnsureDefaultNudges seeds existing dydo.json).

TESTS (+11 cases / 5 methods, 4194 passing total)
- DynaDocs.Tests/Commands/WorktreeCommandTests.cs:1947 Cleanup_DirectoryRemovalFails_DoesNotPrintCleanedUp (the regression test the brief asked for)
- DynaDocs.Tests/Commands/WorktreeCommandTests.cs:1979 Prune_DirectoryRemovalFails_DoesNotCountAsRemoved
- DynaDocs.Tests/Services/ConfigFactoryTests.cs:296 DefaultNudges_MatchesOpenEndedUntilLoop_AsWarn (3 InlineData)
- DynaDocs.Tests/Services/ConfigFactoryTests.cs:309 DefaultNudges_DoesNotMatchValidPollingPatterns (5 InlineData — for-loop, gh run watch, dydo wait, dydo wait --task, while-loop)
- DynaDocs.Tests/Services/ConfigFactoryTests.cs:321 DefaultNudges_UntilLoopNudge_IsIdempotent_InEnsureDefaultNudges

KEY DECISIONS
1. Brief proposed mocking RemoveZombieDirectory returning false — added an explicit override hook for that exact test seam.
2. Brief proposed bool returned through TeardownWorktree. Final return uses !Directory.Exists at the end of teardown rather than RemoveZombieDirectory's bool, so the user-facing log reflects disk truth even if RemoveGitWorktree's --force succeeds after RemoveZombieDirectory fails.
3. ExecutePrune now correctly counts only worktrees actually removed — '  Worktree X: directory remains ...' printed for the partial case so the aggregate 'Pruned N orphaned' line stays accurate.
4. Manual end-to-end smoke (drop pattern into a fresh dispatch brief) was not run — would have required spinning up a child agent with the new binary on PATH, and the unit tests already verify (a) regex compiles, (b) matches the bad pattern, (c) does NOT match valid alternatives, (d) is idempotent in EnsureDefaultNudges. The integration plumbing in GuardCommand.CheckNudges is unchanged.

VERIFICATION
- dotnet build clean (0 warnings, 0 errors).
- python DynaDocs.Tests/coverage/run_tests.py → Passed 4194 / Failed 0 (delta +11 from baseline 4183).
- python DynaDocs.Tests/coverage/gap_check.py --force-run → 141/141 modules at tier (100.0%).

CONTEXT
Pre-tag fixes for v1.4.7. v1.4.6 already shipped PR1+PR2+PR3. After your review lands, balazs is clear to push v1.4.7.

## Code Review

- Reviewed by: Emma
- Date: 2026-05-08 12:32
- Result: PASSED
- Notes: PASS. Both fixes match the brief precisely. FIX 1: TeardownWorktree return uses !Directory.Exists at the call boundary (disk truth, not inner-bool inference) — captures RemoveGitWorktree --force success after RemoveZombieDirectory failure. All 3 callers (ExecuteCleanup, FinalizeMerge, ExecutePrune) branch on the bool; ExecutePrune correctly does not increment orphansRemoved on failure. FIX 2: pattern \buntil\s+\[ is appropriately narrow, severity=warn, idempotent in EnsureDefaultNudges (matched by pattern). Tests: regression test Cleanup_DirectoryRemovalFails_DoesNotPrintCleanedUp asserts the exact pair the brief required (no 'cleaned up', yes 'directory remains'); Prune_DirectoryRemovalFails_DoesNotCountAsRemoved asserts Pruned 0 + 'directory remains'. Verification: 4194/4194 pass (delta +11), gap_check 141/141 at tier (100.0%), dydo check 0 errors. The 9 dydo check warnings are pre-existing orphan-doc warnings in project/inquisitions and project/issues, unrelated to this commit. v1.4.7 clear to tag.

Awaiting human approval.

## Approval

- Approved: 2026-05-08 12:36
