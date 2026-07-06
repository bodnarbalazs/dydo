---
title: ExecuteCleanup prints 'Worktree X: cleaned up.' even when RemoveZombieDirectory failed (misleading log on Windows file-lock)
id: 179
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-05-07
resolved-date: 2026-07-04
---

# ExecuteCleanup prints 'Worktree X: cleaned up.' even when RemoveZombieDirectory failed (misleading log on Windows file-lock)

Worktree cleanup logs success unconditionally even when the directory delete failed; misleading on Windows when files are locked.

## Description

`Commands/WorktreeCommand.cs:331-335` prints `Worktree {worktreeId}: cleaned up.` unconditionally after `TeardownWorktree` returns, regardless of whether the underlying directory delete actually succeeded.

The call chain is:

- `TeardownWorktree` (line 737-744) → `RemoveZombieDirectory` (line 809-823) → `DeleteDirectoryJunctionSafe`.
- `RemoveZombieDirectory` catches the failure and emits `WARNING: Could not remove directory {worktreePath}: {ex.Message}` to stderr (line 821).
- `TeardownWorktree` has no return value, so `ExecuteCleanup` cannot know teardown was incomplete.
- The unconditional `Console.WriteLine($"Worktree {worktreeId}: cleaned up.")` then fires.

On Windows, when the worktree directory is locked by another process, this produces a misleading log pair: `WARNING:` on stderr (the truth) followed by `cleaned up.` on stdout (the lie).

`FinalizeMerge` (line ~982-988) and `ExecutePrune` (line ~1018-1023) have the same shape and need the same fix.

## Impact

Log-correctness / observability defect. Operationally directly fed an incorrect inference during inquisition `investigate-watchdog-killing-active-agents`: the misleading "WARNING + cleaned up." pair seeded the (refuted) "watchdog kill-heuristic" hypothesis. See `dydo/project/inquisitions/agent-crashes.md` §"2026-05-08 — Dexter / Watchdog kill-heuristic investigation (the Heisenbug)" Finding #2 for full reasoning.

## Reproduction

On Windows, with another process holding an open handle inside a worktree directory:

```
dydo worktree cleanup <worktree-id> --agent <name>
```

stderr: `WARNING: Could not remove directory ...: The process cannot access the file ...`
stdout: `Worktree <id>: cleaned up.`

Inspect the filesystem — the directory is still on disk.

## Proposed Fix

Thread a return value through `RemoveZombieDirectory` → `TeardownWorktree` indicating success / partial / failure, and let `ExecuteCleanup`, `FinalizeMerge`, and `ExecutePrune` log one of three messages:

- `Worktree X: cleaned up.`
- `Worktree X: partially cleaned up (directory still in use — will retry on next prune).`
- `Worktree X: cleanup failed.`

Same change for the two adjacent callers. Implementation is small (one bool/enum return per helper, one branch per caller).

## Resolution

Fixed at HEAD: TeardownWorktree returns bool and ExecuteCleanup prints 'cleaned up' vs 'directory remains' conditionally (WorktreeCommand.cs:332-338,748,991). Triage sweep 2026-07-04 (Brian, CoS).