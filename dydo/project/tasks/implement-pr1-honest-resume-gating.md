---
area: general
name: implement-pr1-honest-resume-gating
status: human-reviewed
created: 2026-05-07T11:49:37.1749874Z
assigned: Brian
updated: 2026-05-07T13:46:34.8898058Z
---

# Task: implement-pr1-honest-resume-gating

Review PR1 of agent-crash-fixes (commit e80730c). Closes #0173 + augments #0151.

CHANGES (production):
- Models/AgentState.cs:73-82 — new LaunchedPid field, mirrors PreResumePid.
- Services/AgentRegistry.cs — launched-pid round-trips through state.md; RecordResumeLaunch helper persists it post-launch without bumping the resume counter; reset paths (claim, release, same-session reclaim) clear it.
- Services/IAgentRegistry.cs — IncrementResumeAttempts gains optional launchedPid param; RecordResumeLaunch added to interface.
- Services/WatchdogService.cs — ResumeWarmupGate bumped 60s -> 5min (#0173 audit showed 8/32/10-min rehydrations); IsBadSessionFailFast now also requires launched-PID dead; new IsLaunchedClaudeStillAlive silent-skips when warmup elapsed but launched PID alive (no log, no relaunch — per locked Q3 silent-skip decision); ResumeContext / ParseResumeFields plumb launched-pid; KillClaudeProcesses now routes through MatchesProcessName (dropped dead ClaudeProcessNames HashSet).
- Services/ProcessUtils.Ancestry.cs — anchored regex per needle: ^claude(\.exe(\.old\.\d+)?)?$ + ^node(\.exe)?$ — other needles keep literal-stem path.

VERIFICATION:
- dotnet build clean.
- run_tests.py: 4153/4153 passed (was ~4131 baseline; +22 new tests).
- gap_check.py --force-run: 140/140 modules at tier (100%).

LOCKED DECISIONS (from inbox brief, applied as-is):
- Q1 PR-split: PR1 = #1+#5+log-honesty silent-skip+#3 whitelist (NOT just #1+#3).
- Q2 launched-pid persistence: option (a) — AgentState field, mirrors PreResumePid via ParseResumeFields.
- Q3 log honesty: silent-skip — keep resume_blocked event name, only emit on liveness=dead. Alive past warmup = no log line.
- Q4 5-min gate: kept (belt-and-braces; not load-bearing once #5 ships).
- Q7 SaturateResumeAttempts: kept (correct hammer now; alternative re-introduces #0152 race).

OUT OF SCOPE: PR2 (#2 worktree anchor + #4 LaunchResume worktree wrapper) and PR3 (instrumentation events, Q3-(ii) resume_pending) are deferred per brief.

REGRESSION TESTS ADDED:
- ProcessUtilsTests.MatchesProcessName — claude.exe.old.<ts>, anchored boundaries (no prefix/suffix bypass; node-gyp negative).
- AgentRegistryTests.IncrementResumeAttempts_PersistsLaunchedPid_RoundTrips, RecordResumeLaunch_PersistsLaunchedPid_WithoutBumpingCounter, ResetResumeBookkeeping_ClearsLaunchedPid_OnSameSessionReclaim.
- WatchdogServiceTests.PollAndResumeForAgent_LaunchedPidAlive_PastWarmup_DoesNotEmitResumeBlocked (the regression test for balazs's 'feels broken' bug), ..._LaunchedPidDead_PastWarmup_EmitsResumeBlocked, ..._LegacyState_NoLaunchedPid_PreservesPreFixBehavior, ..._LaunchSucceeds_PersistsLaunchedPidToState, ..._LaunchFailed_LeavesLaunchedPidNull, PollAndCleanup_KillsPostUpdateRenamedClaude.

PLAN: dydo/agents/Brian/archive/20260507-110855/plan-agent-crash-fixes.md (PR1 section).

Please verify (a) the plan-vs-implementation mapping is faithful, (b) the silent-skip decision is implemented correctly (alive launched_pid past warmup → no log emission), (c) the legacy-state fallthrough preserves pre-fix behaviour for state.md without launched-pid, (d) no PR2/PR3 work has leaked in, (e) the dropped ClaudeProcessNames HashSet has no remaining production callers.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review PR1 of agent-crash-fixes (commit e80730c). Closes #0173 + augments #0151.

CHANGES (production):
- Models/AgentState.cs:73-82 — new LaunchedPid field, mirrors PreResumePid.
- Services/AgentRegistry.cs — launched-pid round-trips through state.md; RecordResumeLaunch helper persists it post-launch without bumping the resume counter; reset paths (claim, release, same-session reclaim) clear it.
- Services/IAgentRegistry.cs — IncrementResumeAttempts gains optional launchedPid param; RecordResumeLaunch added to interface.
- Services/WatchdogService.cs — ResumeWarmupGate bumped 60s -> 5min (#0173 audit showed 8/32/10-min rehydrations); IsBadSessionFailFast now also requires launched-PID dead; new IsLaunchedClaudeStillAlive silent-skips when warmup elapsed but launched PID alive (no log, no relaunch — per locked Q3 silent-skip decision); ResumeContext / ParseResumeFields plumb launched-pid; KillClaudeProcesses now routes through MatchesProcessName (dropped dead ClaudeProcessNames HashSet).
- Services/ProcessUtils.Ancestry.cs — anchored regex per needle: ^claude(\.exe(\.old\.\d+)?)?$ + ^node(\.exe)?$ — other needles keep literal-stem path.

VERIFICATION:
- dotnet build clean.
- run_tests.py: 4153/4153 passed (was ~4131 baseline; +22 new tests).
- gap_check.py --force-run: 140/140 modules at tier (100%).

LOCKED DECISIONS (from inbox brief, applied as-is):
- Q1 PR-split: PR1 = #1+#5+log-honesty silent-skip+#3 whitelist (NOT just #1+#3).
- Q2 launched-pid persistence: option (a) — AgentState field, mirrors PreResumePid via ParseResumeFields.
- Q3 log honesty: silent-skip — keep resume_blocked event name, only emit on liveness=dead. Alive past warmup = no log line.
- Q4 5-min gate: kept (belt-and-braces; not load-bearing once #5 ships).
- Q7 SaturateResumeAttempts: kept (correct hammer now; alternative re-introduces #0152 race).

OUT OF SCOPE: PR2 (#2 worktree anchor + #4 LaunchResume worktree wrapper) and PR3 (instrumentation events, Q3-(ii) resume_pending) are deferred per brief.

REGRESSION TESTS ADDED:
- ProcessUtilsTests.MatchesProcessName — claude.exe.old.<ts>, anchored boundaries (no prefix/suffix bypass; node-gyp negative).
- AgentRegistryTests.IncrementResumeAttempts_PersistsLaunchedPid_RoundTrips, RecordResumeLaunch_PersistsLaunchedPid_WithoutBumpingCounter, ResetResumeBookkeeping_ClearsLaunchedPid_OnSameSessionReclaim.
- WatchdogServiceTests.PollAndResumeForAgent_LaunchedPidAlive_PastWarmup_DoesNotEmitResumeBlocked (the regression test for balazs's 'feels broken' bug), ..._LaunchedPidDead_PastWarmup_EmitsResumeBlocked, ..._LegacyState_NoLaunchedPid_PreservesPreFixBehavior, ..._LaunchSucceeds_PersistsLaunchedPidToState, ..._LaunchFailed_LeavesLaunchedPidNull, PollAndCleanup_KillsPostUpdateRenamedClaude.

PLAN: dydo/agents/Brian/archive/20260507-110855/plan-agent-crash-fixes.md (PR1 section).

Please verify (a) the plan-vs-implementation mapping is faithful, (b) the silent-skip decision is implemented correctly (alive launched_pid past warmup → no log emission), (c) the legacy-state fallthrough preserves pre-fix behaviour for state.md without launched-pid, (d) no PR2/PR3 work has leaked in, (e) the dropped ClaudeProcessNames HashSet has no remaining production callers.

## Code Review

- Reviewed by: Dexter
- Date: 2026-05-07 14:23
- Result: PASSED
- Notes: PASS. PR1 (e80730c) faithful to plan and behaviour-correct on all five verification points.

PLAN MAPPING (a): all listed file:line changes present and exactly as specified — AgentState.LaunchedPid (mirrors PreResumePid w/ #0173 cross-ref), AgentRegistry round-trip + RecordResumeLaunch + reset on claim/release/same-session-reclaim, IAgentRegistry signature additions, WatchdogService gate=5min + IsBadSessionFailFast launched-PID-dead requirement + IsLaunchedClaudeStillAlive silent-skip + ResumeContext/ParseResumeFields plumbing, ProcessUtils.Ancestry per-needle anchored regex with literal-stem fallthrough.

SILENT-SKIP (b): IsLaunchedClaudeStillAlive (WatchdogService.cs:572-576) is checked at line 465-466 BEFORE IncrementResumeAttempts and BEFORE the LogResume emission, so an alive launched_pid past warmup produces no log line, no relaunch, no counter bump — exactly the locked Q3 decision. The two predicates are mutually exclusive on the LaunchedPid liveness clause, so ordering is safe.

LEGACY FALLTHROUGH (c): IsBadSessionFailFast clause '(!ctx.LaunchedPid.HasValue || !IsProcessRunning(ctx.LaunchedPid.Value))' short-circuits to true on null LaunchedPid, preserving the original wall-clock-only predicate for state.md without launched-pid. IsLaunchedClaudeStillAlive requires LaunchedPid.HasValue, so legacy state cannot trigger the silent-skip path. Test PollAndResumeForAgent_LegacyState_NoLaunchedPid_PreservesPreFixBehavior pins this.

NO PR2/PR3 LEAK (d): grep for resume_outcome/RecoveryKind/ResumePredecessorSession/GetMainAnchorsDirPath/WorktreeResumeWrapper/BuildResumeBody/resume_pending across *.cs returns no production hits. AgentRegistry's anchor registration still uses GetDydoRoot (not FindMainDydoRoot); TerminalLauncher / WindowsTerminalLauncher / LinuxTerminalLauncher unchanged.

CLAUDEPROCESSNAMES REMOVAL (e): grep across *.cs confirms zero remaining production callers. The only remaining references are historical docs (project/issues, project/changelog, project/inquisitions). KillClaudeProcesses now routes through MatchesProcessName(claude) || MatchesProcessName(node) — fail-closed property preserved by the anchored regexes.

GATES:
- dydo check: 0 errors, 4 pre-existing orphan-doc warnings (not introduced by this commit)
- run_tests.py: 4153/4153 passed
- gap_check.py --force-run: 140/140 modules at tier (100%)

Code is clean, tests are meaningful (regression test for balazs's 'feels broken' bug is named clearly and pins the exact failure mode), no AI slop, anti-slop mandate respected. Ship PR1.

Awaiting human approval.