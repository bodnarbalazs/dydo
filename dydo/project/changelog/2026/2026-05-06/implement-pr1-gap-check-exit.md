---
area: general
type: changelog
date: 2026-05-06
---

# Task: implement-pr1-gap-check-exit

Review PR1 of runtime-regression batch (#0169): gap_check.py exit-code propagation. Commit: 12e30e9.

Three changes (per plan PR1 section, archived at dydo/agents/Dexter/archive/20260505-173954/plan-runtime-regression-batch.md):
1. Upfront 'tests_ok = True' before the if/else (Emma's caveat #3 — staleness-skip path never assigns it; without this the new reference would UnboundLocalError on the fresh-skip path).
2. Fold 'or not tests_ok' into the exit condition at end of main().
3. Gate FAILS banner emitted only when 'not tests_ok' (addresses the rollout risk that humans see '[RESULT] All modules pass tier requirements' followed silently by exit 1).

No plan deviations. Single file changed: DynaDocs.Tests/coverage/gap_check.py (+9/-1).

Verification:
- Clean tree (--force-run): 4086 tests passed, no Gate FAILS banner, exit 0.
- Deliberate failing-test injection (untracked DynaDocs.Tests/RegressionProbe.cs deleted before commit): worktree picked up the probe via copy_dirty_files, 5 failures observed, Gate FAILS banner printed ('Tier check: pass. Gate FAILS.'), then sys.exit(1) reached cleanly.
- dotnet build: 0 warnings, 0 errors.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review PR1 of runtime-regression batch (#0169): gap_check.py exit-code propagation. Commit: 12e30e9.

Three changes (per plan PR1 section, archived at dydo/agents/Dexter/archive/20260505-173954/plan-runtime-regression-batch.md):
1. Upfront 'tests_ok = True' before the if/else (Emma's caveat #3 — staleness-skip path never assigns it; without this the new reference would UnboundLocalError on the fresh-skip path).
2. Fold 'or not tests_ok' into the exit condition at end of main().
3. Gate FAILS banner emitted only when 'not tests_ok' (addresses the rollout risk that humans see '[RESULT] All modules pass tier requirements' followed silently by exit 1).

No plan deviations. Single file changed: DynaDocs.Tests/coverage/gap_check.py (+9/-1).

Verification:
- Clean tree (--force-run): 4086 tests passed, no Gate FAILS banner, exit 0.
- Deliberate failing-test injection (untracked DynaDocs.Tests/RegressionProbe.cs deleted before commit): worktree picked up the probe via copy_dirty_files, 5 failures observed, Gate FAILS banner printed ('Tier check: pass. Gate FAILS.'), then sys.exit(1) reached cleanly.
- dotnet build: 0 warnings, 0 errors.

## Code Review

- Reviewed by: Charlie
- Date: 2026-05-05 19:12
- Result: PASSED
- Notes: PASS (soft-pass per Adele's orchestrator call, balazs's convention for the runtime-regression batch). PR1 implementation matches plan exactly: tests_ok=True initialized at gap_check.py:677 with why-comment at :674-676; 'or not tests_ok' folded into exit at :725; Gate FAILS banner at :721-724. Surgical +9/-1, no plan deviations, dotnet build clean, dydo check errors all pre-existing. gap_check.py exits non-zero on this branch but the failures are race-based flakes in already-tracked, plan-acknowledged out-of-scope territory (Compact_CorruptBaseline_LogsWarningInsteadOfSilentSkip = #0167 site PR2 migrates; IncrementResumeAttempts_ConcurrentCalls_ProduceExactCount = #0165 failure #1; EnsureRunning_RegistersClaudeAnchor_InAnchorsDirectory = Finding #5 family). Two runs produced three distinct failing tests with zero overlap, confirming race rather than deterministic break. PR1's banner appeared as designed — the planned mitigation for rollout-risk #3 is working. PR2 #0167 should now land next per plan ordering. Full findings: dydo/agents/Charlie/notes/review-pr1-gap-check-exit.md.

Awaiting human approval.

## Approval

- Approved: 2026-05-06 17:47
