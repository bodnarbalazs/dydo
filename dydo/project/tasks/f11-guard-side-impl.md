---
area: general
name: f11-guard-side-impl
status: human-reviewed
created: 2026-05-23T10:48:33.7153047Z
assigned: Dexter
updated: 2026-05-23T16:59:02.8687109Z
---

# Task: f11-guard-side-impl

Review #0207 part 2 (guard-side ClaimedPid auto-refresh).

## What landed (commit d23eb9d on fix/identity-hijack-slice-a)

Per Charlie's brief + plan dydo/agents/Charlie/plan-f11-guard-side.md (read it):

1. WriteClaimedPid extraction in Services/AgentRegistry.cs (pure refactor; RefreshClaimedPid delegates).
2. AgentRegistry.RefreshResumedAgentSession(string?) — the 11-step pseudocode, whole body in try/catch, internal visibility. Steps 1-5 extracted into RecoveryClassifier.ShouldRefreshResumedPid; steps 6-11 in private RefreshResumedAgentSessionUnderLock (CC split, see decisions below).
3. Bounded-retry lock helper inlined (3x / 50 ms around TryAcquireLock).
4. Guard wiring: one call in Commands/GuardCommand.cs Execute, immediately after sessionId finalized, before Security Layer 1.
5. Companion change (Proof A): IsReclaimableStaleWorking predicate + ResumeInFlight in HandleExistingSession's stale-working branch.
6. Tests: 22 in new DynaDocs.Tests/Services/GuardResumeRefreshTests.cs + 1 in new DynaDocs.Tests/Commands/GuardResumeRefreshGuardLevelTests.cs + 1 in AutoResumeRearmWaitGateTests.cs (GuardRefreshThenWait_PassesF11Gate). Total 24 new tests covering all 24 plan items including Charlie's later additions (pid-reuse-skips-refresh, guard-refresh-on-resume-worktree, f2-corner-no-audit-after-saturate).

## Verification

- dotnet test (full suite via worktree runner): **4290 / 4290 PASS** (incl. all 24 new tests + existing AutoResumeRearmWaitGate + StaleWorkingReclaim + IdentityHijack tests).
- gap_check: **140 / 140 modules PASS** all tier requirements.
- Build clean: 0 warnings, 0 errors on touched files.
- **Live spike 1 (plain auto-resume): all six (a)-(f) confirmations PASS** with dispatched Emma:
  (a) .session.ClaimedPid refreshed from dead pre-resume PID 51168 -> live PID 18572;
  (b) _general-wait.json marker registered; role/task/status preserved;
  (c) state.md ResumeAttempts=0, LastResumeLaunchedAt=null, pre-resume-pid=null, launched-pid=null;
  (d) recovery_kind=auto Claim audit event present in .events sidecar;
  (e) watchdog.log resume_outcome=succeeded, reason=same_session_reclaim, attempts=1, elapsed=7s;
  (f) exactly 1 claude --resume launched.
- **Live spike 2 (worktree): could not complete live** — two attempts (Frank, Grace) both hit environmental issues unrelated to the fix:
  - Frank: F1-class .session-context bleed during worktree onboarding; tab confused itself as Dexter; never claimed.
  - Grace: claimed cleanly, then was force-cleaned mid-flight; the resumed claude rejected with 'No conversation found' (plan G2 case). My code correctly no-ops upstream of the failure.
  Unit test GuardRefreshOnResume_Worktree_WritesThroughJunctionAndEmitsToMainLog covers the worktree-specific code path my fix touches (FindMainDydoRoot routing of audit emit; .session writes through the junction). Recommend the reviewer or human run the live worktree spike from a clean terminal.
