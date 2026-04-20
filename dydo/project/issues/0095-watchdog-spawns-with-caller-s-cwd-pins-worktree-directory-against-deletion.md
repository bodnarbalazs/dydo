---
id: 95
area: backend
type: issue
severity: high
status: open
found-by: inquisition
date: 2026-04-18
---

# Watchdog spawns with caller's CWD — pins worktree directory against deletion

## Description

`WatchdogService.EnsureRunning` (`Services/WatchdogService.cs:76-82`) constructs a `ProcessStartInfo` without setting `WorkingDirectory`. When `Process.Start` is called with `WorkingDirectory` unset, the spawned child inherits the parent's current directory. On Windows this pins an open directory handle on the CWD, blocking later `RemoveDirectory` on that path.

In practice, when a dispatch (e.g. `--auto-close`) fires from inside a worktree, the watchdog is spawned with its CWD inside that worktree. Because the watchdog never exits on its own (see issue #97), the worktree directory remains locked until the process is killed externally.

Runtime evidence on this machine: stale `watchdog.pid` (dead PID 27220) inside `dydo/_system/.local/worktrees/inquisition-worktree-system/dydo/_system/.local/` — and 15 stranded worktree directories, most from long-finished sessions. User remediation history (LC audit `ffba2137-…events` lines 97-101) shows the pattern `taskkill /im dydo.exe /f` + `dotnet tool update -g dydo`, consistent with a running watchdog locking its own image. Tied to inquisition `dydo/project/inquisitions/stale-dydo-processes.md` finding F1.

## Reproduction

Covered by `DynaDocs.Tests/Services/WatchdogServiceTests.cs :: EnsureRunning_DoesNotSetWorkingDirectory_InheritsCallerCwd` (PASSING). The test captures the `ProcessStartInfo` handed to `Process.Start` and asserts `string.IsNullOrEmpty(psi.WorkingDirectory)` while the caller's CWD is a worktree-like temp directory.

## Resolution

Set `psi.WorkingDirectory = <stable path>` in `EnsureRunning` before `Process.Start`. The stable path should be the main project root (see issue #96 — `FindDydoRoot` currently resolves to the worktree, so the fix must coordinate with that one; picking `Environment.SystemDirectory` or a neutral path is also acceptable). Add a regression test that asserts `psi.WorkingDirectory` is set to a non-worktree path.