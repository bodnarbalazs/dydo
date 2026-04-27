---
area: general
type: changelog
date: 2026-04-27
---

# Task: orchestrator-general-wait-priority-routing

Review wait priority routing + new orchestrator general-wait guardrail + orchestrator template change.

See git log master..HEAD (note: all changes squashed by user into commit 06cbfda 'wait updates in progress', not 3 separate commits as Brian's brief expected).

Two code changes plus tests + template:

1) Routing fix — Commands/WaitCommand.cs: WaitGeneral now re-reads claimed task subjects each poll cycle (was startup snapshot). New helper GetActiveTaskWaitSubjects filters out '_'-prefix sentinels. Brief was 'Option A'; the implementation is exactly that.

2) Orchestrator general-wait guardrail (added per user direction mid-task — NOT in Brian's original brief, surfaced in conversation). Commands/GuardCommand.cs: CheckPendingState now also blocks when an orchestrator has dispatched at least one task wait but lacks a listening '_general-wait' marker with a live PID. SelfHealAndGetPendingMarkers was updated to delete sentinel markers (instead of flipping listening) when their PID is dead, and to filter sentinel markers out of the pending task-list display. The dydo-wait bash bypass (IsDydoWaitAnyForm) covers starting the general wait.

3) Tests — DynaDocs.Tests/Integration/WaitCommandTests.cs (5 routing tests) and PendingStateGuardTests.cs (8 guardrail tests). Verify: subject exclusion semantics; general wait skips matching task subject; general wait fires on unmatched subject; both-active priority via FindMessage views; per-call freshness of GetActiveTaskWaitSubjects; orchestrator blocked without general wait; allowed when listening; dead-PID blocks + self-heal removes stale marker; non-orchestrators unaffected; dydo wait bypass works; combined error message; sentinel not in task list.

4) Template — Templates/mode-orchestrator.template.md: added Monitor-section paragraph + bash snippet for the keep-one-general-wait-open policy, and a one-line Complete-section note about parent-PID teardown.

Verify race scenario coverage (regression goal from brief): GetActiveTaskWaitSubjects_ReturnsFreshState_AcrossCalls + WaitGeneral_TaskWaitWins_WhenBothActive together establish that a task wait registered after general wait startup is excluded by the next poll. Single-iteration tests can't directly exercise multi-poll registration timing — flagged for review judgment.

Verify template change is minimal — addition only, no rewriting of surrounding prose.

Tests: 3853/3853 pass. gap_check exit 0, 136/136 tier-pass.

Out-of-scope items left untouched per brief: dydo wait --cancel destructiveness; rearm sentinel naming; OS notifications.

Open follow-up worth considering (your call to file or skip): the new guardrail is gated on 'orchestrator has at least one task wait marker' — a stricter reading of the user's directive ('always have one open') would gate purely on role==orchestrator after onboarding. Narrow gating chosen to avoid blocking onboarding reads. Worth a follow-up issue or fine.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review wait priority routing + new orchestrator general-wait guardrail + orchestrator template change.

See git log master..HEAD (note: all changes squashed by user into commit 06cbfda 'wait updates in progress', not 3 separate commits as Brian's brief expected).

Two code changes plus tests + template:

1) Routing fix — Commands/WaitCommand.cs: WaitGeneral now re-reads claimed task subjects each poll cycle (was startup snapshot). New helper GetActiveTaskWaitSubjects filters out '_'-prefix sentinels. Brief was 'Option A'; the implementation is exactly that.

2) Orchestrator general-wait guardrail (added per user direction mid-task — NOT in Brian's original brief, surfaced in conversation). Commands/GuardCommand.cs: CheckPendingState now also blocks when an orchestrator has dispatched at least one task wait but lacks a listening '_general-wait' marker with a live PID. SelfHealAndGetPendingMarkers was updated to delete sentinel markers (instead of flipping listening) when their PID is dead, and to filter sentinel markers out of the pending task-list display. The dydo-wait bash bypass (IsDydoWaitAnyForm) covers starting the general wait.

3) Tests — DynaDocs.Tests/Integration/WaitCommandTests.cs (5 routing tests) and PendingStateGuardTests.cs (8 guardrail tests). Verify: subject exclusion semantics; general wait skips matching task subject; general wait fires on unmatched subject; both-active priority via FindMessage views; per-call freshness of GetActiveTaskWaitSubjects; orchestrator blocked without general wait; allowed when listening; dead-PID blocks + self-heal removes stale marker; non-orchestrators unaffected; dydo wait bypass works; combined error message; sentinel not in task list.

4) Template — Templates/mode-orchestrator.template.md: added Monitor-section paragraph + bash snippet for the keep-one-general-wait-open policy, and a one-line Complete-section note about parent-PID teardown.

Verify race scenario coverage (regression goal from brief): GetActiveTaskWaitSubjects_ReturnsFreshState_AcrossCalls + WaitGeneral_TaskWaitWins_WhenBothActive together establish that a task wait registered after general wait startup is excluded by the next poll. Single-iteration tests can't directly exercise multi-poll registration timing — flagged for review judgment.

Verify template change is minimal — addition only, no rewriting of surrounding prose.

Tests: 3853/3853 pass. gap_check exit 0, 136/136 tier-pass.

Out-of-scope items left untouched per brief: dydo wait --cancel destructiveness; rearm sentinel naming; OS notifications.

Open follow-up worth considering (your call to file or skip): the new guardrail is gated on 'orchestrator has at least one task wait marker' — a stricter reading of the user's directive ('always have one open') would gate purely on role==orchestrator after onboarding. Narrow gating chosen to avoid blocking onboarding reads. Worth a follow-up issue or fine.

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-27 15:12
- Result: PASSED
- Notes: PASS. Routing: re-read of claimed subjects per poll cycle correctly closes the dispatch-time race; sentinel-prefix filter (GetActiveTaskWaitSubjects) prevents self-exclusion of the general wait. Guard: OrchestratorMissingGeneralWait gating on 'has at least one task wait' is the right call - blocking onboarding reads would be wrong. Self-heal correctly distinguishes sentinel markers (delete) from task markers (reset listening), and pending task list is filtered to hide sentinels. Bash bypass via IsDydoWaitAnyForm lets the orchestrator start the required wait. Tests: 3853/3853 pass, gap_check 136/136. Race coverage is structurally proven via GetActiveTaskWaitSubjects_ReturnsFreshState_AcrossCalls + WaitGeneral_TaskWaitWins_WhenBothActive - sufficient given multi-poll loop timing is awkward to test directly. Template change is minimal additive (Monitor + Complete sections). No new security concerns. The narrow gating in OrchestratorMissingGeneralWait is correct as-is - not filing a follow-up.

Awaiting human approval.

## Approval

- Approved: 2026-04-27 15:31
