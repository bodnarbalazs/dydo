---
area: process-lifecycle
type: inquisition
---

# Stale dydo processes & worktree file locks

## 2026-04-18 — Frank

### Scope

- **Entry point:** targeted audit from Brian's brief (`dydo/agents/Brian/brief-stale-dydo-processes-inquiry.md`)
- **Question:** which dydo-spawned processes can hold file-system locks past their intended lifetime, and why?
- **Files investigated:**
  - `Services/WatchdogService.cs`
  - `Commands/WaitCommand.cs`
  - `Services/AuditService.cs`
  - `Services/DispatchService.cs` (`EnsureRunning` call site, `CreateGitWorktree`)
  - `Services/TerminalLauncher.cs`, `Services/WindowsTerminalLauncher.cs`
  - `Commands/WorktreeCommand.cs` (`ExecuteCleanup`, `ExecutePrune`, `TeardownWorktree`, `DeleteDirectoryJunctionSafe`, `RemoveZombieDirectory`)
  - `Utils/FileLock.cs`, `Services/AgentRegistry.cs` (claim lock)
  - `Services/ProcessUtils.cs`, `Services/ProcessUtils.Ancestry.cs`
  - `Utils/PathUtils.Discovery.cs` (`FindDydoRoot`)
- **Evidence cross-checked:**
  - `C:\Users\User\Desktop\LC\dydo\_system\audit\2026\ffba2137-…events` (the taskkill sequence and preceding worktree state)
  - `C:\Users\User\Desktop\LC\dydo\_system\audit\2026\6d618692-…events` (earlier `rm -rf worktrees` attempts blocked by the nudge)
  - Live process scan: `tasklist /FI "IMAGENAME eq dydo.exe"` plus `Win32_Process.CommandLine` lookups
  - `watchdog.pid` contents in LC, in the main DynaDocs repo, and inside every DynaDocs worktree leaf
- **Scouts dispatched:** 4 test-writers in parallel, one per hypothesis:
  - **Emma** → H1 (watchdog CWD inheritance) — **CONFIRMED**
  - **Grace** → H2 (`FindDydoRoot` resolves to worktree) — **CONFIRMED**
  - **Henry** → H3 (watchdog has no parent-PID / cancellation / signal handling) — **CONFIRMED**
  - **Iris** → H4 (`RemoveZombieDirectory` error hides the locked path) — **NOT REPRODUCED**
- **Reconnaissance only. No production code changed; only new tests added to prove the mechanisms.**

### Process inventory

Every process that dydo either *is* or *spawns* that can outlive the command the user typed. "CWD-inherits" = `ProcessStartInfo.WorkingDirectory` is not set, so the child inherits the parent's CWD.

