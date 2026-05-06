---
area: general
type: changelog
date: 2026-05-06
---

# Task: fix-pr2-dead-test

Review commit e3e6c47 on master, the fix to PR #0167's dead test that Frank flagged.

WHAT
- Deleted Compact_CorruptBaseline_DoesNotLeakConsoleErrorOnException (was AuditCompactionTests.cs:849-867). It was structurally dead: SnapshotCompactionService.Compact swallows malformed-baseline JSON to a stderr warning and returns (proven by the LogsWarningInsteadOfSilentSkip test directly above it), so the catch was unreachable and Assert.Same only re-verified the implicit success-path finally.
- Added DynaDocs.Tests/ConsoleCaptureTests.cs with two contract pins on ConsoleCapture.Stderr:
  - Stderr_RestoresConsoleError_WhenActionThrows: delegate throws InvalidOperationException("probe"); asserts the exception propagates AND Console.Error is restored to the pre-call instance. This is the OnException path the original test claimed but never exercised.
  - Stderr_RestoresConsoleError_WhenActionSucceeds: positive case asserting captured-output round-trip and Console.Error restoration.

DEVIATIONS
None from Frank's recommended option (a). Test names follow Subject_Scenario_Expectation.

VERIFICATION
- run_tests.py full suite: 4112/0 (4111 baseline -1 dead +2 new, exact expected delta).
- gap_check.py --force-run: 140/140 modules pass tier requirements.
- Single commit on top of d05f696 (HEAD moved past 405a220 during my session; commit is the next on master, not an amend).

REPORT BACK
Per baton-passing convention, after reviewing report back to Adele on task fix-pr2-dead-test. Adele dispatched me with --wait.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit e3e6c47 on master, the fix to PR #0167's dead test that Frank flagged.

WHAT
- Deleted Compact_CorruptBaseline_DoesNotLeakConsoleErrorOnException (was AuditCompactionTests.cs:849-867). It was structurally dead: SnapshotCompactionService.Compact swallows malformed-baseline JSON to a stderr warning and returns (proven by the LogsWarningInsteadOfSilentSkip test directly above it), so the catch was unreachable and Assert.Same only re-verified the implicit success-path finally.
- Added DynaDocs.Tests/ConsoleCaptureTests.cs with two contract pins on ConsoleCapture.Stderr:
  - Stderr_RestoresConsoleError_WhenActionThrows: delegate throws InvalidOperationException("probe"); asserts the exception propagates AND Console.Error is restored to the pre-call instance. This is the OnException path the original test claimed but never exercised.
  - Stderr_RestoresConsoleError_WhenActionSucceeds: positive case asserting captured-output round-trip and Console.Error restoration.

DEVIATIONS
None from Frank's recommended option (a). Test names follow Subject_Scenario_Expectation.

VERIFICATION
- run_tests.py full suite: 4112/0 (4111 baseline -1 dead +2 new, exact expected delta).
- gap_check.py --force-run: 140/140 modules pass tier requirements.
- Single commit on top of d05f696 (HEAD moved past 405a220 during my session; commit is the next on master, not an amend).

REPORT BACK
Per baton-passing convention, after reviewing report back to Adele on task fix-pr2-dead-test. Adele dispatched me with --wait.

## Code Review (2026-05-06 10:31)

- Reviewed by: Charlie
- Result: FAILED
- Issues: FAIL on gap_check (exit 1). The commit itself is clean: dead-test claim verified (LoadBaselines swallows malformed JSON; LogsWarningInsteadOfSilentSkip pins the swallow-to-stderr behavior, so the catch was unreachable); the two new ConsoleCapture.Stderr contract pins exercise both restore paths exactly as named (throw + success); run_tests.py 4112/0 matches the brief; and the change also removes one of the three gate-bypass Console.SetError sites flagged in inquisition #0167 (AuditCompactionTests.cs:843-854). However per reviewer mode gap_check must be green and exits 1 with three failures + one CRAP fail, all unrelated to e3e6c47 (which only touches DynaDocs.Tests/ConsoleCaptureTests.cs and DynaDocs.Tests/Services/AuditCompactionTests.cs): (1) CommandDocConsistencyTests.ReadmeClones_ContentInSync — Templates/about-dynadocs.template.md:335 lists '[--summary "..."]' on dydo issue create which dydo/reference/about-dynadocs.md does not; (2) CommandDocConsistencyTests.ReferenceDocAndTemplate_HaveSameOptions — same --summary drift vs dydo-commands.md reference (third failure same area, name not captured in tail output); (3) Commands/IssueCreateHandler.cs CRAP 30.2 > T1 30. Drift looks introduced by PR2 work (#0160 SummaryRule added --summary to template but not reference docs). Grace's PR2 review reported gap_check 140/140 — discrepancy worth a sanity re-run on the docs side. Reporting back per baton convention; not dispatching a fix from here since the failures are outside this task's scope.

Requires rework.

## Approval

- Approved: 2026-05-06 17:47
