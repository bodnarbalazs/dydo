---
id: 125
area: backend
type: issue
severity: medium
status: open
found-by: inquisition
date: 2026-04-28
---

# State-file read/write unsynchronized between watchdog and registry

## Description

**Mechanism.** `AgentRegistry.WriteStateFile` (Services/AgentRegistry.cs:1350-1403) rebuilds the entire state file content and writes via `File.WriteAllText` — no atomic-replace, no lock around the write itself. The lock-protected callers (`ReserveAgent`, `ClaimAgent`, `ReleaseAgent`) hold `TryAcquireLock` (Services/AgentRegistry.cs:1978) at the operation level. The watchdog's `ParseStateForWatchdog` (Services/WatchdogService.cs:412-433) reads via `File.ReadAllText` and never acquires that lock. `ClearAutoClose` (Services/WatchdogService.cs:285-295) does an unlocked RMW.

The torn-read `try/catch` in `ParseStateForWatchdog` is a partial mitigation — it returns `(false, false, null, null)` on parse failure, which neutralizes one tick — but it does not protect against logically stale-but-syntactically-valid reads (e.g. observing the `free` content committed a moment ago, then acting on it 200ms later by which point the registry has already written `dispatched`). This is the underlying race that finding #1 exploits.

**Suggested fixes.**
1. Take the per-agent lock for the entire watchdog poll (read + decide + kill + ClearAutoClose). Closes finding #1 windows A and B and most of this issue.
2. Switch `WriteStateFile` to atomic temp-file rename: `File.WriteAllText(temp, content); File.Move(temp, dest, overwrite: true)`. Atomic on POSIX (`rename(2)`) and on NTFS (`MoveFileEx(MOVEFILE_REPLACE_EXISTING)`). Eliminates torn-read class entirely.

**Found by inquisition:** dydo/project/inquisitions/agent-deaths.md (Finding #5).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)