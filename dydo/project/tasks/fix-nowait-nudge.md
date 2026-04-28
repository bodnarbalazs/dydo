---
area: general
name: fix-nowait-nudge
status: human-reviewed
created: 2026-04-28T14:28:31.7163437Z
assigned: Emma
updated: 2026-04-28T14:52:55.6606154Z
---

# Task: fix-nowait-nudge

Review the --no-wait nudge implementation on branch worktree/fix-nowait-nudge (4 commits since 45fc6f1).

Plan: dydo/agents/Dexter/archive/20260428-142558/plan-orchestrator-nowait-nudge.md (verbatim mirror of CheckNoLaunchNudge, gated on CanOrchestrate).

Production changes:
- Services/DispatchService.cs — CheckNoWaitNudge helper + call site after CheckWaitPrivilege, before CheckDispatchRestriction.
- Services/AgentRegistry.cs — explicit .no-wait-nudge-* cleanup line in CleanupAfterRelease (the generic .nudge-* glob is anchored and does NOT cover the new prefix).

Tests added:
- DynaDocs.Tests/Integration/DispatchCommandTests.cs — 5 tests in a new "--no-wait Nudge Tests" region: OrchestratorFirstAttempt_FailsWithNudge, OrchestratorSecondAttempt_Succeeds, WithoutSender_SkipsNudge, OrchestratorMarkerCleanedOnRelease, CodeWriter_SkipsNudge (CanOrchestrate gate proof).
- DynaDocs.Tests/Integration/DispatchWaitIntegrationTests.cs — added parallel .no-wait-nudge-* bypass to DispatchInSeparateSessionWithResult so two pre-existing inquisitor-release tests (AfterJudgeDispatch / AfterInquisitorRedispatch) keep exercising the dispatch path under test rather than the new nudge.

Verification:
- dotnet build clean.
- run_tests.py full: 3858 passed, 0 failed (one transient flake first run, clean rerun).
- gap_check.py: 136/136 modules pass tier (exit 0).

Plan deviations:
- Test 4.2 / 4.4 call BypassNoLaunchNudge twice rather than once (first attempt of the --no-wait test consumes the no-launch bypass before the no-wait nudge fires, so a fresh bypass is needed before the second call).
- DispatchWaitIntegrationTests helper change wasn't in the plan but kept two pre-existing tests green.

Origin: Adele (already msg'd directly). Reply obligation already fulfilled.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review the --no-wait nudge implementation on branch worktree/fix-nowait-nudge (4 commits since 45fc6f1).

Plan: dydo/agents/Dexter/archive/20260428-142558/plan-orchestrator-nowait-nudge.md (verbatim mirror of CheckNoLaunchNudge, gated on CanOrchestrate).

Production changes:
- Services/DispatchService.cs — CheckNoWaitNudge helper + call site after CheckWaitPrivilege, before CheckDispatchRestriction.
- Services/AgentRegistry.cs — explicit .no-wait-nudge-* cleanup line in CleanupAfterRelease (the generic .nudge-* glob is anchored and does NOT cover the new prefix).

Tests added:
- DynaDocs.Tests/Integration/DispatchCommandTests.cs — 5 tests in a new "--no-wait Nudge Tests" region: OrchestratorFirstAttempt_FailsWithNudge, OrchestratorSecondAttempt_Succeeds, WithoutSender_SkipsNudge, OrchestratorMarkerCleanedOnRelease, CodeWriter_SkipsNudge (CanOrchestrate gate proof).
- DynaDocs.Tests/Integration/DispatchWaitIntegrationTests.cs — added parallel .no-wait-nudge-* bypass to DispatchInSeparateSessionWithResult so two pre-existing inquisitor-release tests (AfterJudgeDispatch / AfterInquisitorRedispatch) keep exercising the dispatch path under test rather than the new nudge.

Verification:
- dotnet build clean.
- run_tests.py full: 3858 passed, 0 failed (one transient flake first run, clean rerun).
- gap_check.py: 136/136 modules pass tier (exit 0).

Plan deviations:
- Test 4.2 / 4.4 call BypassNoLaunchNudge twice rather than once (first attempt of the --no-wait test consumes the no-launch bypass before the no-wait nudge fires, so a fresh bypass is needed before the second call).
- DispatchWaitIntegrationTests helper change wasn't in the plan but kept two pre-existing tests green.

Origin: Adele (already msg'd directly). Reply obligation already fulfilled.

## Code Review

- Reviewed by: Kate
- Date: 2026-04-28 15:03
- Result: PASSED
- Notes: PASS. CheckNoWaitNudge is a clean verbatim mirror of CheckNoLaunchNudge with the three documented diffs (CanOrchestrate gate, marker prefix, message). Call site sequencing is correct. AgentRegistry cleanup is required and correctly placed. All 5 new tests cover the gates (orchestrator first/second attempt, no-sender skip, marker release cleanup, code-writer inverse). DispatchWaitIntegrationTests helper change is minimal and well-explained. run_tests.py: 3858/3858 on rerun (one transient static-state flake in StaleDispatchDoubleClaimTests, unrelated to nudge changes — passed in isolation). gap_check.py --force-run: 136/136 modules pass tier, exit 0.

Awaiting human approval.