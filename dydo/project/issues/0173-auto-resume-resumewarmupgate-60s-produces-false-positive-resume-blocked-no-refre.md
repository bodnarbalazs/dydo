---
id: 173
area: backend
type: issue
severity: high
status: open
found-by: inquisition
date: 2026-05-06
---

# Auto-resume ResumeWarmupGate=60s produces false-positive resume_blocked: no_refresh_after_warmup on real-but-slow rehydrations

The 60-second `ResumeWarmupGate` introduced in v1.4.3 fires before claude --resume finishes rehydrating real production conversations. The watchdog declares the resume "blocked" via `IsBadSessionFailFast` and saturates the resume-attempts cap, even when the resumed claude is alive and rehydrating. The user-visible symptom: every `resume` log line is followed ~60s later by `resume_blocked: no_refresh_after_warmup`, yet the session quietly recovers minutes later — the recovery rate is high but the logged "success" rate is ~0%.

## Description

Finding #1 (with subordinate Finding #5) of inquisition `dydo/project/inquisitions/agent-crashes.md` (Brian, 2026-05-06).

**Root location:** `Services/WatchdogService.cs:73` sets `ResumeWarmupGate = TimeSpan.FromSeconds(60)`. `IsBadSessionFailFast` (lines 544-548) returns true when warmup elapsed AND `ClaimedPid == PreResumePid`. In `PollAndResumeForAgent` (lines 458-464), a true result calls `registry.SaturateResumeAttempts(ctx.AgentName, ctx.Cap)` and logs `resume_blocked` with reason `no_refresh_after_warmup`.

**Why the gate fires falsely:** `PreResumePid` is captured from `state.md` and is exactly the value the resumed claude must overwrite via `RefreshClaimedPid` on its first tool call. Until the resumed claude reaches its first dydo guard hook, `ClaimedPid == PreResumePid` is necessarily true. claude --resume rehydration on real conversations takes minutes, not "tens of seconds" as the comment at line 70 assumes. The gate also does NOT check whether the resume terminal's `launched_pid` is alive — so it cannot distinguish "live and rehydrating" from "dead and gone."

**Direct evidence (today, 2026-05-06):**

| Session | Resume fired | Resume blocked | Last sidecar event |
|---------|-------------|---------------|--------------------|
| Charlie 8b52b181 | 13:27:00 | 13:28:06 | Release 13:36:19 (8 min later) |
| Brian f9936e33 | 16:16:32 | 16:17:34 | Release 16:48:53 (32 min later) |
| Frank 04d1191f | 16:44:53 | 16:45:56 | Release 16:55:14 (10 min later) |
| Brian 4c2838f8 | 18:03:06 | 18:04:11 | No sidecar (genuine failure) |

3 of 4 "blocked" sessions actually completed successfully — the resumed claude just took longer than 60s to rehydrate. The fourth session is the genuine failure case (no first tool call after resume).

**Cross-project replication:** the LC project at `C:/Users/User/Desktop/LC/dydo` shows the identical phase change on the same dates (Brian's report, "Cross-project replication of Finding #1"). The regression is platform-wide.

**Regression trace:** maps to commit `9b27195` (2026-05-02, v1.4.3) which introduced both the warmup gate and `IsBadSessionFailFast`. v1.4.6 install does not change the symptom (no `Services/` changes vs v1.4.5).

## Reproduction

1. Dispatch any agent that runs a non-trivial workload (e.g. an inquisitor reading sidecars).
2. Force-crash its claude (`taskkill /F /PID <pid>`).
3. Watch `dydo/_system/.local/watchdog.log`: the watchdog logs one `resume`, then 60s later `resume_blocked: no_refresh_after_warmup`.
4. Wait several minutes. The resumed claude does eventually rehydrate and reach `Release` in the audit sidecar, despite the "blocked" log.

## Resolution

Three options, in increasing order of correctness:

1. **Cheapest test:** raise `ResumeWarmupGate` from `TimeSpan.FromSeconds(60)` to `TimeSpan.FromMinutes(5)`. Single-line change in `Services/WatchdogService.cs:73`. The 5-minute gate would have correctly caught all three of today's success cases (8/32/10-minute rehydration deltas). Update the comment at lines 67-71 to reflect the calibrated value.
2. **Better:** replace the wall-clock gate with a liveness check. `IsBadSessionFailFast` should additionally assert `!ProcessUtils.IsProcessRunning(launchedPid)`. This requires persisting `launched-pid` next to `pre-resume-pid` in `state.md` (currently it is logged by `WatchdogLogger.LogResume` but not threaded back into the resume context). With this change the gate fires only when the launched terminal/claude is also dead — distinguishing "rehydrating" from "crashed during resume."
3. **Best:** stop saturating the cap on a guess. `no_refresh_after_warmup` should degrade to a one-time log line ("resume not yet acknowledged") without poisoning subsequent attempts. The `attempts < 3` cap (`ResumeAttemptsCap`) is the actual loop-protection; saturating to `cap` on a false positive collapses the safety net into a single shot.

(Filled when resolved)

## Related

- [Decision 022 — Auto-Resume Crashed Agents](../decisions/022-auto-resume-crashed-agents.md)
- [#0150](0150-auto-resume-on-crash-sometimes-fails-to-trigger-watchdog-misses-dead-claude-process.md) — umbrella "auto-resume sometimes fails to trigger." This issue is the specific identified mechanism for the post-v1.4.3 era.
- [#0152](0152-auto-resume-race-watchdog-fires-duplicate-launches-during-the-resumed-claude-war.md) — the warmup gate itself was the fix for #0152 (cap-saturation race). This issue documents that the fix overshoots — the gate is too short and SaturateResumeAttempts too heavy.
- [#0153](0153-resume-attempts-is-not-reset-on-same-session-reclaims-so-the-counter-accumulates.md) — `ResetResumeBookkeeping` partly self-heals the cap saturation once the resumed claude eventually re-claims.
- `Services/WatchdogService.cs:73` (`ResumeWarmupGate`), lines 458-464 (the SaturateResumeAttempts callsite), lines 544-548 (`IsBadSessionFailFast`).
- `Services/AgentRegistry.cs:341` (`RefreshClaimedPid`) — runs only after the resumed claude makes its first tool call.