| Process | Entry point | Lifetime model | Resources held | CWD source |
|---------|-------------|----------------|----------------|-----------|
| **watchdog** | `WatchdogService.Run` via `dydo watchdog run`, spawned by `WatchdogService.EnsureRunning` (`Services/WatchdogService.cs:76-95`) | `while (true) { Thread.Sleep(10_000); … }` (`WatchdogService.cs:147-162`). **No cancellation, no parent-PID check, no signal handler.** Exits only on external kill. | Its own `.exe` image, process CWD (see below), transient `File.WriteAllText` on `{agent}/state.md`, transient `Process.Kill` handles on auto-close targets. Holds no long-lived file streams. | Inherits parent's CWD — `EnsureRunning` sets no `WorkingDirectory` (`WatchdogService.cs:76-82`). |
| **dydo wait (general)** | `WaitCommand.WaitGeneral` (`Commands/WaitCommand.cs:74-118`) | `while (!cancelled) { … Thread.Sleep(10_000); }`. Exits on Ctrl+C, on message received, or on parent/claude ancestor death (checked each tick). | `.waiting/{task}.json` marker (rewritten, not held open), inbox directory reads. | The `dydo wait` CLI process — inherits caller's CWD (bash → typically the agent's working directory). |
| **dydo wait (task-scoped)** | `WaitCommand.WaitForTask` (`WaitCommand.cs:120-157`) | Same shape as general wait. | `.waiting/{task}.json` marker, inbox reads. | Same as above. |
| **git subprocesses** | `AuditService.GetCurrentGitHead` (`AuditService.cs:272-294`), `DispatchService.GetCurrentGitBranch` / `RunGitForWorktree` (`DispatchService.cs:346-368`, `633-655`), `WorktreeCommand.RunProcess* ` (`WorktreeCommand.cs:412-473`), `SnapshotService` (`SnapshotService.cs:51-110`), `InquisitionCommand` (`InquisitionCommand.cs:172-195`) | Short-lived; bounded by `WaitForExit(timeout)` and `Kill()` on timeout. | Child git processes that may briefly hold `HEAD.lock`/`index.lock` inside either the main repo or the worktree passed via `-C`. | `WorkingDirectory` is set for most — exceptions: `AuditService.GetCurrentGitHead` and `DispatchService.GetCurrentGitBranch` inherit parent CWD. |
| **dispatched terminal (wt/pwsh)** | `WindowsTerminalLauncher.Launch` (`WindowsTerminalLauncher.cs:70-117`) | Lives for the duration of the claude session. The PowerShell `finally` block does `Set-Location '{mainRoot}'; dydo worktree cleanup …` on exit (`WindowsTerminalLauncher.cs:38-64`). | CWD inside the worktree, its own child processes (claude, bash, dydo guards, sub-dispatches). | Explicitly `Set-Location` into the worktree. |
| **claude.exe (child of the dispatched terminal)** | Spawned by the terminal PowerShell script. | Lives until the agent releases or the user closes the tab. | CWD inside the worktree; spawns Node/bash children that also live there. | Inherits from the PowerShell process (the worktree). |
| **dydo guard (transient)** | `Commands/GuardCommand` on every Claude tool call. | Per-call; tens of milliseconds. | Audit sidecar append (`File.AppendAllText`, self-closing), session lock (`using var`, scope-bounded). | Inherits claude's bash → worktree. Lifetime too short to matter. |
| **watchdog pid file** | `{dydoRoot}/_system/.local/watchdog.pid` (`WatchdogService.cs:26-27`). **Not a process — but tracks one.** | Created atomically on spawn (`FileMode.CreateNew`, `FileShare.None`, closed in `finally`). Deleted in `Stop`. **Not deleted on crash** — next `EnsureRunning` checks liveness and overwrites (`WatchdogService.cs:42-53`). | — | — |

### Plausible lock-holders (ranked)

Ranked by how well each fits the user's specific report: "a process held a file handle that blocked worktree deletion; `taskkill /im dydo.exe /f` released it."

#### 1. Watchdog spawned with its CWD inside a worktree — HIGH confidence (all three mechanisms TESTED)

The watchdog is the only dydo process that (a) lives indefinitely, (b) inherits an unconstrained CWD, and (c) has no exit path short of being killed. All three legs of this finding now have a passing test in the suite.

**Tested evidence (all three mechanisms):**

- **CWD inheritance** — `DynaDocs.Tests/Services/WatchdogServiceTests.cs :: EnsureRunning_DoesNotSetWorkingDirectory_InheritsCallerCwd` (Emma). Stubs `WatchdogService.StartProcessOverride`, sets the current directory into a fake "worktree" temp dir, calls `EnsureRunning(dydoRoot)`, captures the `ProcessStartInfo` that would have been passed to `Process.Start`, and asserts `string.IsNullOrEmpty(psi.WorkingDirectory)` — i.e. proves the watchdog inherits the caller's CWD. PASSED. Captured `WorkingDirectory` value: `""` (string.Empty — the .NET default when not set).
- **Per-worktree resolution** — `DynaDocs.Tests/Services/PathUtilsWorktreeIsolationTests.cs :: FindDydoRoot_FromInsideWorktree_ResolvesToWorktreeNotMainProject` (Grace). Builds `<main>/dydo.json` + `<main>/dydo/_system/.local/worktrees/my-wt/dydo.json`, `SetCurrentDirectory` into the worktree, calls `PathUtils.FindDydoRoot()`, asserts the result equals the worktree's `dydo/` path and is NOT the main's, and that `WatchdogService.GetPidFilePath(resolved)` lands inside the worktree. PASSED. Concrete run paths in the commit.
- **No liveness / signal handling** — `DynaDocs.Tests/Services/WatchdogParentLivenessAbsenceTests.cs` (Henry). Three structural tests: (i) the source of `WatchdogService.Run` contains NONE of `GetParentPid`, `FindAncestorProcess`, `CancellationToken`, `ProcessExit`, `CancelKeyPress`; (ii) the detector works — `WaitCommand.cs` DOES contain them (sanity check); (iii) the `Run` body is a bare `while(true) { Thread.Sleep; … }` with no `break`/`return` after the loop start. All three PASSED. The behavioural leak-a-real-watchdog test was intentionally dropped — running it would itself orphan a watchdog.

