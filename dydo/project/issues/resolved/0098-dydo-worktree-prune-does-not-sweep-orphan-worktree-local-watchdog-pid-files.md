---
id: 98
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-18
resolved-date: 2026-04-20
---

# dydo worktree prune does not sweep orphan worktree-local watchdog.pid files

## Description

Once issues #95/#96/#97 are fixed, no *new* worktree-local `watchdog.pid` files should appear. But this repo already has accumulated orphans — e.g. `dydo/_system/.local/worktrees/inquisition-worktree-system/dydo/_system/.local/watchdog.pid` points to dead PID 27220, and there are 15 stranded worktree directories on this machine, most from long-finished sessions. `dydo worktree prune` currently does not notice these stale pid files, so they sit there indefinitely, confusing future diagnostics (the file looks authoritative but isn't).

Tied to inquisition `dydo/project/inquisitions/stale-dydo-processes.md` recommended follow-up 8.

## Reproduction

1. Identify a stale worktree-local pid file: `find dydo/_system/.local/worktrees -name watchdog.pid`
2. Verify the PID it contains is not running (`tasklist /FI "PID eq <n>"`).
3. Run `dydo worktree prune`.
4. Observe the file still exists.

## Resolution

Extend `dydo worktree prune` to walk `{worktree}/dydo/_system/.local/watchdog.pid` for each discovered worktree (including already-orphaned ones), read the PID, check `ProcessUtils.IsProcessRunning`, and delete the file if stale. Optionally, delete the entire empty worktree directory if it has no live pid file and the branch is already removed (currently `prune` leaves unregistered zombie dirs — confirm scope before expanding).

Coordinate test placement with existing `WorktreeCommand` prune tests.