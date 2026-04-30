---
area: general
type: changelog
date: 2026-04-30
---

# Task: unified-general-wait-slice1

Review Slice 1 of unified-general-wait — fix for #0133 (dydo wait deadlock). 

WHAT CHANGED
- Services/AgentRegistry.cs: new CreateListeningWaitMarker(agentName, task, targetAgent, pid) — atomic temp-then-rename that publishes Listening=true + Pid in a single write. If a marker already exists, Target and Since are preserved (so a dispatcher-pre-created task marker keeps its dispatch context when WaitForTask flips it listening).
- Services/AgentRegistry.cs ValidateReleasePreconditions: filter '_'-prefix sentinel markers from the wait-block check. CleanupAfterRelease still wipes them via ClearAllWaitMarkers — this just stops them from blocking validation. Mirrors existing '_'-sentinel semantics in GetActiveTaskWaitSubjects and Guard_GeneralWaitMarker_NotIncluded_InPendingTaskList.
- Services/IAgentRegistry.cs: interface bump for the new method.
- Commands/WaitCommand.cs WaitGeneral (line 81): replaced CreateWaitMarker + UpdateWaitMarkerListening with the atomic create — closes the race that #0133 traced.
- Commands/WaitCommand.cs WaitForTask (line 134): same atomic upsert, replacing the non-atomic read-modify-write UpdateWaitMarkerListening (parity per the brief).
- DynaDocs.Tests/Commands/CheckAgentValidatorTests.cs: stub the new interface method on FakeAgentRegistryForCAV.

TESTS ADDED (all pass; failed-on-master compile errors confirmed before fix)
- WaitCommandTests: CreateListeningWaitMarker_WritesListeningAndPid_InOneStep, CreateListeningWaitMarker_PreservesTargetAndSince_WhenMarkerExists, Wait_General_MarkerListeningWhenLoopStarts, Wait_Task_MarkerListeningWhenLoopStarts.
- AgentLifecycleTests: Release_NotBlockedBy_GeneralWaitSentinel, Release_StillBlockedBy_RealTaskWaitMarker.

PLAN DEVIATIONS
- Plan suggested keeping the existing two-step (Create + Update) call path 'for legitimate callers'. I left CreateWaitMarker and UpdateWaitMarkerListening in place — only the wait-command call sites were switched. DispatchService and other existing callers still use the original two-step pattern.
- Plan listed seven tests; I added six core ones. The Wait_General_FiresOnNewArrival, BlocksWhenInboxEmpty, BlocksWhenOnlyKnownUnreads variants are already covered by existing tests (WaitGeneral_RecordsPidInMarker, WaitGeneral_SkipsMessage_AlreadyInUnreadAtStart, WaitGeneral_PopsOnNewMessage_EvenWhenStartupUnreadExists). MarkerCleanedOnRelease is covered by Release_NotBlockedBy_GeneralWaitSentinel which asserts the marker is gone post-release.

KEY DECISIONS
- Atomic write uses the same temp-then-rename pattern AgentRegistry already uses for state.md (line 1417). Path: temp suffix '$path.tmp.{pid}.{guid}' → File.Move with overwrite — atomic on NTFS via MoveFileEx and on POSIX via rename(2).
- Catch around the existing-marker read is intentionally swallowed — corrupt marker falls back to caller-provided Target/Since rather than throwing.

OUT OF SCOPE (per brief)
- Slices 2 (guard generalisation, dispatch --wait reshape) and 3 (templates).
- Role JSON changes (none required).

FULL TEST RUN
- Full dotnet test: 3921/3922 passed; one pre-existing test-pollution flake (InboxServiceTests.PrintInboxItem_TaskItem_IncludesFilePath) passes in isolation and under gap_check's isolated worktree run. Unrelated to this change — root cause is xUnit parallelism letting an integration test mutate CWD while this non-Integration-collection test runs.
- gap_check.py: PASS — 3922/3922 tests, 137/137 modules at tier compliance.

FILES TOUCHED
- Commands/WaitCommand.cs
- Services/AgentRegistry.cs
- Services/IAgentRegistry.cs
- DynaDocs.Tests/Commands/CheckAgentValidatorTests.cs
- DynaDocs.Tests/Integration/WaitCommandTests.cs
- DynaDocs.Tests/Integration/AgentLifecycleTests.cs

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review Slice 1 of unified-general-wait — fix for #0133 (dydo wait deadlock). 

