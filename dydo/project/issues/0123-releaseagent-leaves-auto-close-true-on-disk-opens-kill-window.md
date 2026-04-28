---
id: 123
area: backend
type: issue
severity: high
status: open
found-by: inquisition
date: 2026-04-28
---

# ReleaseAgent leaves auto-close: true on disk, opens kill window

## Description

**Mechanism.** `AgentRegistry.ReleaseAgent` (Services/AgentRegistry.cs:492-502) clears `Status`, `Role`, `Task`, `Since`, `WritablePaths`, `ReadOnlyPaths`, `UnreadMustReads`, `UnreadMessages` — but **not** `AutoClose`. `CleanupAfterRelease` (Services/AgentRegistry.cs:574-589) doesn't either. `ClearAutoClose` lives in the watchdog (Services/WatchdogService.cs:285) and runs only after the kill loop. So between release and the watchdog's next poll (up to 10s default), the agent is in the lethal `free + auto-close: true` state — exactly the precondition for finding #1's redispatch race.

**Suggested fix.** Smallest single change: add `s.AutoClose = false` to the lambda in `ReleaseAgent` (Services/AgentRegistry.cs:492-502). The 'kill old claude' path then runs at most once per dispatch (set on dispatch via `SetDispatchMetadata` at AgentRegistry.cs:1334-1341, cleared on release-or-watchdog-kill) and the race window in finding #1 vanishes even without the lock fix. Test by asserting `auto-close: false` on disk after release.

**Found by inquisition:** dydo/project/inquisitions/agent-deaths.md (Finding #3).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)