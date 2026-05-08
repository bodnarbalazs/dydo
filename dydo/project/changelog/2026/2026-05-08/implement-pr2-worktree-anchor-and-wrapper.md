---
area: general
type: changelog
date: 2026-05-08
---

# Task: implement-pr2-worktree-anchor-and-wrapper

Review PR2 of agent-crash-fixes batch (commit de50134, CI run 25504955467 green).

SCOPE
1. Finding #2 / #0174 — claim-time watchdog anchor written into the worktree's dydo dir instead of main. Root cause: AgentRegistry.cs:417 resolved its own dydo root via _configService.GetDydoRoot(_basePath), which returns the worktree's dydo when the basepath is inside one. Watchdog only reads main's anchors dir, so worktree-claimed leaf agents were invisible to it.
2. Finding #4 / #0175 — TerminalLauncher.LaunchResume / WindowsTerminalLauncher.GetResumeArguments / LinuxTerminalLauncher.BuildResumeBashCommand / MacTerminalLauncher.BuildResumeShellComponents had no worktree wrapper symmetry with the dispatch path. A resumed claude in a worktree never recreated junctions, never ran init-settings, and never ran "dydo worktree cleanup" on release — so the worktree dir lingered.

CHANGES
- Services/WatchdogService.cs (RegisterMainAnchor) — new public RegisterMainAnchor(int? anchorPid, string? startPath = null). Single-source helper that resolves to PathUtils.FindMainDydoRoot(startPath) and routes through RegisterAnchor. The optional startPath seed lets callers point at their basepath so test fixtures with synthetic project roots resolve correctly.
- Services/AgentRegistry.cs (claim-time anchor block) — claim-time anchor write now calls RegisterMainAnchor(FindClaudeAncestor(), _basePath). Six lines saved; the dydoRoot null check folded into the helper.
- Services/WatchdogService.cs (LaunchResumeOverride) — signature widened from (string,string,string?,string?,bool) to (string,string,string?,string?,bool,string?,string?) to carry worktreeId + mainProjectRoot. All 25 test sites in WatchdogServiceTests.cs updated.
- Services/WatchdogService.cs (PollAndResumeForAgent + ResolveResumeWorktreeId) — reads .worktree from agentDir via the new ResolveResumeWorktreeId helper and passes worktreeId + projectRoot (already main) through to LaunchResumeOverride / LaunchResumeTerminal.
- Services/TerminalLauncher.cs — LaunchResumeTerminal / LaunchResume / GetWindowsResumeArguments / GetLinuxResumeArguments / GetMacResumeArguments all take optional worktreeId + mainProjectRoot pair.
- Services/WindowsTerminalLauncher.cs (GetResumeArguments) — wraps the resume body in 'Set-Location {wtDir}; junctions; init-settings; Start-Sleep; try { resume } finally { Set-Location {root}; cleanup --agent name }' when both worktreeId + mainProjectRoot are set; passes them to wt + powershell fallback.
- Services/LinuxTerminalLauncher.cs (BuildResumeBashCommand) — prepends WorktreeSetupScript and appends WorktreeCleanupScript before 'exec bash' when worktree context is present. TryLaunchResume + GetResumeArguments take the new pair.
- Services/MacTerminalLauncher.cs (BuildResumeShellComponents) — adds wtSetup + wtCleanup symmetrical with BuildShellComponents.

TESTS (delta +12; baseline 4153 -> 4165 passing)
- DynaDocs.Tests/Services/PathUtilsWorktreeIsolationTests.cs (+2 tests):
  - RegisterMainAnchor_FromInsideWorktree_WritesToMainAnchorsDir — #0174 regression.
  - RegisterMainAnchor_FromMainProject_WritesToMainAnchorsDir — control.
