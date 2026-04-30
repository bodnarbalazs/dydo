---
id: 134
area: backend
type: issue
severity: critical
status: resolved
found-by: manual
date: 2026-04-29
resolved-date: 2026-04-30
---

# Auto-close mechanism broken — ReleaseAgent clears auto-close prematurely (v1.3.9 regression)

## Description

After today's six commits + tag v1.3.9, the auto-close terminal-kill mechanism is broken on every released agent. Released agents go free cleanly but the watchdog never kills their claude session — terminals stay open indefinitely.

## Root cause

Commit 8d3e3b1 (fix(registry): clear auto-close on release and atomic-replace state writes, #0123/#0125) added `s.AutoClose = false;` inside ReleaseAgent's UpdateAgentState lambda (Services/AgentRegistry.cs:505). After release, on-disk state is now `free + auto-close: false`. The watchdog kill condition (Services/WatchdogService.cs:359, `if (!autoClose || !isFree || agentName == null) return 0;`) requires `auto-close: true` to fire, so the kill window never opens and claude is never terminated.

## Why the original fix was wrong

#0123 was a workaround for the redispatch race in #0121 — the watchdog observing stale `free + auto-close: true` and killing a freshly redispatched claude. That race was already closed by the per-agent .claim.lock added in 06512de, which brackets PollAndCleanupForAgent against Reserve/Release/SetDispatchMetadata. With the lock in place, the post-release `free + auto-close: true` window is the *design*, not a bug — it is exactly when the watchdog is supposed to fire. #0123 was a misdiagnosed second-order fix that broke the legitimate flow.

## Verification

Live state at investigation time:
- Watchdog running (PID 40336), polling every 10s.
- Watchdog log: continuous `tick agents:14 kills_attempted:0` for ~10 minutes.
- dydo/agents/Noah/state.md (smoke-test agent): `status: free, auto-close: false`. Watchdog reads, kill gate fails on `!autoClose`, claude survives.

## Other suspects ruled out

- e1eac2e (Windows always-NoExit): does not compound; postClaudeCheck still terminates explicitly via `exit 0` on the free path.
- 4dd5d03 (PollAndCleanup → PollAndCleanupForAgent + KillClaudeProcesses): semantics-preserving refactor; kill condition unchanged.
- 762eeda (file-based anchors): watchdog still spawns and polls on Windows orphan path; not the regression.
- 3532bd9 (logging): never-throws contract honoured.

## Fix

Revert the one-line addition in ReleaseAgent (commit bd3cebe). The atomic state-file write from #0125 stays. The per-agent lock and the ClaudeProcessNames whitelist from #0121/#0122 stay.

## Reference commits

- 8d3e3b1 (regression source)
- 06512de (the per-agent lock that already closed #0121)
- bd3cebe (the revert that fixes this issue)
- 4dd5d03, 762eeda, 3532bd9, e1eac2e (audited, not the cause)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed by bd3cebe (revert of s.AutoClose=false in ReleaseAgent; the per-agent lock from 06512de keeps the redispatch race closed without that change).