---
id: 96
area: backend
type: issue
severity: high
status: resolved
found-by: inquisition
date: 2026-04-18
resolved-date: 2026-04-21
---

# FindDydoRoot resolves to worktree — spawns duplicate watchdog with worktree-scoped pid file

## Description

`PathUtils.FindDydoRoot` (`Utils/PathUtils.Discovery.cs:11-17`, via `ConfigService.GetProjectRoot`) walks up from `Environment.CurrentDirectory` looking for the nearest `dydo.json`. Worktrees are full git checkouts, so each worktree directory contains its own `dydo.json` at the root. Crucially, only four subpaths are junctioned from the worktree back to the main repo (`Commands/WorktreeCommand.cs:475-481`): `dydo/agents`, `dydo/_system/roles`, `dydo/project/issues`, `dydo/project/inquisitions`. **`dydo/_system/.local` is NOT junctioned.**

Consequence: when any code running in a worktree calls `FindDydoRoot()`, it gets the worktree's `dydo/` back. `WatchdogService.EnsureRunning()` — the overload without arguments — uses exactly that resolution (`Services/WatchdogService.cs:34`), so a dispatch from inside a worktree spawns a **second watchdog**, with its pid file at `{worktree}/dydo/_system/.local/watchdog.pid` and its state scan (`PollAndCleanup` etc.) rooted at the worktree's dydo directory. That watchdog then independently pins the worktree (see #95) and duplicates work already handled by the main-project watchdog.

Tied to inquisition `dydo/project/inquisitions/stale-dydo-processes.md` finding F2.

## Reproduction

Covered by `DynaDocs.Tests/Services/PathUtilsWorktreeIsolationTests.cs :: FindDydoRoot_FromInsideWorktree_ResolvesToWorktreeNotMainProject` (PASSING). The test sets up `<main>/dydo.json` + `<main>/dydo/_system/.local/worktrees/my-wt/dydo.json`, sets the current directory into the worktree, calls `PathUtils.FindDydoRoot()`, and asserts the result equals the worktree's `dydo/` and that `WatchdogService.GetPidFilePath(resolved)` lands inside the worktree.

Runtime corroboration: `dydo/_system/.local/worktrees/inquisition-worktree-system/dydo/_system/.local/watchdog.pid` (dead PID 27220) — a worktree-scoped pid file left behind by a previous worktree-spawned watchdog.

## Resolution

For watchdog purposes specifically, resolve the dydo root to the **main project** root rather than the nearest one. Options: (a) add a `FindMainProjectRoot` that walks past worktree directories (detect `_system/.local/worktrees/{id}` in the path and climb above it), (b) pass the main project root explicitly at the dispatch call site (`DispatchService.cs:205`) and plumb it through `EnsureRunning`, (c) read the existing `.worktree-root` marker (see `dydo/understand/architecture.md § Worktree Dispatch`). Option (c) is the cheapest and matches existing marker conventions. Must pair with issue #95 (CWD also needs pinning).