---
id: 123
area: backend
type: issue
severity: high
status: resolved
found-by: inquisition
date: 2026-04-28
resolved-date: 2026-04-30
---

# ReleaseAgent leaves auto-close: true on disk, opens kill window

Resolved high-severity bug: `ReleaseAgent` cleared most state fields but left `AutoClose: true` on disk, so between release and the watchdog's next poll the agent sat in the lethal `free + auto-close: true` state that fed the #0121 redispatch race. Resolved as won't-fix in commit `bd3cebe` — clearing `AutoClose` on release broke the auto-close mechanism itself. The per-agent lock from `06512de` closes the race window the original issue was motivated by, without needing the field clear.

## Description

**Mechanism.** `AgentRegistry.ReleaseAgent` (Services/AgentRegistry.cs:492-502) clears `Status`, `Role`, `Task`, `Since`, `WritablePaths`, `ReadOnlyPaths`, `UnreadMustReads`, `UnreadMessages` — but **not** `AutoClose`. `CleanupAfterRelease` (Services/AgentRegistry.cs:574-589) doesn't either. `ClearAutoClose` lives in the watchdog (Services/WatchdogService.cs:285) and runs only after the kill loop. So between release and the watchdog's next poll (up to 10s default), the agent is in the lethal `free + auto-close: true` state — exactly the precondition for finding #1's redispatch race.

**Suggested fix.** Smallest single change: add `s.AutoClose = false` to the lambda in `ReleaseAgent` (Services/AgentRegistry.cs:492-502). The 'kill old claude' path then runs at most once per dispatch (set on dispatch via `SetDispatchMetadata` at AgentRegistry.cs:1334-1341, cleared on release-or-watchdog-kill) and the race window in finding #1 vanishes even without the lock fix. Test by asserting `auto-close: false` on disk after release.

**Found by inquisition:** dydo/project/inquisitions/agent-deaths.md (Finding #3).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Resolved as won't-fix by bd3cebe — clearing auto-close on release crashed the auto-close mechanism (kill watchdog needs free + auto-close: true). Per-agent lock from 06512de closes the redispatch race the original issue motivated.