- DynaDocs.Tests/Services/TerminalLauncherTests.cs (+8 tests):
  - GetWindowsResumeArguments_WithWorktree_IncludesSetupAndCleanup
  - GetWindowsResumeArguments_WithoutWorktree_OmitsWrapper
  - GetLinuxResumeArguments_WithWorktree_IncludesSetupAndCleanup
  - GetLinuxResumeArguments_WithoutWorktree_OmitsWrapper
  - GetMacResumeArguments_WithWorktree_IncludesSetupAndCleanup
  - BuildResumeBashCommand_WithWorktree_UsesForwardSlashes_NotPlatformSeparator (cross-platform path-handling — #0175 brief asked specifically for at least one test that doesn't use literal backslash).
  - TryLaunchResume_WithWorktree_PassesWrapperToTerminal
  - WindowsLaunchResume_WithWorktree_EmbedsWrapperInWtArguments
- DynaDocs.Tests/Services/WatchdogServiceTests.cs (+2 tests):
  - PollAndResumeForAgent_WithWorktreeMarker_PassesWorktreeContextToLaunchResume — verifies watchdog -> launcher data flow.
  - PollAndResumeForAgent_NoWorktreeMarker_PassesNullWorktreeId — symmetry guard so non-worktree resumes don't accidentally invoke the wrapper.

VERIFICATION
- dotnet build clean (0 warnings, 0 errors).
- run_tests.py: 4165/4165 passed (4 m 26 s).
- gap_check.py --force-run: 140/140 modules at tier (exit 0).
- Linux CI: gh run 25504955467 — completed/success.

PLAN DEVIATIONS (1)
- The plan recommended adding WatchdogService.GetMainAnchorsDirPath() as the central helper. I instead added RegisterMainAnchor(int?, string?) — same intent (single source of truth, enforced by construction) but at the right abstraction level: both callsites want "register an anchor in main", not "compute the anchors dir path." The helper also takes an optional startPath so the existing AgentRegistryTests.ClaimAgent_FreshClaim_RegistersAnchorWithClaudeAncestor test (which constructs AgentRegistry against a synthetic _testDir) keeps passing — the test's basepath now seeds FindMainDydoRoot's worktree-walkback search instead of the host process CWD. I think this is materially better than the plan's GetMainAnchorsDirPath alternative; flag if you disagree.

PR3 DEFERRED — no resume_outcome event, no recovery_kind on Claim, no resume_predecessor_session. PR1 surface left alone (LaunchedPid, IsBadSessionFailFast, ProcessUtils.Ancestry regex, ResumeWarmupGate value).

REVIEW BRIEF
Confirm the anchor-write site routes through RegisterMainAnchor and resolves to the main dydo root regardless of basepath. Confirm the resume-launch path recreates junctions and runs cleanup on all three platforms. Verify the LaunchResumeOverride signature change covers every call site. Spot-check the cross-platform path-handling assertions in TerminalLauncherTests. After approval, baton-pass back to Adele on this same task.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review PR2 of agent-crash-fixes batch (commit de50134, CI run 25504955467 green).

SCOPE
1. Finding #2 / #0174 — claim-time watchdog anchor written into the worktree's dydo dir instead of main. Root cause: AgentRegistry.cs:417 resolved its own dydo root via _configService.GetDydoRoot(_basePath), which returns the worktree's dydo when the basepath is inside one. Watchdog only reads main's anchors dir, so worktree-claimed leaf agents were invisible to it.
2. Finding #4 / #0175 — TerminalLauncher.LaunchResume / WindowsTerminalLauncher.GetResumeArguments / LinuxTerminalLauncher.BuildResumeBashCommand / MacTerminalLauncher.BuildResumeShellComponents had no worktree wrapper symmetry with the dispatch path. A resumed claude in a worktree never recreated junctions, never ran init-settings, and never ran "dydo worktree cleanup" on release — so the worktree dir lingered.

CHANGES
- Services/WatchdogService.cs (RegisterMainAnchor) — new public RegisterMainAnchor(int? anchorPid, string? startPath = null). Single-source helper that resolves to PathUtils.FindMainDydoRoot(startPath) and routes through RegisterAnchor. The optional startPath seed lets callers point at their basepath so test fixtures with synthetic project roots resolve correctly.
- Services/AgentRegistry.cs (claim-time anchor block) — claim-time anchor write now calls RegisterMainAnchor(FindClaudeAncestor(), _basePath). Six lines saved; the dydoRoot null check folded into the helper.
- Services/WatchdogService.cs (LaunchResumeOverride) — signature widened from (string,string,string?,string?,bool) to (string,string,string?,string?,bool,string?,string?) to carry worktreeId + mainProjectRoot. All 25 test sites in WatchdogServiceTests.cs updated.
- Services/WatchdogService.cs (PollAndResumeForAgent + ResolveResumeWorktreeId) — reads .worktree from agentDir via the new ResolveResumeWorktreeId helper and passes worktreeId + projectRoot (already main) through to LaunchResumeOverride / LaunchResumeTerminal.
- Services/TerminalLauncher.cs — LaunchResumeTerminal / LaunchResume / GetWindowsResumeArguments / GetLinuxResumeArguments / GetMacResumeArguments all take optional worktreeId + mainProjectRoot pair.
- Services/WindowsTerminalLauncher.cs (GetResumeArguments) — wraps the resume body in 'Set-Location {wtDir}; junctions; init-settings; Start-Sleep; try { resume } finally { Set-Location {root}; cleanup --agent name }' when both worktreeId + mainProjectRoot are set; passes them to wt + powershell fallback.
- Services/LinuxTerminalLauncher.cs (BuildResumeBashCommand) — prepends WorktreeSetupScript and appends WorktreeCleanupScript before 'exec bash' when worktree context is present. TryLaunchResume + GetResumeArguments take the new pair.
- Services/MacTerminalLauncher.cs (BuildResumeShellComponents) — adds wtSetup + wtCleanup symmetrical with BuildShellComponents.

TESTS (delta +12; baseline 4153 -> 4165 passing)
- DynaDocs.Tests/Services/PathUtilsWorktreeIsolationTests.cs (+2 tests):
  - RegisterMainAnchor_FromInsideWorktree_WritesToMainAnchorsDir — #0174 regression.
  - RegisterMainAnchor_FromMainProject_WritesToMainAnchorsDir — control.
- DynaDocs.Tests/Services/TerminalLauncherTests.cs (+8 tests):
  - GetWindowsResumeArguments_WithWorktree_IncludesSetupAndCleanup
  - GetWindowsResumeArguments_WithoutWorktree_OmitsWrapper
  - GetLinuxResumeArguments_WithWorktree_IncludesSetupAndCleanup
  - GetLinuxResumeArguments_WithoutWorktree_OmitsWrapper
  - GetMacResumeArguments_WithWorktree_IncludesSetupAndCleanup
  - BuildResumeBashCommand_WithWorktree_UsesForwardSlashes_NotPlatformSeparator (cross-platform path-handling — #0175 brief asked specifically for at least one test that doesn't use literal backslash).
  - TryLaunchResume_WithWorktree_PassesWrapperToTerminal
  - WindowsLaunchResume_WithWorktree_EmbedsWrapperInWtArguments
- DynaDocs.Tests/Services/WatchdogServiceTests.cs (+2 tests):
  - PollAndResumeForAgent_WithWorktreeMarker_PassesWorktreeContextToLaunchResume — verifies watchdog -> launcher data flow.
  - PollAndResumeForAgent_NoWorktreeMarker_PassesNullWorktreeId — symmetry guard so non-worktree resumes don't accidentally invoke the wrapper.

VERIFICATION
- dotnet build clean (0 warnings, 0 errors).
- run_tests.py: 4165/4165 passed (4 m 26 s).
- gap_check.py --force-run: 140/140 modules at tier (exit 0).
- Linux CI: gh run 25504955467 — completed/success.

PLAN DEVIATIONS (1)
- The plan recommended adding WatchdogService.GetMainAnchorsDirPath() as the central helper. I instead added RegisterMainAnchor(int?, string?) — same intent (single source of truth, enforced by construction) but at the right abstraction level: both callsites want "register an anchor in main", not "compute the anchors dir path." The helper also takes an optional startPath so the existing AgentRegistryTests.ClaimAgent_FreshClaim_RegistersAnchorWithClaudeAncestor test (which constructs AgentRegistry against a synthetic _testDir) keeps passing — the test's basepath now seeds FindMainDydoRoot's worktree-walkback search instead of the host process CWD. I think this is materially better than the plan's GetMainAnchorsDirPath alternative; flag if you disagree.

PR3 DEFERRED — no resume_outcome event, no recovery_kind on Claim, no resume_predecessor_session. PR1 surface left alone (LaunchedPid, IsBadSessionFailFast, ProcessUtils.Ancestry regex, ResumeWarmupGate value).

REVIEW BRIEF
Confirm the anchor-write site routes through RegisterMainAnchor and resolves to the main dydo root regardless of basepath. Confirm the resume-launch path recreates junctions and runs cleanup on all three platforms. Verify the LaunchResumeOverride signature change covers every call site. Spot-check the cross-platform path-handling assertions in TerminalLauncherTests. After approval, baton-pass back to Adele on this same task.

## Approval

- Approved: 2026-05-08 12:36
