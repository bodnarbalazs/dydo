---
id: 121
area: backend
type: issue
severity: critical
status: open
found-by: inquisition
date: 2026-04-28
---

# Watchdog kills re-dispatched agents in poll-gap (stale-decision + ClearAutoClose RMW)

## Description

Root cause of mid-work agent deaths. Two race windows:

**Window A — stale-decision kill.** `WatchdogService.PollAndCleanup` (Services/WatchdogService.cs:248-282) reads agent state via `ParseStateForWatchdog` (no lock), decides to kill based on `free + auto-close: true`, then runs the kill loop with no re-check. Between the read and the kill, an orchestrator can re-dispatch the agent: `ReserveAgent` (AgentRegistry.cs:144-148) flips status to Dispatched under per-agent lock, `LaunchNewTerminal` spawns a fresh claude with the identical `{agent} --inbox` prompt, `SetupAgentWorkspace` flips to Working. The watchdog still acts on its stale `free` snapshot and kills the new claude.exe (matches by command line; not a shell, not skipped).

**Window B — ClearAutoClose unlocked RMW.** `WatchdogService.ClearAutoClose` (Services/WatchdogService.cs:285-295) does `ReadAllText → string.Replace → WriteAllText` with no lock. If a registry write lands between the read and the write, the watchdog rewrites the dispatcher's `Working + auto-close: true` content with its stale `Free` snapshot — silently rolling back `status`, `role`, `task`, `dispatched-by`. Worst case: next `ReserveAgent` reads the rolled-back state and double-launches.

**Regression history.** Commit b60b258 (2026-03-27) removed the two-poll deferral that previously protected the redispatch race, citing 'phantom close issue ... fixed separately' — but the cited fix (b2db397, ShellProcessNames skip) only handles the bash-wrapper subset. Commit d2a3ba2 (2026-04-24) re-anchored the watchdog from the short-lived dispatcher to `FindAncestorProcess('claude')`, extending its lifetime to the full orchestrator session and amplifying the race. Audit logs show a step change in the death rate after d2a3ba2: ~1.1 deaths/day pre vs ~3.7/day post.

**Suggested fix.** Take the per-agent registry lock (`AgentRegistry.TryAcquireLock`, AgentRegistry.cs:1978) for the entire watchdog poll on each agent — read state, kill loop, ClearAutoClose all under the same lock. Closes both windows.

**Found by inquisition:** dydo/project/inquisitions/agent-deaths.md (Finding #1).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)