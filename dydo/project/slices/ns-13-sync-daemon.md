---
title: ns-13 Notion Sync Daemon (Watchdog Repurpose)
blocked-by: ns-10-live-verify-and-close
due:
needs-human: false
priority: Normal
sprint: notion-stabilization
status: in-progress
work-type: feature
area: backend
type: context
---

# ns-13 Notion Sync Daemon (Watchdog Repurpose)

The promised DR-041 repurpose (scope sketch in `dydo/project/tasks/notion-sync-daemon.md`): `dydo watchdog` is an inert stub (`Commands/WatchdogCommand.cs`, `Services/WatchdogService.cs` — "awaiting Notion-sync repurpose"). Turn it into the background sync loop so the board stays current without manual `dydo notion sync`. **Cuttable: if the sprint runs long, this slice moves to the next sprint without failing the gate.** Runs only after ns-10 proves the sync stable — a daemon multiplies whatever behavior exists.

## Task

**Honest baseline (plan-gate verified):** no pid-file plumbing exists anywhere — `Services/WatchdogService.cs` is an empty class; only `Services/WatchdogLogger.cs` (log path) survives. This slice builds the process lifecycle from scratch, to this locked spec:

- **Pid file:** `dydo/_system/.local/watchdog.pid` (gitignored `.local`, same convention as sync state). Content: the process id, one line.
- **Stale detection:** on `start`, if the pid file exists, probe liveness (`Process.GetProcessById` in try/catch); dead pid ⇒ delete file and proceed; live pid ⇒ refuse with a message naming the pid.
- **Detached spawn:** `start` launches `dydo watchdog run [--interval n]` via `ProcessStartInfo` with `UseShellExecute = false`, `CreateNoWindow = true`, current executable path resolved from `Environment.ProcessPath`; `run` writes its own pid file on entry and deletes it on clean exit.
- **Stop:** read pid → `Process.Kill` (try/catch, dead is fine) → delete pid file.

1. `dydo watchdog run` (foreground loop): every interval, execute one spine sync tick (`NotionSyncService` with default scope — spine only) against the configured parent, **single-flight**: a tick that overruns the interval blocks the next from starting — skipped, never queued — so ticks cannot overlap or pile up (in-process re-entrancy guard; the pid file covers cross-process).
2. Defaults and flags: **15s interval** (the product target: board updates feel instant when switching contexts — balazs, 2026-07-22, superseding the sprint plan's 60s guess), `--interval <seconds>` override (floor 5s); single instance per the pid spec above; token/parent resolved exactly like `dydo notion sync` — **missing config: daemon refuses to start with a clear message** (not a silent idle loop).
3. Each tick logs one summary line (created/updated/archived/conflicts/fuse-trips) to the existing watchdog log path; a tripped deletion fuse or API failure logs loudly and the loop continues (next tick retries) — the daemon never dies on a sync error, only on config errors.
4. **Cheap ticks are CORE SCOPE, not optional** (supersedes the earlier 'consider if cheap' note): 99%+ of ticks find nothing changed and must not read or download record bodies to discover that.
   - **Remote pre-filter:** `QueryDataSource` already returns every page's `last_edited_time` in the paginated query (~10-12 requests for the whole board). Store the stamp per page at each tick; only pages whose stamp is newer (INCLUSIVE compare — Notion stamps are minute-granular, an edit may share the stored minute; re-checking a page is harmless, missing one is not) get a body read (`GetBlockChildren`) and full reconcile. A false positive (property touch, unchanged content) costs one body read and the norm-compare yields None — safe direction.
   - **Local pre-filter:** gate file re-parsing on mtime vs the last tick (a changed mtime with unchanged content is again a harmless norm-compare None).
   - **Provision probe caching:** `StillValid`'s per-type retrieves run once every N ticks (e.g. 20) and immediately on any tick failure, not every tick.
   - The interactive `dydo notion sync` path keeps its current full-read behavior unless sharing the pre-filter is free — correctness of the manual path must not depend on daemon tick state.
5. Throttling: rely on `NotionClient`'s built-in 3 req/s throttle; no additional pacing needed beyond the interval floor.
6. Update `dydo/reference/dydo-commands.md` + `Templates/dydo-commands.template.md` (watchdog section), `dydo/understand/architecture.md` (Watchdog section — currently says "a stub"), and close `dydo/project/tasks/notion-sync-daemon.md` **recording the descopes explicitly in the task file**: the guard-trigger self-start from the original scope is deliberately dropped (a manually started daemon is the v1), and the ~15s interval became a 60s default with a 15s floor. Descoped items that still matter move to a backlog note, not silence.

## Files

- `Commands/WatchdogCommand.cs`, `Services/WatchdogService.cs`
- `Sync/Notion/NotionSyncService.cs` (tick entry — reuse, don't fork)
- Docs listed above; tests: `DynaDocs.Tests/` watchdog + command tests (fake-backed tick loop with injected clock)

## Success criteria

- New tests: tick invokes one spine sync; sync failure logs and loop survives; second `start` refuses (pid); missing token refuses start; interval floor enforced; **single-flight: an overrunning tick causes the next to be skipped, never queued or overlapped** (injected-clock test); **pre-filter: a tick with no remote stamp changes and no local mtime changes issues ZERO GetBlockChildren calls** (fake call-counter assert); a stamp-changed page gets exactly its body re-read and reconciles.
- Manual check: `dydo watchdog start` against the configured board keeps it current across a file edit within one 15s interval.
- **Measured acceptance (live): a quiet tick completes in under 5 seconds against the real ~400-record board** — measured and recorded in notion-sync.md before this slice closes. If it does not fit, the design is wrong, not the interval.
- Full ratchet green; docs updated.
