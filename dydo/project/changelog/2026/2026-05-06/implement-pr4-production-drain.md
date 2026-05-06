---
area: general
type: changelog
date: 2026-05-06
---

# Task: implement-pr4-production-drain

Review PR4 of the runtime-regression batch — the final piece. Commit a3654be.

WHAT I IMPLEMENTED
1. Added Services/ProcessUtils.RunProcessCapture(fileName, arguments, workingDir, timeoutMs, environment, redirectStdin) returning (int ExitCode, string Stdout, string Stderr). Drains both pipes concurrently via ReadToEndAsync started before WaitForExit; kills process tree on timeout; returns -1 sentinel on start-failure or timeout. Optional env dictionary merges into psi.Environment (preserves parent env). Optional redirectStdin closes stdin immediately to signal EOF.

2. Migrated five production callers that redirected both streams but read only stdout:
   - Services/SnapshotService.cs:51-58  GetFullGitHead    (5s, workingDir)
   - Services/SnapshotService.cs:63-72  GetGitTrackedFiles (10s, workingDir)
   - Services/AuditService.cs:272-278   GetCurrentGitHead  (1s, no workingDir)
   - Services/FileCoverageService.cs:292-296 RunGit         (10s, workingDir)
   - Commands/InquisitionCommand.cs:165-177 HasChangesSince (5s, workingDir)

3. OQ3: routed Commands/WorktreeCommand.cs:619-643 RunProcessSilent through the helper. Preserves the load-bearing GIT_TERMINAL_PROMPT=0 env via a static GitNoPromptEnv dict and the stdin-EOF contract via redirectStdin: true. Translates the helper's -1 sentinel back to 1 to preserve the documented contract (FinalizeMerge's best-effort cleanup branches on this).

4. WatchdogService.cs intentionally NOT migrated — fire-and-forget shape, no WaitForExit on the parent side; buffer-pressure pattern does not apply. Documented in commit message.