**Why the CWD ends up in a worktree:**

- `EnsureRunning` uses `PathUtils.FindDydoRoot() ?? "."` to locate the PID file (`WatchdogService.cs:34`, `113`, `144`).
- `FindDydoRoot` walks up from `Environment.CurrentDirectory` looking for `dydo.json` (`Utils/PathUtils.Discovery.cs:11-17`, via `ConfigService.GetProjectRoot`).
- Worktrees are full git checkouts — each contains its own `dydo.json` at the worktree root. Only four subpaths are junctioned back to the main repo (`Commands/WorktreeCommand.cs:475-481`): `dydo/agents`, `dydo/_system/roles`, `dydo/project/issues`, `dydo/project/inquisitions`. `_system/.local/` is **not** junctioned.
- Therefore, when a dispatch with `--auto-close` fires from inside a worktree (for example, when an agent sub-dispatches from its worktree-scoped session), `FindDydoRoot()` returns **the worktree's** `dydo/` path. The watchdog PID file is written to `{worktree}/dydo/_system/.local/watchdog.pid`, and `Process.Start` — with no `WorkingDirectory` set (`WatchdogService.cs:76-82`) — spawns a watchdog whose CWD is the same worktree directory the dispatching dydo was running in.
- On Windows, an open directory handle (which the OS maintains for every process's CWD) blocks `RemoveDirectory`. The watchdog therefore pins the worktree directory against deletion until it is killed.

**Why it outlives its purpose:**

- `Run()` is a bare `while (true)` loop with a 10 s sleep (`WatchdogService.cs:147-162`). No `CancellationToken`, no `AppDomain.ProcessExit`, no `Console.CancelKeyPress`, no parent-PID liveness check. Whatever spawned it can disappear — the watchdog keeps going.
- There is no "work is done" condition. Even when every agent is free and no queues remain, the loop continues polling.
- `EnsureRunning` only *starts* a watchdog if no live PID is registered in the worktree's PID file; it never stops one (`WatchdogService.Stop` is only called from `dydo watchdog stop`, which is not invoked during normal worktree teardown — verified: no `grep` hit for `WatchdogService.Stop` outside of `dydo watchdog stop`/its tests).

**Evidence in the live system:**

Snapshot taken during this inquiry (main DynaDocs repo on this machine):

```
PID   CommandLine
45760 "C:\Users\User\.dotnet\tools\dydo.exe" watchdog run           <- LC project watchdog
39680 "C:\Users\User\.dotnet\tools\dydo.exe" watchdog run           <- DynaDocs project watchdog
34124 C:\Users\User\.dotnet\tools\dydo.exe wait --task coverage-slice-G1-asset-repos
```

And — the smoking gun — a stale worktree-local PID file:

```
/c/Users/User/Desktop/Projects/DynaDocs/dydo/_system/.local/worktrees/
  inquisition-worktree-system/dydo/_system/.local/watchdog.pid  -> 27220
```

PID 27220 is not running (verified with `tasklist /FI "PID eq 27220"` → "No tasks"). The worktree still exists on disk despite `inquisition-worktree-system` being long finished. This is exactly the shape the user described: a watchdog was once alive inside that worktree, holding the CWD; the user killed it; the file handle was released; but neither the PID file nor the now-orphaned worktree directory was cleaned up.

The main-repo worktrees directory currently holds 15 leaf worktrees (`ls .../worktrees`) — most are old `inquisition-*` / `smoke-*` sessions that should have been cleaned up.

LC-side evidence in `ffba2137-…events`:

- Lines 93-95: agent runs `git worktree list`, then `git -C .../worktrees/coverage-slice-C-auth-misc status --short`. The worktree directory is clearly still present ~2 h after merge notifications.
- Lines 97-98 (2026-04-17T20:25:05/12): back-to-back `taskkill /im dydo.exe /f` and `taskkill //IM dydo.exe //F`. Immediately followed by `dotnet tool install -g dydo`, `dotnet tool update -g dydo`, `dotnet tool uninstall … && dotnet tool install …` (lines 99-101). `dotnet tool update` fails on Windows if the tool's `.exe` is currently running — same lock mechanism, different lock target. The user's workaround sequence here is only consistent with a persistent `dydo.exe` process (i.e. the watchdog).
- Earlier (`6d618692-…events` lines 159, 166, and `ffba2137-…events` line 161): multiple `rm -rf dydo/_system/.local/worktrees/…` attempts blocked by the "Use dydo worktree cleanup instead of deleting worktree directories directly" nudge. Implies `dydo worktree cleanup` was already attempted and left the directories behind — otherwise the user would not have been reaching for `rm -rf`.

#### 2. `dydo wait` process with its CWD inside a worktree — MEDIUM confidence

`WaitCommand` runs inside the CLI process itself; its CWD is whatever bash passed in (`Commands/WaitCommand.cs`). Brian's current live snapshot has `dydo wait --task coverage-slice-G1-asset-repos` running (pid 34124, parent is a Claude-spawned bash).

Mitigations present, but imperfect:

- Parent-PID and claude-ancestor liveness checks every 10 s (`WaitCommand.cs:103-108`, `143-147`). If bash dies, the wait exits. **But** `GetParentPid` on Windows uses `NtQueryInformationProcess` which returns the PID as recorded at process creation — the PID is not invalidated when the parent exits, it just becomes a no-op lookup, and `IsProcessRunning` correctly reports false, so the check does work in practice. The only gap is that **PID recycling** can let a freshly-started unrelated process masquerade as the parent; low-probability but non-zero over hours-long waits.
- `Console.CancelKeyPress` catches Ctrl+C and Ctrl+Break only; it does **not** catch `CTRL_CLOSE_EVENT` when the user closes the terminal tab with the X button. In that case the process is killed uncleanly, the `finally` block that removes the wait marker never runs, and the CWD handle is released by the OS (so no lock *from this path* after death) — but `PollOrphanedWaits` (`WatchdogService.cs:272-313`) does clean the markers up on the next watchdog tick.
- If `FindAncestorProcess("claude")` returns null at startup (parent chain doesn't include claude — e.g., a script invokes `dydo wait` directly), the claude liveness guard is silently skipped (`WaitCommand.cs:77`, `123`). Not catastrophic because the parent check still runs, but worth noting.

Net: a `dydo wait` *while* running does pin its CWD, but death paths release it. The residual risk window is bounded by the 10 s poll interval (up to ~10 s after bash dies before wait exits). Short-lived locks, but real.

#### 3. `dotnet tool` (re)installation blocked by a running watchdog — HIGH confidence, different lock

Not a worktree lock, but the same root cause: `taskkill /im dydo.exe /f` was the only remediation because `dotnet tool update -g dydo` needs to overwrite `C:\Users\User\.dotnet\tools\dydo.exe`, and Windows locks an executable file while any process image is loaded from it. The LC event sequence (above) shows this pattern directly. Future-you investigating a tool-install failure should recognize this is the same watchdog, not a distinct problem.

#### 4. Git itself — LOW confidence as the proximate cause

Git can absolutely hold `.git/worktrees/{id}/locked`, `HEAD.lock`, `index.lock` during operations. The inquiry brief rightly called this out. Findings:

- Every git subprocess in dydo is launched with `RedirectStandardInput = true` + `StandardInput.Close()` (`WorktreeCommand.cs:435-445`, `DispatchService.cs:638-648`) and `WaitForExit(ProcessTimeoutMs=30_000)` + `Kill()` on timeout. Git cannot indefinitely hold locks through dydo's code paths — a hung git is killed after 30 s.
- `git worktree remove --force` in `TeardownWorktree` (`WorktreeCommand.cs:537-544`) is followed by `RemoveZombieDirectory`. If git held `.git/worktrees/{id}/locked`, the remove would have failed non-silently and the directory removal would have reported it.

Given the `dotnet tool update` evidence (which is unambiguously a dydo.exe lock, not a git lock), and the presence of a stranded worktree-local `watchdog.pid`, the dominant cause is the watchdog, not git. Git may contribute in other incidents but is not the primary explanation here.

#### 5. Dispatched-terminal PowerShell (CWD in worktree) with killed `finally` — LOW confidence

The launcher script's `finally` block does `Set-Location '{mainRoot}'; dydo worktree cleanup …` before exit (`WindowsTerminalLauncher.cs:38-64`). If the terminal is force-closed via `CTRL_CLOSE_EVENT`, PowerShell gets ~5 seconds to run the finally before the OS kills it. Usually that's enough; if not, the CWD-in-worktree is released on process death, so no residual lock from this path — but the expected cleanup call is skipped, leaving the worktree directory and registered git worktree behind (a different *orphaning* problem, not a *locking* one).

### Evidence from LC

- `C:\Users\User\Desktop\LC\dydo\_system\audit\2026\ffba2137-…events`, lines 93-101: worktree `coverage-slice-C-auth-misc` inspected live, then a `taskkill /im dydo.exe /f` + `taskkill //IM dydo.exe //F` sequence, immediately followed by three `dotnet tool install/update/uninstall` attempts. The reinstall attempts are only necessary if `dydo.exe` was locking its own image — i.e., a running watchdog.
- Same file, line 161 (2026-04-18T17:36:04): `rm -rf dydo/_system/.local/worktrees/coverage-slice-B1-email …` blocked by the dydo nudge. The user was attempting direct filesystem deletion of worktree directories that `dydo worktree cleanup` had not removed.
- `6d618692-…events` lines 159-166: earlier `rm -rf dydo/_system/.local/worktrees/coverage-slice-C-auth-misc …` attempt on 2026-04-14 — same pattern, recurring over at least four days.
- `C:\Users\User\Desktop\LC\dydo\_system\.local\watchdog.pid` → 45760 → live watchdog at the LC root level. LC's `worktrees/` directory is currently empty, so this watchdog is no longer pinning any worktree — but it has been running since today 19:51:40 UTC, well after the user's last LC session presumably ended.
- DynaDocs main repo: 15 stranded worktree directories, at least one with a dead-PID `watchdog.pid` inside (`inquisition-worktree-system`).

### Hypotheses not reproduced

**H4 — `RemoveZombieDirectory` error hides which path refused to delete.** I expected the warning printed at `Commands/WorktreeCommand.cs:604-617` to be generic ("Access denied") and lose the exact path that had been locked. Iris's test proved the opposite. `DynaDocs.Tests/Commands/WorktreeCommandTests.cs :: RemoveZombieDirectory_HeldInnerFile_StderrPreservesInnerPath` (contract form, after she inverted the failing version) creates a nested file, opens it with `FileShare.None`, calls `RemoveZombieDirectory`, and captures this verbatim stderr:

```
WARNING: Could not remove directory C:\Users\User\AppData\Local\Temp\dydo-wt-test-02a76520\zombie-locked-862667a4: The process cannot access the file 'C:\Users\User\AppData\Local\Temp\dydo-wt-test-02a76520\zombie-locked-862667a4\nested\locked-evidence-f7894991.dat' because it is being used by another process.
```

The inner path (`…\nested\locked-evidence-f7894991.dat`) **is** surfaced, because `.NET`'s `IOException.Message` from a blocked `File.Delete` already embeds it. Hypothesis rejected. The *remaining* gap — no PID / image name of the holding process — is now kept in the report as a "nice to have" follow-up rather than a critical diagnostic failure.

No other hypotheses were left untested.

### Open questions

1. **Does the guard also spawn watchdogs into worktrees?** Every Claude tool call in a worktree runs the guard inside that worktree. The guard itself never calls `EnsureRunning` (verified by grep: only `DispatchService.cs:205` and `WatchdogCommand.cs:15` call it). But any dispatch from within a worktree does, so: yes, the per-worktree watchdog scenario is triggered by a normal agent sub-dispatch flow, not by an edge case.
2. **Does Windows reparent a child process's CWD when the directory is deleted from elsewhere?** If an admin unlocks the directory via tools like `handle.exe /c`, Windows does *not* automatically update a running process's CWD. We did not attempt a live `handle.exe` check — no Sysinternals available — so the directory-handle hypothesis for the watchdog is inferred from the `ProcessStartInfo` contract and the Windows CWD model, not directly observed. A single `handle.exe -accepteula -p <watchdog_pid>` on a repro machine would close the loop in seconds.
3. **Does LC's live watchdog (pid 45760) have its CWD inside an LC worktree or at the LC root?** Cannot determine without admin-level process inspection. The fact that LC's `worktrees/` directory is now empty means it's not currently blocking anything we can see; but if it was spawned from within a worktree that has since been deleted by other means, the watchdog's CWD refers to a deleted directory — which on Windows is a "deleted but still held" state that can still cause secondary oddities.
4. **Do orphan watchdogs share agent state correctly?** A worktree-local watchdog polls `{worktree}/dydo/agents` — which is a junction back to the main repo. So two watchdogs (main + worktree) can race on the same agent state files. Probably benign (same `Process.Kill` targets, same `File.WriteAllText` replacement), but not analyzed in depth.

### Recommended follow-ups

Concrete next steps, roughly ordered by impact-to-effort:

1. **Pin the watchdog's CWD to the main project root when spawning** — in `WatchdogService.EnsureRunning` set `psi.WorkingDirectory = dydoRoot` (or the main project root derived from it) before `Process.Start`. Eliminates the worktree-directory lock mechanism entirely. One-line change plus a test.
2. **Resolve `FindDydoRoot` to the main project root for watchdog purposes, not the nearest one** — so that a dispatch from inside a worktree does not spawn a *second* watchdog. There should be one watchdog per project, not per worktree. Same change target as (1) but semantically distinct.
3. **Add a parent-PID or heartbeat-file liveness check to the watchdog loop** — either store the spawning dydo's PID (or the whole "last ping" timestamp in `watchdog.pid`) and self-terminate if the anchor is gone for N minutes. Without (1)/(2), this at least bounds the damage from a rogue worktree-scoped watchdog.
4. **Clean up `watchdog.pid` on graceful exit** — the watchdog currently has no exit path, so there is also no file cleanup. Once (3) exists, `Run()` should delete the PID file before returning.
5. **Identify the locking process on `RemoveZombieDirectory` failure**. The path *is* already surfaced — H4 disproved my original concern: `.NET`'s `IOException.Message` on a locked delete already embeds the full inner path (verified; see H4 test below). The remaining gap is the process ID / image name of the holder. Shelling out to `handle.exe` / `openfiles.exe` on Windows, or `lsof`/`fuser` on Linux, and appending that to the warning would have named the watchdog directly and saved this inquiry.
6. **Worktree cleanup should retry with backoff** when `DeleteDirectoryJunctionSafe` hits an `IOException`/`UnauthorizedAccessException`. Two or three 250 ms retries are free and would handle the transient case where a dydo guard is mid-call during cleanup.
7. **Telemetry that would have answered this in 30 seconds**: a `dydo doctor` subcommand that lists all live `dydo.exe` processes with their command lines and CWDs (via WMI on Windows, `/proc/<pid>/cwd` on Linux, `lsof -p` on macOS), cross-references them against `*/watchdog.pid` on disk, and flags mismatches.
8. **Prune orphan worktree PID files** — `dydo worktree prune` should also sweep `{worktree}/dydo/_system/.local/watchdog.pid` files whose PID is dead.

### Confidence

**High** on the primary finding (watchdog with worktree-scoped CWD is the dominant lock-holder). Triangulated from three independent evidence channels, each with a passing test:

- **Code + tests**: `EnsureRunning` has no `WorkingDirectory` (proved by Emma's `EnsureRunning_DoesNotSetWorkingDirectory_InheritsCallerCwd`); `Run` is a bare `while (true)` with no liveness/cancellation/signal handling (proved by Henry's three `WatchdogParentLivenessAbsenceTests`); `FindDydoRoot` resolves per-worktree because `_system/.local` is not junctioned (proved by Grace's `FindDydoRoot_FromInsideWorktree_ResolvesToWorktreeNotMainProject`).
- **Runtime**: a stranded `watchdog.pid` holding a dead PID (27220) inside a never-deleted worktree directory (`inquisition-worktree-system`); 15 orphan worktree directories on this machine alone.
- **User behaviour traces**: `taskkill /im dydo.exe /f` as the precondition for every `rm -rf worktrees/…` / `dotnet tool update` that previously failed.

**Medium** on secondary findings (`dydo wait` CWD lock window, `CTRL_CLOSE_EVENT` cleanup gap, git lock contribution). Code analysis supports them but they are not the primary cause.

**Not investigated in depth**: queue watchdog behaviour when queues accumulate, audit-file `.tmp` temporary-file survival after ungraceful kill, FileSystemWatcher usage (none found). Separate passes could strengthen those corners but are unlikely to change the ranking above.

### Judge rulings — Jack, 2026-04-18

#### F1 — Watchdog CWD inheritance

- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/WatchdogService.cs` (lines 36-107, 142-162 — read in full), `DynaDocs.Tests/Services/WatchdogServiceTests.cs` (Emma's test at lines 787-839, plus surrounding fixture), live verification of `dydo/_system/.local/worktrees/inquisition-worktree-system/dydo/_system/.local/watchdog.pid` (contents: `27220`; `tasklist /FI "PID eq 27220"` → "No tasks are running").
- **Independent verification:** Re-read `ProcessStartInfo` construction at `WatchdogService.cs:76-82` — confirmed `UseShellExecute`, `CreateNoWindow`, `RedirectStandardOutput`, `RedirectStandardError` are set but `WorkingDirectory` is not. Ran Emma's test via `python DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~WatchdogServiceTests.EnsureRunning_DoesNotSetWorkingDirectory_InheritsCallerCwd"` → PASSED (1/1, 111 ms).
- **Alternative explanations considered:** Could `Process.Start` silently substitute some other default when `WorkingDirectory` is empty? Checked .NET documented behaviour (empty `WorkingDirectory` → child inherits parent's CWD) — this is the documented behaviour and the Emma test captures it at spawn time. Could this be intentional ("the watchdog should follow the user's current directory")? No — the code contract is "watchdog is per-project", not "per-shell-invocation"; and the consequences (worktree lock + duplicate watchdogs per F2) contradict any plausible intent.
- **Issue:** #95

#### F2 — Per-worktree FindDydoRoot resolution spawns duplicate watchdogs

- **Judge ruling:** CONFIRMED
- **Files examined:** `Utils/PathUtils.Discovery.cs` (lines 11-17), `Services/ConfigService.cs` (`FindConfigFile` 19-28, `GetProjectRoot` 70-77, `GetDydoRoot` 82-87), `Commands/WorktreeCommand.cs` (`JunctionSubpaths` 475-481), `Services/WatchdogService.cs` (`EnsureRunning()` overload at line 34, pid-file path at lines 26-27), `DynaDocs.Tests/Services/PathUtilsWorktreeIsolationTests.cs` (Grace's test lines 40-82).
- **Independent verification:** Cross-checked `JunctionSubpaths` against the ConfigService walk: the four junctioned subpaths (`dydo/agents`, `dydo/_system/roles`, `dydo/project/issues`, `dydo/project/inquisitions`) are all *below* the worktree's top-level `dydo.json`, so `WalkUpForFile` from inside a worktree hits the worktree's `dydo.json` before the main's. Verified on-disk: `ls dydo/_system/.local/worktrees/` shows 15 stranded worktree directories; the stale pid file inside `inquisition-worktree-system` matches exactly the path Grace's test predicts.
- **Alternative explanations considered:** Is the worktree-local pid file actually benign (main watchdog still handles everything)? No — `WatchdogService.Run` uses the `FindDydoRoot()` value to scope `PollAndCleanup`/`PollOrphanedWaits` (`WatchdogService.cs:144`, `164-208`, `272-313`). A worktree-scoped watchdog sees a different `agentsDir` (the junction back to the main's agents), so it races with the main watchdog on state.md updates — the inquiry's own open question 4 flagged this as "probably benign but not analyzed in depth". Even treating the race as benign, the pid-file placement and the second watchdog process itself are real artifacts, confirmed by test and by on-disk state.
- **Issue:** #96

#### F3 — Watchdog Run() has no cancellation, parent-PID, or signal handling

- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/WatchdogService.cs` (`Run` at lines 142-162, entire file for context), `Commands/WaitCommand.cs` (greps for `GetParentPid`/`FindAncestorProcess`/`CancelKeyPress` hit at lines 76-79 and 122-125 — positive control), `DynaDocs.Tests/Services/WatchdogParentLivenessAbsenceTests.cs` (three tests).
- **Independent verification:** Read `Run` directly: `while (true) { Thread.Sleep(10_000); try { PollAndCleanup; PollQueues; PollOrphanedWaits; } catch { } }` — no cancellation path, no signal hookup, no parent liveness check. Greped for `WatchdogService.Stop` across the source tree — only `Commands/WatchdogCommand` (the explicit `dydo watchdog stop` CLI) and its tests call it, confirming no code path in normal worktree teardown triggers a graceful watchdog exit. The three Henry tests are structural (source-string grep), which is a defensible choice given that a behavioural test would itself leak a real watchdog into the test-runner host.
- **Alternative explanations considered:** Does `EnsureRunning` somehow terminate old watchdogs when spawning new ones? No — it only *starts* when no live PID is present (lines 42-53). Could the watchdog shut down on empty polls? No — `Run` has no notion of "no more work"; every tick is the same regardless of system state. Could a separate shutdown hook exist? Greped — none.
- **Issue:** #97

#### F4 — RemoveZombieDirectory diagnostic gap (original hypothesis)

- **Judge ruling:** FALSE POSITIVE (Frank's original hypothesis, as he acknowledges)
- **Files examined:** `Commands/WorktreeCommand.cs` (`RemoveZombieDirectory` at lines 604-618 — 15 lines total; uses `ex.Message` from a caught `Exception`), `DynaDocs.Tests/Commands/WorktreeCommandTests.cs` (Iris's contract test `RemoveZombieDirectory_HeldInnerFile_StderrSurfacesInnerPath_ButNotHoldingPid` at lines 1899-1930).
- **Independent verification:** Read the method — the warning is `WARNING: Could not remove directory {worktreePath}: {ex.Message}`. On Windows, the .NET runtime embeds the inner held file path directly into the `IOException.Message` for a blocked `File.Delete`. Iris's test captures this verbatim: stderr contains the full nested path `…\nested\locked-evidence-…dat`. The test also makes a stronger, positive guarantee: it asserts the PID of the holder is **not** in the message, codifying the remaining (minor) gap.
- **Alternative explanations considered:** Could Windows error formatting ever omit the inner path? Iris's test exercises the actual Windows path via a real `FileStream(..., FileShare.None)` handle; the assertion pins the current .NET / Windows behaviour, and if it ever changed, the test would fail and surface the regression — which is the intended contract.
- **Issue:** none (original hypothesis rejected; remaining "no PID of holder" gap is a nice-to-have diagnostic enhancement — explicitly deferred below).

### Judge notes on recommended follow-ups

Frank listed 8 follow-ups; I filed issues for those that are either (a) direct fixes for confirmed findings or (b) address observable residual state on disk:

- **Follow-up 1** (pin watchdog CWD): covered by #95. **Issue filed.**
- **Follow-up 2** (watchdog resolves to main project root): covered by #96. **Issue filed.**
- **Follow-up 3** (parent-PID / liveness check): covered by #97. **Issue filed.**
- **Follow-up 4** (clean up `watchdog.pid` on graceful exit): merged into the #97 resolution plan — it only becomes meaningful once a termination path exists, and is trivially added in the same `finally` block.
- **Follow-up 5** (name the holding process on `RemoveZombieDirectory` warning): **deferred.** Nice-to-have diagnostic; adds a shell-out to `handle.exe`/`lsof` that carries portability concerns of its own. Revisit if a similar inquisition recurs after #95/#96/#97 are fixed.
- **Follow-up 6** (retry with backoff in worktree cleanup): **deferred.** Speculative mitigation for a race not demonstrated to be occurring; once #95-#97 ship, the primary lock holder is gone and retries may just mask a future bug if one appears.
- **Follow-up 7** (`dydo doctor` subcommand): **deferred.** New feature rather than a bug fix; valuable, but scope separately after the root causes are fixed.
- **Follow-up 8** (prune orphan worktree pid files): covered by #98. **Issue filed** (medium severity — addresses the observed on-disk residue, particularly the 15 stranded worktrees on this machine).

### Verdict summary

- Findings reviewed: **4**
- Confirmed: **3** (F1, F2, F3)
- False positive: **1** (F4 — original hypothesis, by design of the Iris investigation)
- Inconclusive: **0**
- Issues filed: **#95, #96, #97, #98**
