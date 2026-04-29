---
area: general
type: changelog
date: 2026-04-29
---

# Task: fix-watchdog-deaths-critical

Review commit 06512de (fix-watchdog-deaths-critical) against Emma's plan at dydo/agents/Emma/archive/20260428-204956/plan-watchdog-deaths-critical.md and the agent-deaths inquisition (dydo/project/inquisitions/agent-deaths.md, findings #0121 + #0122).

What was implemented:

1. Services/AgentRegistry.cs — extracted lock acquire/release into internal static helpers
   (TryAcquireLockAtPath / ReleaseLockAtPath) that take a lockPath parameter. Existing
   private instance TryAcquireLock/ReleaseLock are now one-line wrappers. Body diff is
   char-faithful with the original lines 1981-2050 / 2055-2079 — only intentional change
   is GetLockFilePath(agentName) -> lockPath parameter. Original error messages preserved
   verbatim by carrying agentName as a second parameter (existing AgentRegistryTests
   assert on 'claim in progress' substring; preserving the messages keeps them green).
   Plan code skeleton dropped agentName from messages; Emma overrode that to honor the
   brief's 'char-faithful' rule.

2. Services/WatchdogService.cs — added ClaudeProcessNames whitelist HashSet {claude,node}
   immediately after ShellProcessNames (which is left untouched per plan). PollAndCleanup
   now acquires the per-agent .claim.lock for each iteration via AgentRegistry.TryAcquireLockAtPath;
   skip-and-retry on contention; release in finally{}. Kill loop replaced with whitelist:
   continue unless procName is in ClaudeProcessNames (case-insensitive).

3. DynaDocs.Tests/Services/WatchdogServiceTests.cs — 4 new tests + 1 [Theory] over 9
   emulator names = 12 new test instances:
     - PollAndCleanup_LockHeldByWriter_DoesNotKill (#0121 regression: external lock held
       -> no kill, no auto-close clear; release -> next poll proceeds)
     - PollAndCleanup_LinuxTerminalEmulatorPid_NotKilled (#0122 regression: 9 emulators
       gnome-terminal, konsole, xfce4-terminal, alacritty, kitty, wezterm, tilix, foot, xterm)
     - PollAndCleanup_ClaudeProcess_Killed (whitelist positive)
     - PollAndCleanup_ReleasesLockAfterWork (finally-block guard)
   Plus 6 mechanical override-edits to existing tests (plan called out 4; 2 dead-PID
   tests — DeadPid_ClearsAutoClose and FirstPoll_ProcessesRunning_ClearsImmediately —
   also needed GetProcessNameOverride='claude' to keep exercising the kill-and-throw
   path under the whitelist contract).

Plan deviations:
  - TryAcquireLockAtPath signature has an extra agentName parameter (plan code skeleton
    omitted it). Kept to preserve existing 'Could not acquire claim lock for agent X'
    and 'Agent X claim in progress' messages — substring-asserted by AgentRegistryTests.
    The brief's 'char-faithful' rule wins over the plan's code skeleton on this point.
  - 6 mechanical test edits instead of 4 (plan listed 4; the 2 dead-PID tests also
    needed updating because under the whitelist a null procName now skips instead of
    killing — adding the override keeps the throw path covered).

Tests: dotnet build clean, dotnet test 3883/3883 pass (one pre-existing flake on
PathUtilsTests.IsInsideWorktree_NullPath_ChecksCwd — CWD race with parallel test
classes — reproduced once, vanished on re-run; unrelated to this change).
gap_check.py exit 0; 136/136 modules pass tier requirements.

Out of scope (per brief): #0123-#0132 stay filed. ShellProcessNames untouched.
Atomic WriteStateFile not included.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit 06512de (fix-watchdog-deaths-critical) against Emma's plan at dydo/agents/Emma/archive/20260428-204956/plan-watchdog-deaths-critical.md and the agent-deaths inquisition (dydo/project/inquisitions/agent-deaths.md, findings #0121 + #0122).

What was implemented:

1. Services/AgentRegistry.cs — extracted lock acquire/release into internal static helpers
   (TryAcquireLockAtPath / ReleaseLockAtPath) that take a lockPath parameter. Existing
   private instance TryAcquireLock/ReleaseLock are now one-line wrappers. Body diff is
   char-faithful with the original lines 1981-2050 / 2055-2079 — only intentional change
   is GetLockFilePath(agentName) -> lockPath parameter. Original error messages preserved
   verbatim by carrying agentName as a second parameter (existing AgentRegistryTests
   assert on 'claim in progress' substring; preserving the messages keeps them green).
   Plan code skeleton dropped agentName from messages; Emma overrode that to honor the
   brief's 'char-faithful' rule.