WHAT CHANGED
- Services/AgentRegistry.cs: new CreateListeningWaitMarker(agentName, task, targetAgent, pid) — atomic temp-then-rename that publishes Listening=true + Pid in a single write. If a marker already exists, Target and Since are preserved (so a dispatcher-pre-created task marker keeps its dispatch context when WaitForTask flips it listening).
- Services/AgentRegistry.cs ValidateReleasePreconditions: filter '_'-prefix sentinel markers from the wait-block check. CleanupAfterRelease still wipes them via ClearAllWaitMarkers — this just stops them from blocking validation. Mirrors existing '_'-sentinel semantics in GetActiveTaskWaitSubjects and Guard_GeneralWaitMarker_NotIncluded_InPendingTaskList.
- Services/IAgentRegistry.cs: interface bump for the new method.
- Commands/WaitCommand.cs WaitGeneral (line 81): replaced CreateWaitMarker + UpdateWaitMarkerListening with the atomic create — closes the race that #0133 traced.
- Commands/WaitCommand.cs WaitForTask (line 134): same atomic upsert, replacing the non-atomic read-modify-write UpdateWaitMarkerListening (parity per the brief).
- DynaDocs.Tests/Commands/CheckAgentValidatorTests.cs: stub the new interface method on FakeAgentRegistryForCAV.

TESTS ADDED (all pass; failed-on-master compile errors confirmed before fix)
- WaitCommandTests: CreateListeningWaitMarker_WritesListeningAndPid_InOneStep, CreateListeningWaitMarker_PreservesTargetAndSince_WhenMarkerExists, Wait_General_MarkerListeningWhenLoopStarts, Wait_Task_MarkerListeningWhenLoopStarts.
- AgentLifecycleTests: Release_NotBlockedBy_GeneralWaitSentinel, Release_StillBlockedBy_RealTaskWaitMarker.

PLAN DEVIATIONS
- Plan suggested keeping the existing two-step (Create + Update) call path 'for legitimate callers'. I left CreateWaitMarker and UpdateWaitMarkerListening in place — only the wait-command call sites were switched. DispatchService and other existing callers still use the original two-step pattern.
- Plan listed seven tests; I added six core ones. The Wait_General_FiresOnNewArrival, BlocksWhenInboxEmpty, BlocksWhenOnlyKnownUnreads variants are already covered by existing tests (WaitGeneral_RecordsPidInMarker, WaitGeneral_SkipsMessage_AlreadyInUnreadAtStart, WaitGeneral_PopsOnNewMessage_EvenWhenStartupUnreadExists). MarkerCleanedOnRelease is covered by Release_NotBlockedBy_GeneralWaitSentinel which asserts the marker is gone post-release.

KEY DECISIONS
- Atomic write uses the same temp-then-rename pattern AgentRegistry already uses for state.md (line 1417). Path: temp suffix '$path.tmp.{pid}.{guid}' → File.Move with overwrite — atomic on NTFS via MoveFileEx and on POSIX via rename(2).
- Catch around the existing-marker read is intentionally swallowed — corrupt marker falls back to caller-provided Target/Since rather than throwing.

OUT OF SCOPE (per brief)
- Slices 2 (guard generalisation, dispatch --wait reshape) and 3 (templates).
- Role JSON changes (none required).

FULL TEST RUN
- Full dotnet test: 3921/3922 passed; one pre-existing test-pollution flake (InboxServiceTests.PrintInboxItem_TaskItem_IncludesFilePath) passes in isolation and under gap_check's isolated worktree run. Unrelated to this change — root cause is xUnit parallelism letting an integration test mutate CWD while this non-Integration-collection test runs.
- gap_check.py: PASS — 3922/3922 tests, 137/137 modules at tier compliance.

FILES TOUCHED
- Commands/WaitCommand.cs
- Services/AgentRegistry.cs
- Services/IAgentRegistry.cs
- DynaDocs.Tests/Commands/CheckAgentValidatorTests.cs
- DynaDocs.Tests/Integration/WaitCommandTests.cs
- DynaDocs.Tests/Integration/AgentLifecycleTests.cs

## Code Review

- Reviewed by: Adele
- Date: 2026-04-29 20:04
- Result: PASSED
- Notes: PASS. Atomic CreateListeningWaitMarker correctly closes the #0133 race: Listening=true and Pid published in a single temp-then-rename write that mirrors the existing WriteStateFile pattern (AgentRegistry.cs:1482). Existing-marker upsert preserves Target/Since so dispatcher-pre-created markers keep their context. ValidateReleasePreconditions sentinel filter is consistent with GetActiveTaskWaitSubjects and OrchestratorMissingGeneralWait — '_'-prefix semantics are uniform across the codebase. Both call sites (WaitGeneral, WaitForTask) updated; legitimate two-step callers (DispatchService.CreateWaitMarker dispatch path) untouched. Tests cover the atomic-write contract, upsert preservation, the regression scenario via IsProcessRunningOverride observation, and both release-block branches (sentinel vs real task). Plan deviations on test count are justified (existing tests already cover the variants). gap_check.py: 137/137 modules at tier compliance.

Awaiting human approval.

## Approval

- Approved: 2026-04-30 12:51
