---
area: general
type: changelog
date: 2026-05-06
---

# Task: implement-pr3-git-helper-drain

Review PR3 of the runtime-regression batch — commit 6d00b4c on master. Test-file scope only, no production code changed.

CHANGES
- DynaDocs.Tests/Services/SnapshotServiceTests.cs:52-87 — drain pattern applied to RunGit (5s timeout, throw-on-timeout/throw-on-nonzero-exit, surface captured stderr).
- DynaDocs.Tests/Services/SnapshotServiceTests.cs (new test, end of file) — RunGit_NoisyOutput_DoesNotDeadlock. Mirrors WorktreeMergeSafetyGitHelperTests.Git_NoisyOutput_DoesNotDeadlock: 256 KB content + git log -p, assert under 10 s.
- DynaDocs.Tests/Integration/InquisitionTests.cs:14-55 — InitGitRepo refactored to call a new private RunGit(string args) helper using the drain pattern with the same 5 s timeout and throw semantics.
- DynaDocs.Tests/Integration/InquisitionTests.cs (new test, RunGit Helper Tests region) — InitGitRepo_CompletesAndProducesValidRepository, the lighter contract pin per plan recommendation.

CANONICAL SHAPE
Mirrors aeee461 (#0148) exactly: using var process = Process.Start(psi) plus the null-throw, concurrent ReadToEndAsync on both pipes before WaitForExit, on timeout Kill(entireProcessTree:true) and throw the timeout message, on non-zero exit surface stderrTask.GetAwaiter().GetResult() in the throw. The 5 s timeout matches the existing per-helper contract (vs aeee461's 30 s).

PLAN DEVIATIONS
None. Followed dydo/agents/Dexter/archive/20260505-173954/plan-runtime-regression-batch.md PR3 section verbatim. Did NOT take the optional TestProcess.cs extraction (plan recommendation: defer).

KEY DECISIONS
- The new regression test in SnapshotServiceTests reuses the existing private RunGit. If the helper ever deadlocks again, the 5 s timeout fires and RunGit throws — and the assertion Stopwatch under 10 s still catches the regression cleanly because the throw happens within the test's measured window.
- The InquisitionTests regression test is intentionally lighter (just confirms .git and HEAD exist after InitGitRepo). The plan flagged the noisy-output test there as not deterministically reproducible at the 5 s timeout for init and commit --allow-empty.

VERIFICATION GATE
- dotnet build: clean
- python DynaDocs.Tests/coverage/run_tests.py: 4120 of 4120 passed (was 4118 baseline plus 2 new tests)
- python DynaDocs.Tests/coverage/gap_check.py --force-run: 140 of 140 modules pass tier requirements (exit 0)

PR4 NOTES
None. PR3 is mechanically self-contained; PR4 (production helper unification) does not need plan changes from PR3 findings.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review PR3 of the runtime-regression batch — commit 6d00b4c on master. Test-file scope only, no production code changed.

CHANGES
- DynaDocs.Tests/Services/SnapshotServiceTests.cs:52-87 — drain pattern applied to RunGit (5s timeout, throw-on-timeout/throw-on-nonzero-exit, surface captured stderr).
- DynaDocs.Tests/Services/SnapshotServiceTests.cs (new test, end of file) — RunGit_NoisyOutput_DoesNotDeadlock. Mirrors WorktreeMergeSafetyGitHelperTests.Git_NoisyOutput_DoesNotDeadlock: 256 KB content + git log -p, assert under 10 s.
- DynaDocs.Tests/Integration/InquisitionTests.cs:14-55 — InitGitRepo refactored to call a new private RunGit(string args) helper using the drain pattern with the same 5 s timeout and throw semantics.
- DynaDocs.Tests/Integration/InquisitionTests.cs (new test, RunGit Helper Tests region) — InitGitRepo_CompletesAndProducesValidRepository, the lighter contract pin per plan recommendation.

CANONICAL SHAPE
Mirrors aeee461 (#0148) exactly: using var process = Process.Start(psi) plus the null-throw, concurrent ReadToEndAsync on both pipes before WaitForExit, on timeout Kill(entireProcessTree:true) and throw the timeout message, on non-zero exit surface stderrTask.GetAwaiter().GetResult() in the throw. The 5 s timeout matches the existing per-helper contract (vs aeee461's 30 s).

PLAN DEVIATIONS
None. Followed dydo/agents/Dexter/archive/20260505-173954/plan-runtime-regression-batch.md PR3 section verbatim. Did NOT take the optional TestProcess.cs extraction (plan recommendation: defer).

KEY DECISIONS
- The new regression test in SnapshotServiceTests reuses the existing private RunGit. If the helper ever deadlocks again, the 5 s timeout fires and RunGit throws — and the assertion Stopwatch under 10 s still catches the regression cleanly because the throw happens within the test's measured window.
- The InquisitionTests regression test is intentionally lighter (just confirms .git and HEAD exist after InitGitRepo). The plan flagged the noisy-output test there as not deterministically reproducible at the 5 s timeout for init and commit --allow-empty.

VERIFICATION GATE
- dotnet build: clean
- python DynaDocs.Tests/coverage/run_tests.py: 4120 of 4120 passed (was 4118 baseline plus 2 new tests)
- python DynaDocs.Tests/coverage/gap_check.py --force-run: 140 of 140 modules pass tier requirements (exit 0)

PR4 NOTES
None. PR3 is mechanically self-contained; PR4 (production helper unification) does not need plan changes from PR3 findings.

## Code Review

- Reviewed by: Dexter
- Date: 2026-05-06 14:14
- Result: PASSED
- Notes: PASS. Drain pattern in both helpers matches aeee461 byte-for-byte (modulo 5s vs 30s timeout per existing per-helper contract). 4120/4120 tests pass; gap_check.py --force-run = 140/140 at tier. Test-file scope only; no production code touched.

Awaiting human approval.

## Approval

- Approved: 2026-05-06 17:47