2. Services/WatchdogService.cs — added ClaudeProcessNames whitelist HashSet {claude,node}
   immediately after ShellProcessNames (which is left untouched per plan). PollAndCleanup
   now acquires the per-agent .claim.lock for each iteration via AgentRegistry.TryAcquireLockAtPath;
   skip-and-retry on contention; release in finally{}. Kill loop replaced with whitelist:
   continue unless procName is in ClaudeProcessNames (case-insensitive).

3. DynaDocs.Tests/Services/WatchdogServiceTests.cs — 4 new tests + 1 [Theory] over 9
   emulator names = 12 new test instances:
     - PollAndCleanup_LockHeldByWriter_DoesNotKill (#0121 regression: external lock held
       -> no kill, no auto-close clear; release -> next poll proceeds)
     - PollAndCleanup_LinuxTerminalEmulatorPid_NotKilled (#0122 regression: 9 emulators
       gnome-terminal, konsole, xfce4-terminal, alacritty, kitty, wezterm, tilix, foot, xterm)
     - PollAndCleanup_ClaudeProcess_Killed (whitelist positive)
     - PollAndCleanup_ReleasesLockAfterWork (finally-block guard)
   Plus 6 mechanical override-edits to existing tests (plan called out 4; 2 dead-PID
   tests — DeadPid_ClearsAutoClose and FirstPoll_ProcessesRunning_ClearsImmediately —
   also needed GetProcessNameOverride='claude' to keep exercising the kill-and-throw
   path under the whitelist contract).

Plan deviations:
  - TryAcquireLockAtPath signature has an extra agentName parameter (plan code skeleton
    omitted it). Kept to preserve existing 'Could not acquire claim lock for agent X'
    and 'Agent X claim in progress' messages — substring-asserted by AgentRegistryTests.
    The brief's 'char-faithful' rule wins over the plan's code skeleton on this point.
  - 6 mechanical test edits instead of 4 (plan listed 4; the 2 dead-PID tests also
    needed updating because under the whitelist a null procName now skips instead of
    killing — adding the override keeps the throw path covered).

Tests: dotnet build clean, dotnet test 3883/3883 pass (one pre-existing flake on
PathUtilsTests.IsInsideWorktree_NullPath_ChecksCwd — CWD race with parallel test
classes — reproduced once, vanished on re-run; unrelated to this change).
gap_check.py exit 0; 136/136 modules pass tier requirements.

Out of scope (per brief): #0123-#0132 stay filed. ShellProcessNames untouched.
Atomic WriteStateFile not included.

## Code Review

- Reviewed by: Noah
- Date: 2026-04-28 21:28
- Result: PASSED
- Notes: PASS. Diff is faithful to plan with the two deviations Emma pre-disclosed (both well-justified): (a) TryAcquireLockAtPath keeps an agentName parameter for error-message preservation — verified existing AgentRegistryTests assert on 'claim in progress' + Environment.ProcessId substrings, dropping it would have broken them; (b) 6 mechanical override-edits to existing PollAndCleanup_* tests instead of 4 (DeadPid_ClearsAutoClose + FirstPoll_ProcessesRunning_ClearsImmediately also need GetProcessNameOverride='claude' because under the whitelist a null procName now skips instead of killing — correct call to keep the kill-and-throw path covered). TryAcquireLockAtPath / ReleaseLockAtPath bodies are char-faithful with the originals (only intentional change is GetLockFilePath(agentName) -> lockPath parameter). PollAndCleanup correctly brackets the read-decide-kill-clear cycle with try/finally; the early-continue runs inside the try so the lock is always released. Lock paths align with the registry: watchdog computes {dydoRoot}/agents/{dirname}/.claim.lock, registry computes Path.Combine(GetAgentWorkspace(agentName), '.claim.lock') = same value when dirname == agentName (the canonical convention). ClaudeProcessNames whitelist {claude, node} matches inquisition #0122 evidence exactly. ShellProcessNames left untouched per plan (no scope creep). Tests: 12 new instances cover #0121 lock-held-skip + release-and-resume, #0122 theory over all 9 emulators (gnome-terminal/konsole/xfce4-terminal/alacritty/kitty/wezterm/tilix/foot/xterm), whitelist positive case, and the finally-block lock-release guard. 3883/3883 pass under the worktree-isolated runner. gap_check.py exit 0; 136/136 modules pass tier requirements. Out-of-scope items (#0123-#0132, ShellProcessNames cleanup, atomic WriteStateFile) correctly stay filed.

Awaiting human approval.

## Approval

- Approved: 2026-04-29 12:04
