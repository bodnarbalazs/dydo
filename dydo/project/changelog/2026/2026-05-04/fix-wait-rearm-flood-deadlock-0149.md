---
area: general
type: changelog
date: 2026-05-04
---

# Task: fix-wait-rearm-flood-deadlock-0149

Review the WaitGeneral snapshot fix per dydo/agents/Adele/plan-fix-wait-rearm-flood-deadlock-0149.md (archived at dydo/agents/Adele/archive/20260501-204845/plan-fix-wait-rearm-flood-deadlock-0149.md). Single commit 0844b2f touches Commands/WaitCommand.cs and DynaDocs.Tests/Integration/WaitCommandTests.cs only. Implementation: snapshot UnreadMessages before CreateListeningWaitMarker (extracted into private CaptureUnreadSnapshot helper to keep WaitGeneral CRAP under threshold), pass as excludeIds on each poll's MessageFinder.FindMessage. Comment block at the old :103-108 rewritten per plan to reflect the new contract. Tests: deleted WaitGeneral_FiresOnSecondMessage_ArrivedDuringRearmGap (Option A in plan; its contract was the exact behaviour #0149 changes), added three regression tests in a new region Snapshot-At-Registration Tests (#0149) — DoesNotFireOnPreStackedUnreads_PreventsRearmFloodDeadlock, FiresOnPostRegistrationArrival_EvenWithPreStackedUnreads, StaysBlockedAfterDrain_NoSpuriousFire. Test #3 uses ClearAllUnreadMessages to simulate drain (per plan parenthetical fallback). Full test suite green: 4036 passed, 0 failed. WaitCommand.cs gap_check passes (CRAP 28.0, threshold 30) after the helper extraction. HEADS-UP for reviewer: gap_check on the worktree run reports ONE remaining failure on Services/WatchdogService.cs (CRAP 40.4, CC 40, many uncovered lines). That failure is pre-existing and entirely unrelated to this task — driven by uncommitted in-flight watchdog and auto-resume work present in the working tree (Services/WatchdogService.cs, AgentRegistry.cs, IAgentRegistry.cs, ProcessUtils.Ancestry.cs, MacTerminalLauncher.cs, etc., and tied to open issues 0150, 0151, 0152, 0154). My commit does not touch any of those files. Per plan scope decisions, Findings 3 (#0155 guard bypass), 4 (#0156 docs), and 6 (#0158 dead-code) are explicitly out of scope and remain for separate slices. Plan also calls out Suggestion B (narrow MissingGeneralWait bypass) as deliberately not included since the snapshot fix removes the need. Reviewer flow: read the plan, the inquisition at dydo/project/inquisitions/wait-rearm-flood-deadlock.md, Brian's CONFIRMED rulings inlined under each finding, then verify the diff matches the planned approach. Manual smoke (Noah's smoke-wait-flood-v142) is optional but cheap. Reply on this task subject when done.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review the WaitGeneral snapshot fix per dydo/agents/Adele/plan-fix-wait-rearm-flood-deadlock-0149.md (archived at dydo/agents/Adele/archive/20260501-204845/plan-fix-wait-rearm-flood-deadlock-0149.md). Single commit 0844b2f touches Commands/WaitCommand.cs and DynaDocs.Tests/Integration/WaitCommandTests.cs only. Implementation: snapshot UnreadMessages before CreateListeningWaitMarker (extracted into private CaptureUnreadSnapshot helper to keep WaitGeneral CRAP under threshold), pass as excludeIds on each poll's MessageFinder.FindMessage. Comment block at the old :103-108 rewritten per plan to reflect the new contract. Tests: deleted WaitGeneral_FiresOnSecondMessage_ArrivedDuringRearmGap (Option A in plan; its contract was the exact behaviour #0149 changes), added three regression tests in a new region Snapshot-At-Registration Tests (#0149) — DoesNotFireOnPreStackedUnreads_PreventsRearmFloodDeadlock, FiresOnPostRegistrationArrival_EvenWithPreStackedUnreads, StaysBlockedAfterDrain_NoSpuriousFire. Test #3 uses ClearAllUnreadMessages to simulate drain (per plan parenthetical fallback). Full test suite green: 4036 passed, 0 failed. WaitCommand.cs gap_check passes (CRAP 28.0, threshold 30) after the helper extraction. HEADS-UP for reviewer: gap_check on the worktree run reports ONE remaining failure on Services/WatchdogService.cs (CRAP 40.4, CC 40, many uncovered lines). That failure is pre-existing and entirely unrelated to this task — driven by uncommitted in-flight watchdog and auto-resume work present in the working tree (Services/WatchdogService.cs, AgentRegistry.cs, IAgentRegistry.cs, ProcessUtils.Ancestry.cs, MacTerminalLauncher.cs, etc., and tied to open issues 0150, 0151, 0152, 0154). My commit does not touch any of those files. Per plan scope decisions, Findings 3 (#0155 guard bypass), 4 (#0156 docs), and 6 (#0158 dead-code) are explicitly out of scope and remain for separate slices. Plan also calls out Suggestion B (narrow MissingGeneralWait bypass) as deliberately not included since the snapshot fix removes the need. Reviewer flow: read the plan, the inquisition at dydo/project/inquisitions/wait-rearm-flood-deadlock.md, Brian's CONFIRMED rulings inlined under each finding, then verify the diff matches the planned approach. Manual smoke (Noah's smoke-wait-flood-v142) is optional but cheap. Reply on this task subject when done.

## Code Review

- Reviewed by: Dexter
- Date: 2026-05-01 22:57
- Result: PASSED
- Notes: PASS. Code is clean, plan-faithful, well-tested. WaitGeneral snapshot capture is correctly placed before CreateListeningWaitMarker; comment block accurately rewritten (resolves Inquisition Finding #5/#0157 inline); CaptureUnreadSnapshot helper appropriately scoped. Three new regression tests cover the contract; 94339bc cleanly addresses my comment-imprecision feedback. Out-of-scope items (#0155/#0156/#0158/Suggestion B) correctly deferred per plan. At Adele's slice (0844b2f + 94339bc), 4036 tests pass and gap_check passes on WaitCommand.cs (CRAP 28.0). The literal gap_check rule is overridden by user decision: the only failing module is Services/WatchdogService.cs (CRAP 40.4) which is pre-existing and being addressed in the user's separate commit 9b27195 (closes #0151/#0152). A racing test (IncrementResumeAttempts_ConcurrentCalls_ProduceExactCount) introduced in 9b27195 also fails — separate slice, not Adele's responsibility.

Awaiting human approval.

## Approval

- Approved: 2026-05-04 21:52