5. Test-side fast-follow (Charlie's optional suggestion): DEFERRED. The three test helpers (SnapshotServiceTests.RunGit, InquisitionTests.RunGit, WorktreeMergeSafetyIntegrationTests.Git) already drain concurrently post-PR3. They throw on failure, use different timeouts, and one uses ArgumentList vs Arguments string — routing them through a tuple-returning helper would add throw-wrapper boilerplate at every site rather than reduce code. Plan also explicitly defers TestProcess.cs extraction. Respected that.

REGRESSION TESTS ADDED (+11)
- DynaDocs.Tests/Services/ProcessUtilsCaptureTests.cs (7 tests):
  RunProcessCapture_LargeOutput_DoesNotDeadlock,
  _NonZeroExit_ReturnsCapturedStderr,
  _Timeout_KillsProcessAndReturnsSentinel,
  _WorkingDirRespected,
  _EnvironmentInjected_PreservesParentEnv,
  _StartFailure_ReturnsSentinel,
  _RedirectStdin_DoesNotHangOnStdinReaders.
- DynaDocs.Tests/Commands/WorktreeCommandRunProcessSilentTests.cs (4 tests):
  RunProcessSilent_NoisyStderr_DoesNotDeadlock (Windows-only),
  _PropagatesExitCode_OnSuccess,
  _PropagatesExitCode_OnFailure,
  _StartFailure_ReturnsOne (pins the -1 -to- 1 sentinel translation).

VERIFICATION GATE
- dotnet build: clean (main + tests).
- python DynaDocs.Tests/coverage/run_tests.py: 4131 passed, 0 failed (was 4120 baseline + 11 new).
- python DynaDocs.Tests/coverage/gap_check.py --force-run: 140/140 modules clean, exit 0.
- Manual probe: dydo inquisition coverage --since 30 — exercises HasChangesSince through the new helper, output is sensible.
- HEAD pinned at 6d00b4c before verification kicked off; gap_check ran on a clean tree.

PLAN DEVIATIONS
- The plan's surfaced-surprise #5 recommended redirectStdin be default-true (belt-and-braces); I made it opt-in (default false) to keep behaviour strictly preserved for the four simple callers (SnapshotService/AuditService/FileCoverageService/InquisitionCommand). They did not redirect stdin before, and silently changing that adds a behavioural surface for the reviewer to second-guess. Only RunProcessSilent opts in (it requires the contract).
- The plan's verification step 4 mentions dydo audit replay byte-for-byte — that subcommand is not exposed in the CLI. Substituted dydo inquisition coverage --since 30 as the live-data probe. Reviewer: confirm this substitution is acceptable or point me at a real replay command.
- The env parameter type is IReadOnlyDictionary<string, string?>?; null value means remove key (safer than empty-string).

KEY DECISIONS
- -1 sentinel chosen for both start-failure and timeout (single failure code; callers that need a different exit-code contract — only RunProcessSilent today — translate at the call site).
- environment is merged into psi.Environment (which inherits parent) rather than overwriting; the EnvironmentInjected_PreservesParentEnv test pins this.
- Process.Kill(entireProcessTree: true) on timeout matches the aeee461 test-helper shape; documented in plan as the safer choice.

FOLLOW-UP CANDIDATES (not filed; surface to user)
- The dydo audit replay surface gap above — either expose a replay subcommand or document the verification step differently.
- TestProcess.cs extraction (deferred per plan); revisit if a fourth test-side git helper appears.

REPORT-BACK BATON
Per workflow, dispatching this reviewer fulfils my reply obligation to Adele on implement-pr4-production-drain. Reviewer: please report back to Adele on this task as the baton-holder.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review PR4 of the runtime-regression batch — the final piece. Commit a3654be.

WHAT I IMPLEMENTED
1. Added Services/ProcessUtils.RunProcessCapture(fileName, arguments, workingDir, timeoutMs, environment, redirectStdin) returning (int ExitCode, string Stdout, string Stderr). Drains both pipes concurrently via ReadToEndAsync started before WaitForExit; kills process tree on timeout; returns -1 sentinel on start-failure or timeout. Optional env dictionary merges into psi.Environment (preserves parent env). Optional redirectStdin closes stdin immediately to signal EOF.

2. Migrated five production callers that redirected both streams but read only stdout:
   - Services/SnapshotService.cs:51-58  GetFullGitHead    (5s, workingDir)
   - Services/SnapshotService.cs:63-72  GetGitTrackedFiles (10s, workingDir)
   - Services/AuditService.cs:272-278   GetCurrentGitHead  (1s, no workingDir)
   - Services/FileCoverageService.cs:292-296 RunGit         (10s, workingDir)
   - Commands/InquisitionCommand.cs:165-177 HasChangesSince (5s, workingDir)

3. OQ3: routed Commands/WorktreeCommand.cs:619-643 RunProcessSilent through the helper. Preserves the load-bearing GIT_TERMINAL_PROMPT=0 env via a static GitNoPromptEnv dict and the stdin-EOF contract via redirectStdin: true. Translates the helper's -1 sentinel back to 1 to preserve the documented contract (FinalizeMerge's best-effort cleanup branches on this).

4. WatchdogService.cs intentionally NOT migrated — fire-and-forget shape, no WaitForExit on the parent side; buffer-pressure pattern does not apply. Documented in commit message.