- **Live spike 3 (concurrent claim during warmup): unit-test covered, live not scriptable** — would need a separate claude session to issue the concurrent claim (Dexter's terminal already has Dexter claimed; ValidateClaimPreconditions short-circuits). Both legs of the companion change are pinned: ConcurrentClaimDuringWarmup_Refused + ConcurrentClaimAfterWarmup_Allowed.

## Plan deviations

ONE significant deviation: split RefreshResumedAgentSession into entry + UnderLock helper, AND extracted steps 1-5 into RecoveryClassifier.ShouldRefreshResumedPid. The plan said 'if gap_check flags it, extract the trigger predicate into RecoveryClassifier.ShouldRefreshResumedPid' — gap_check DID flag it (initial CC=42, CRAP=42.9 on RefreshResumedAgentSession). After the extraction it was still 30.4 (HandleExistingSession at CC=30 was now the hot method due to the companion change's added clause), so I also extracted IsReclaimableStaleWorking from HandleExistingSession. Final: gap_check 140/140. The plan-specified ShouldRefreshResumedPid extraction is in place.

The HandleExistingSession same-session reclaim branch (#0143/#0153) is KEPT as the plan specifies — both paths are reachable (explicit re-claim vs first guarded call), idempotent under the lock (Proof B). The Slice A KEPT items (#0207 part 1 launcher dydo wait deletion; #0208 IsValidAgentName) untouched.

## Files touched

- Commands/GuardCommand.cs (+9)
- Services/AgentRegistry.cs (+126; WriteClaimedPid extraction, RefreshResumedAgentSession entry, RefreshResumedAgentSessionUnderLock, ResumeInFlight, IsReclaimableStaleWorking, companion clause)
- Services/RecoveryClassifier.cs (+44; ShouldRefreshResumedPid + ResumedPidRefreshDecision record)
- DynaDocs.Tests/Services/GuardResumeRefreshTests.cs (NEW, 715 lines, 22 tests)
- DynaDocs.Tests/Commands/GuardResumeRefreshGuardLevelTests.cs (NEW, 148 lines, 1 test)
- DynaDocs.Tests/Services/AutoResumeRearmWaitGateTests.cs (+34; GuardRefreshThenWait_PassesF11Gate added)
- DynaDocs.csproj (1.4.8 -> 1.5.0, per the user mid-task)

## Notes for the reviewer

- The GuardRefreshThenWait_PassesF11Gate test asserts VerifyCallerOwnsAgent before/after refresh instead of invoking WaitCommand.Parse().Invoke() — WaitCommand has an unbounded while(!cancelled) poll loop that can't be cleanly cancelled from a unit test. The companion WaitWithoutClaudeAncestor_StaleClaimedPid_RefusedByF11Gate covers the refused side of the same predicate (kept unchanged).
- One real-world data point: my own claude tab crashed mid-task and was auto-resumed. After resume, dydo whoami/wait worked normally — exercising the post-fix flow end-to-end as a free bonus spike.

Pre-existing unaffected: #0208 IsValidAgentName test in identity-hijack-* tests stays green; F11 wait-DoS pinning test in AutoResumeRearmWaitGate stays green unchanged.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review #0207 part 2 (guard-side ClaimedPid auto-refresh).

## What landed (commit d23eb9d on fix/identity-hijack-slice-a)

Per Charlie's brief + plan dydo/agents/Charlie/plan-f11-guard-side.md (read it):

1. WriteClaimedPid extraction in Services/AgentRegistry.cs (pure refactor; RefreshClaimedPid delegates).
2. AgentRegistry.RefreshResumedAgentSession(string?) — the 11-step pseudocode, whole body in try/catch, internal visibility. Steps 1-5 extracted into RecoveryClassifier.ShouldRefreshResumedPid; steps 6-11 in private RefreshResumedAgentSessionUnderLock (CC split, see decisions below).
3. Bounded-retry lock helper inlined (3x / 50 ms around TryAcquireLock).
4. Guard wiring: one call in Commands/GuardCommand.cs Execute, immediately after sessionId finalized, before Security Layer 1.
5. Companion change (Proof A): IsReclaimableStaleWorking predicate + ResumeInFlight in HandleExistingSession's stale-working branch.
6. Tests: 22 in new DynaDocs.Tests/Services/GuardResumeRefreshTests.cs + 1 in new DynaDocs.Tests/Commands/GuardResumeRefreshGuardLevelTests.cs + 1 in AutoResumeRearmWaitGateTests.cs (GuardRefreshThenWait_PassesF11Gate). Total 24 new tests covering all 24 plan items including Charlie's later additions (pid-reuse-skips-refresh, guard-refresh-on-resume-worktree, f2-corner-no-audit-after-saturate).

## Verification

- dotnet test (full suite via worktree runner): **4290 / 4290 PASS** (incl. all 24 new tests + existing AutoResumeRearmWaitGate + StaleWorkingReclaim + IdentityHijack tests).
- gap_check: **140 / 140 modules PASS** all tier requirements.
- Build clean: 0 warnings, 0 errors on touched files.
- **Live spike 1 (plain auto-resume): all six (a)-(f) confirmations PASS** with dispatched Emma:
  (a) .session.ClaimedPid refreshed from dead pre-resume PID 51168 -> live PID 18572;
  (b) _general-wait.json marker registered; role/task/status preserved;
  (c) state.md ResumeAttempts=0, LastResumeLaunchedAt=null, pre-resume-pid=null, launched-pid=null;
  (d) recovery_kind=auto Claim audit event present in .events sidecar;
  (e) watchdog.log resume_outcome=succeeded, reason=same_session_reclaim, attempts=1, elapsed=7s;
  (f) exactly 1 claude --resume launched.
- **Live spike 2 (worktree): could not complete live** — two attempts (Frank, Grace) both hit environmental issues unrelated to the fix:
  - Frank: F1-class .session-context bleed during worktree onboarding; tab confused itself as Dexter; never claimed.
  - Grace: claimed cleanly, then was force-cleaned mid-flight; the resumed claude rejected with 'No conversation found' (plan G2 case). My code correctly no-ops upstream of the failure.
  Unit test GuardRefreshOnResume_Worktree_WritesThroughJunctionAndEmitsToMainLog covers the worktree-specific code path my fix touches (FindMainDydoRoot routing of audit emit; .session writes through the junction). Recommend the reviewer or human run the live worktree spike from a clean terminal.
- **Live spike 3 (concurrent claim during warmup): unit-test covered, live not scriptable** — would need a separate claude session to issue the concurrent claim (Dexter's terminal already has Dexter claimed; ValidateClaimPreconditions short-circuits). Both legs of the companion change are pinned: ConcurrentClaimDuringWarmup_Refused + ConcurrentClaimAfterWarmup_Allowed.

## Plan deviations

ONE significant deviation: split RefreshResumedAgentSession into entry + UnderLock helper, AND extracted steps 1-5 into RecoveryClassifier.ShouldRefreshResumedPid. The plan said 'if gap_check flags it, extract the trigger predicate into RecoveryClassifier.ShouldRefreshResumedPid' — gap_check DID flag it (initial CC=42, CRAP=42.9 on RefreshResumedAgentSession). After the extraction it was still 30.4 (HandleExistingSession at CC=30 was now the hot method due to the companion change's added clause), so I also extracted IsReclaimableStaleWorking from HandleExistingSession. Final: gap_check 140/140. The plan-specified ShouldRefreshResumedPid extraction is in place.

The HandleExistingSession same-session reclaim branch (#0143/#0153) is KEPT as the plan specifies — both paths are reachable (explicit re-claim vs first guarded call), idempotent under the lock (Proof B). The Slice A KEPT items (#0207 part 1 launcher dydo wait deletion; #0208 IsValidAgentName) untouched.

## Files touched

- Commands/GuardCommand.cs (+9)
- Services/AgentRegistry.cs (+126; WriteClaimedPid extraction, RefreshResumedAgentSession entry, RefreshResumedAgentSessionUnderLock, ResumeInFlight, IsReclaimableStaleWorking, companion clause)
- Services/RecoveryClassifier.cs (+44; ShouldRefreshResumedPid + ResumedPidRefreshDecision record)
- DynaDocs.Tests/Services/GuardResumeRefreshTests.cs (NEW, 715 lines, 22 tests)
- DynaDocs.Tests/Commands/GuardResumeRefreshGuardLevelTests.cs (NEW, 148 lines, 1 test)
- DynaDocs.Tests/Services/AutoResumeRearmWaitGateTests.cs (+34; GuardRefreshThenWait_PassesF11Gate added)
- DynaDocs.csproj (1.4.8 -> 1.5.0, per the user mid-task)

## Notes for the reviewer

- The GuardRefreshThenWait_PassesF11Gate test asserts VerifyCallerOwnsAgent before/after refresh instead of invoking WaitCommand.Parse().Invoke() — WaitCommand has an unbounded while(!cancelled) poll loop that can't be cleanly cancelled from a unit test. The companion WaitWithoutClaudeAncestor_StaleClaimedPid_RefusedByF11Gate covers the refused side of the same predicate (kept unchanged).
- One real-world data point: my own claude tab crashed mid-task and was auto-resumed. After resume, dydo whoami/wait worked normally — exercising the post-fix flow end-to-end as a free bonus spike.

Pre-existing unaffected: #0208 IsValidAgentName test in identity-hijack-* tests stays green; F11 wait-DoS pinning test in AutoResumeRearmWaitGate stays green unchanged.

## Code Review (2026-05-23 16:24)

- Reviewed by: Brian
- Result: FAILED
- Issues: FAIL: dydo check 13 errors (4 new, 9 pre-existing #0205 noise). Source-code work itself is exemplary — gap_check 140/140, 4290/4290 tests pass, plan executed faithfully with comprehensive edge-case coverage. The 4 new errors are in docs/inquisition files outside code-writer's writable paths — see dydo/agents/Brian/review-f11-guard-side-impl.md for the itemized list and recommendation. Minor non-blocker: dead AgentSession session parameter in RefreshResumedAgentSessionUnderLock.

Requires rework.

## Code Review

- Reviewed by: Emma
- Date: 2026-05-23 18:15
- Result: PASSED
- Notes: PASS. Re-review on working tree after Charlie's M1 fix and Frank's doc fixes (4 prior BLOCKERS). Gates: dotnet test 4290/4290, gap_check 140/140, dydo check 8 errors / 0 new (all pre-existing #0205 noise). M1 dead-param removal is clean; under-lock discipline preserved (fresh=GetSession at step 7 was the only session-state consumer, which is why the pre-lock snapshot was dead weight). Frank's two escape-boundary deviations verified against LinkExtractor.IsInsideInlineCode and CheckDocValidator agents/** exclusion — both sound. Full review: dydo/agents/Emma/review-f11-guard-side-impl.md.

Awaiting human approval.