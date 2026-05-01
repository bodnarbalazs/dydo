---
id: 150
area: backend
type: issue
severity: high
status: open
found-by: manual
date: 2026-05-01
---

# Auto-resume on crash sometimes fails to trigger — watchdog misses dead claude process

Lived practice on v1.4.x: when a claude session crashes mid-task, the auto-resume mechanism (Decision 022) usually fires correctly, but **sometimes it doesn't trigger at all** — the dead terminal stays dead, no resume terminal launches, and the agent is silently stuck in `state.md.status: working`. The conditions that distinguish a successful auto-resume from a missed one are not yet known. Companion to #0144 (new-window vs new-tab polish). Both deserve a thorough inquisition pass.

## Description

Decision 022 specifies the watchdog detects crashes via three conjuncts evaluated each ~10s poll:

1. `state.md` has `status: working` (agent has not released)
2. `.session.ClaimedPid` is not a live process
3. `resume-attempts < cap` (default 3)

When all three hold, the watchdog launches `claude --resume <sessionId>` for that agent. In the missed-trigger cases observed by the user, at least one of the three conjuncts is failing — but which one, and under what conditions, has not been investigated.

Candidate failure modes worth ruling in or out:

- **`ClaimedPid` mismatch** — `.session` file may hold a parent shell PID (PowerShell launcher) rather than the claude PID itself; if the shell stays alive after claude exits, conjunct (2) is false. `RefreshClaimedPid` (#0143 fix) addressed multi-spawn but may not cover this case.
- **Watchdog not running** — the watchdog is a per-dispatch background process; if it dies (or never started, e.g. on resume-launched agents that re-claim into the same session), the poll never fires.
- **`state.md` race** — if the crashed claude was mid-write to `state.md`, the file may be transiently malformed and the watchdog skips that agent rather than triggering resume.
- **`.session` file missing or stale** — Decision 022 says the behavior is on by default "for any agent with a captured `.session` file"; if the file is absent (older agents, race during claim), no resume.
- **`resume-attempts` not resetting** — Decision 022 says it resets on `claim` and `release`; if a previous crash sequence left `resume-attempts: 3` and the reset path is missed, conjunct (3) blocks subsequent crashes.
- **Watchdog poll cadence drift** — 10s is the documented cadence; if the actual cadence skews longer (or the watchdog goes idle), short crashes may miss a poll.
- **Process-liveness check false-positive** — a crashed claude may leave a zombie or orphan still owning the PID briefly; conjunct (2) returns "alive" and skips resume.

## Reproduction

Not yet reliable. Anecdotal pattern: occurs more often on certain agents (Adele was flagged in Brian's handoff as crash-prone), and during heavy parallel dispatches. Inquisitor should attempt:

1. Dispatch a long-lived agent, monitor its `.session.ClaimedPid` and watchdog process state continuously.
2. Forcibly crash claude in the terminal (close window, kill -9 the claude PID, kill the parent shell PID) and record which conjuncts the watchdog evaluates as false in each case.
3. Repeat across original-dispatch surface (new window vs. new tab vs. resumed agent) — companion to #0144.
4. Compare against the cases where auto-resume *does* fire to identify the distinguishing variable.

## Resolution

(Filled when resolved)

## Related

- [Decision 022 — Auto-Resume Crashed Agents](../decisions/022-auto-resume-crashed-agents.md)
- [Issue #0143 (resolved)](resolved/0143-watchdog-re-resumes-already-resumed-agent-on-subsequent-ticks-3-terminals-for-th.md) — multi-spawn; fixed via `RefreshClaimedPid`.
- [Issue #0144](0144-auto-resume-opens-in-new-window-should-reuse-the-original-window-as-a-new-tab-wh.md) — companion polish: when resume *does* fire, it always opens a new window instead of reusing the original window as a new tab.
- `Services/WatchdogService.cs` — `PollAndResumeForAgent` and the three-conjunct gate.
- `Services/AgentRegistry.cs` — `.session` capture, `RefreshClaimedPid`, `resume-attempts` reset.
- `Services/WindowsTerminalLauncher.cs`, `LinuxTerminalLauncher.cs`, `MacTerminalLauncher.cs` — `LaunchResume` path.
