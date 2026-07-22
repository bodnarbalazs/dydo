---
title: ns-13 Notion Sync Daemon (Watchdog Repurpose)
blocked-by: ns-10-live-verify-and-close
due:
needs-human: false
priority: Normal
sprint: notion-stabilization
status: done
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
   - **Remote pre-filter — server-side, O(changes) not O(corpus)** (scale requirement, balazs 2026-07-22: a doc base 100x this repo — ~40k records — must sync just as comfortably; the CHANGED volume stays roughly constant regardless of corpus size, so the tick must never scan the corpus): query each data source WITH a `last_edited_time` filter — `{"filter":{"timestamp":"last_edited_time","last_edited_time":{"on_or_after":"<cursor>"}}}` — live-verified 2026-07-22 (filter accepted; pages carry `last_edited_time`; `sorts` on the timestamp also works). A quiet tick is ONE request per type returning empty, at any corpus size. The cursor is the max SERVER stamp seen in prior results (never the local clock — no skew), compared inclusively (minute granularity: re-checking a boundary page is harmless, missing one is not; dedupe re-hits by page id + stamp). Only filter-hit pages get a body read (`GetBlockChildren`) and reconcile. A false positive (property touch, unchanged content) costs one body read and the norm-compare yields None — safe direction.
   - **Delta-only reconcile:** the tick runs the engine over the CHANGED-ID UNION only (filter hits + local mtime hits); every untouched record's base entry carries forward verbatim, its file is never parsed, its page never read. Snapshot `Save` is skipped entirely when the tick changed nothing (no write amplification at 40k entries).
   - **Deletion semantics on fast ticks (explicit design decision):** a filtered query cannot see archived pages, so fast ticks do NOT detect remote deletions. Remote deletions are detected by an **hourly census** (default every 240 ticks, `--census-interval` override): a plain id/stamp pagination of each data source — no body reads ever, since only stamp-changed pages get bodies in ANY tier — whose disappeared ids surface archives, fuse-guarded as always. The TRUE everything-reconcile stays where it belongs: manual `dydo notion sync`, run rarely and on purpose (balazs 2026-07-22: the one-time full sync is the rare case; normal use must never pay for it — at 100x a 10-minute full sweep would burn ~20% of the API budget on rare-event detection). Local deletions ARE detected every tick (the mtime sweep sees the file gone).
   - **Local pre-filter:** gate file re-parsing on mtime vs the last tick (a changed mtime with unchanged content is again a harmless norm-compare None). The stat-walk is O(corpus) but with trivial constants (~40k stats well under a second warm) — acceptable at 100x; a FileSystemWatcher push path is a recorded future optimization, not v1.
   - **Provision probe caching:** `StillValid`'s per-type retrieves run once every N ticks (e.g. 20) and immediately on any tick failure, not every tick.
   - The interactive `dydo notion sync` path keeps its current full-read behavior unless sharing the pre-filter is free — correctness of the manual path must not depend on daemon tick state.
5. Throttling: rely on `NotionClient`'s built-in 3 req/s throttle; no additional pacing needed beyond the interval floor.
6. Update `dydo/reference/dydo-commands.md` + `Templates/dydo-commands.template.md` (watchdog section), `dydo/understand/architecture.md` (Watchdog section — currently says "a stub"), and close `dydo/project/tasks/notion-sync-daemon.md` **recording the descopes explicitly in the task file**: the guard-trigger self-start from the original scope is deliberately dropped (a manually started daemon is the v1), and the interval is 15s default / 5s floor as originally sketched (an interim 60s plan-time guess was rejected and superseded — see the spec amendments). Descoped items that still matter move to a backlog note, not silence.

## Files

- `Commands/WatchdogCommand.cs`, `Services/WatchdogService.cs`
- `Sync/Notion/NotionSyncService.cs` (tick entry — reuse, don't fork)
- Docs listed above; tests: `DynaDocs.Tests/` watchdog + command tests (fake-backed tick loop with injected clock)

## Success criteria

- New tests: tick invokes one spine sync; sync failure logs and loop survives; second `start` refuses (pid); missing token refuses start; interval floor enforced; **single-flight: an overrunning tick causes the next to be skipped, never queued or overlapped** (injected-clock test); **pre-filter: a tick with no remote stamp changes and no local mtime changes issues ZERO GetBlockChildren calls** (fake call-counter assert); a stamp-changed page gets exactly its body re-read and reconciles; **scale invariance: a quiet tick's REQUEST COUNT is constant in corpus size** (fake seeded with 50 vs 5,000 records: identical request count, one filtered query per type, zero per-record calls, zero file parses for untouched records); the census detects a remote archive that fast ticks skipped (and issues zero body reads doing it).
- Manual check: `dydo watchdog start` against the configured board keeps it current across a file edit within one 15s interval.
- **Measured acceptance (live): a quiet tick completes in under 5 seconds against the real ~400-record board** — measured and recorded in notion-sync.md before this slice closes. If it does not fit, the design is wrong, not the interval.
- Full ratchet green; docs updated.