5. Test-side fast-follow (Charlie's optional suggestion): DEFERRED. The three test helpers (SnapshotServiceTests.RunGit, InquisitionTests.RunGit, WorktreeMergeSafetyIntegrationTests.Git) already drain concurrently post-PR3. They throw on failure, use different timeouts, and one uses ArgumentList vs Arguments string — routing them through a tuple-returning helper would add throw-wrapper boilerplate at every site rather than reduce code. Plan also explicitly defers TestProcess.cs extraction. Respected that.

REGRESSION TESTS ADDED (+11)
- DynaDocs.Tests/Services/ProcessUtilsCaptureTests.cs (7 tests):
  RunProcessCapture_LargeOutput_DoesNotDeadlock,
  _NonZeroExit_ReturnsCapturedStderr,
  _Timeout_KillsProcessAndReturnsSentinel,
  _WorkingDirRespected,
  _EnvironmentInjected_PreservesParentEnv,
  _StartFailure_ReturnsSentinel,
  _RedirectStdin_DoesNotHangOnStdinReaders.
- DynaDocs.Tests/Commands/WorktreeCommandRunProcessSilentTests.cs (4 tests):
  RunProcessSilent_NoisyStderr_DoesNotDeadlock (Windows-only),
  _PropagatesExitCode_OnSuccess,
  _PropagatesExitCode_OnFailure,
  _StartFailure_ReturnsOne (pins the -1 -to- 1 sentinel translation).

VERIFICATION GATE
- dotnet build: clean (main + tests).
- python DynaDocs.Tests/coverage/run_tests.py: 4131 passed, 0 failed (was 4120 baseline + 11 new).
- python DynaDocs.Tests/coverage/gap_check.py --force-run: 140/140 modules clean, exit 0.
- Manual probe: dydo inquisition coverage --since 30 — exercises HasChangesSince through the new helper, output is sensible.
- HEAD pinned at 6d00b4c before verification kicked off; gap_check ran on a clean tree.

PLAN DEVIATIONS
- The plan's surfaced-surprise #5 recommended redirectStdin be default-true (belt-and-braces); I made it opt-in (default false) to keep behaviour strictly preserved for the four simple callers (SnapshotService/AuditService/FileCoverageService/InquisitionCommand). They did not redirect stdin before, and silently changing that adds a behavioural surface for the reviewer to second-guess. Only RunProcessSilent opts in (it requires the contract).
- The plan's verification step 4 mentions dydo audit replay byte-for-byte — that subcommand is not exposed in the CLI. Substituted dydo inquisition coverage --since 30 as the live-data probe. Reviewer: confirm this substitution is acceptable or point me at a real replay command.
- The env parameter type is IReadOnlyDictionary<string, string?>?; null value means remove key (safer than empty-string).

KEY DECISIONS
- -1 sentinel chosen for both start-failure and timeout (single failure code; callers that need a different exit-code contract — only RunProcessSilent today — translate at the call site).
- environment is merged into psi.Environment (which inherits parent) rather than overwriting; the EnvironmentInjected_PreservesParentEnv test pins this.
- Process.Kill(entireProcessTree: true) on timeout matches the aeee461 test-helper shape; documented in plan as the safer choice.

FOLLOW-UP CANDIDATES (not filed; surface to user)
- The dydo audit replay surface gap above — either expose a replay subcommand or document the verification step differently.
- TestProcess.cs extraction (deferred per plan); revisit if a fourth test-side git helper appears.

REPORT-BACK BATON
Per workflow, dispatching this reviewer fulfils my reply obligation to Adele on implement-pr4-production-drain. Reviewer: please report back to Adele on this task as the baton-holder.

## Code Review (2026-05-06 16:54)

- Reviewed by: Frank
- Result: FAILED
- Issues: RunProcessCapture_RedirectStdin_DoesNotHangOnStdinReaders (ProcessUtilsCaptureTests.cs:111) does not verify what its name+comment claim. 'git rev-parse --is-inside-work-tree' doesn't read stdin, so the test passes whether or not the helper closes stdin. Use a stdin-reading command (git update-ref --stdin / git hash-object --stdin / git diff-tree --stdin) so the helper's process.StandardInput.Close() is genuinely load-bearing for the test passing. Everything else PASS: helper design clean, 5 service migrations faithful, OQ3 RunProcessSilent preserves both contracts and -1->1 sentinel, build clean, 4131/4131 tests, gap_check 140/140.

Requires rework.

## Code Review

- Reviewed by: Dexter
- Date: 2026-05-06 17:45
- Result: PASSED
- Notes: PASS. Test fix correctly addresses Frank's concern: switched from 'git rev-parse --is-inside-work-tree' (doesn't read stdin) to 'git hash-object --stdin' (reads bytes until EOF, prints SHA-1), making the close-stdin call load-bearing. EmptyBlobSha1 hash assertion guards against trivial pass via early process exit; timing assertion (<3s) guards against the timeout case. Comment rewritten to describe the actual mechanism. Verified gates on 4751aeb: run_tests.py 4131/4131 pass (4m15s); gap_check.py --force-run 140/140 modules (100%); dydo check errors all pre-existing markdown drift (commit only touches DynaDocs.Tests/Services/ProcessUtilsCaptureTests.cs, +8/-4). Brian's regression-check proof (neutering process.StandardInput.Close() locally → test fails with -1 timeout; restoring → passes in ~370ms) is solid evidence of the contract being exercised.

Awaiting human approval.

## Approval

- Approved: 2026-05-06 17:47
