---
title: Watchdog Autostart Lease
area: general
name: watchdog-autostart-lease
status: done
created: 2026-07-23T15:26:38.7322406Z
assigned: unassigned
---

# Task: watchdog-autostart-lease

Guard-triggered watchdog auto-start with activity-lease expiry. Successor to the v1 descope
recorded in [notion-sync-daemon.md](notion-sync-daemon.md) ("must be started on purpose, not
silently by a hook — revisit if manual start proves a friction point"); balazs named that
friction on 2026-07-23. Design sketched in conversation, not yet scheduled or committed to.

Proposed shape (auto-start and expiry are complements — neither is safe alone):

- **Lease model.** The guard hook, via the throttled-stamp pattern (same as daily validation /
  model-cap restore), refreshes an activity stamp at most once per few minutes and starts the
  daemon if none is running. The daemon checks the stamp each tick and exits cleanly after ~1h
  of no refresh. Net: the daemon runs exactly while someone works in the project, dies an hour
  after work stops, resurrects on the next session's first tool call.
- **Guard stays fast.** The per-call cost must be a throttled stat of a stamp file, never a
  process probe on every tool call.
- **Stop means stop.** `dydo watchdog stop` writes a suppress marker that auto-start honors
  until the next manual `watchdog start` (which clears it). A crashed daemon leaves no marker,
  so crashes still self-heal.
- **Preconditions.** Auto-start only when Notion is connected (token + provisioned board) and
  the deletion fuse is not tripped (a tripped fuse awaits a human decision; silently restarting
  sync is the exact hazard the v1 descope worried about).
- **Lease refresh source: guard-only** (not board activity). A teammate's board edits made
  while you're idle sync on your next session's first tick — the delta cursor makes catch-up
  lossless, just deferred. Simpler, and provably correct.

## Progress

- [x] Activity stamp + throttled auto-start trigger in the guard (`GuardCommand.AutoStartWatchdogIfDue`, one stat on the hot path, wrapped so it can never break/block the guard)
- [x] `WatchdogService.AutoStart` — quiet, preconditioned spawn (no suppress marker, Notion configured via the same `DaemonConfigError` gate, prior provision evidence via `HasProvisionEvidence`, no live pid)
- [x] Suppress marker: `Stop` writes `watchdog.hold` FIRST (before kill/pid-delete); manual `Start` clears it; auto-start honors it; the `Run` loop re-checks it each tick and exits (`hold_honored`) so a racing/kill-failed stop still wins
- [x] Lease expiry in the `Run` loop (`max(stamp, process-start baseline)`, `--lease` on `start`/`run`, `--lease 0` disables, negatives rejected at parse time), `WatchdogLogger.LogLeaseExpired`, injectable `utcNow`/stamp seams
- [x] Detached spawn does not inherit the guard hook's stdio (Windows `ShellExecuteEx`; POSIX fresh-pipe redirect) — empirically verified, prevents the first-tool-call hang
- [x] Tests: lease matrix, hold-marker flow (incl. mid-run + kill-throws), negative-lease rejection, guard auto-start (fresh/stale/missing stamp, live pid, hold, not-connected, no-provision, swallowed exception)
- [x] Docs: `dydo/reference/notion-sync.md` daemon section

## Files Changed

- `Services/WatchdogService.cs` — `AutoStart` + `HasProvisionEvidence`, `Spawn` (no-stdio-inheritance detach), stamp/hold helpers, lease + per-tick hold check in `Run` (`leaseMinutes` + `utcNow` + `ActivityStampReadOverride` seams), `Stop` writes the hold marker first, `Start` clears it and touches the stamp, `DefaultLeaseMinutes`
- `Services/WatchdogLogger.cs` — `LogLeaseExpired`/`lease_expired` + `LogHoldHonored`/`hold_honored` events
- `Commands/GuardCommand.cs` — `AutoStartWatchdogIfDue` (single-stat hot path), called from `Decide` after the model-cap restore
- `Commands/WatchdogCommand.cs` — `--lease` option on `start` and `run` (rejects negatives), forwarded to the daemon
- `DynaDocs.Tests/Services/WatchdogServiceTests.cs` — lease + hold-marker tests (mid-run, kill-throws); argv assertion updated for `--lease`
- `DynaDocs.Tests/Services/WatchdogAutoStartTests.cs` — new; `AutoStart` preconditions (incl. provision-evidence gate) + guard throttle/exception tests
- `DynaDocs.Tests/Commands/WatchdogCommandTests.cs` — `--lease` exposure + negative/zero-lease parse validation
- `DynaDocs.Tests/Integration/IntegrationTestBase.cs` — deliberately neuter the in-process guard's real daemon spawn (`SpawnOverride` no-op, saved/restored) so integration-test hermeticity is intentional, not a coincidence of missing provision evidence
- `dydo/reference/notion-sync.md` — auto-start + lease + hold-marker behavior
- `dydo/project/issues/0213-*.md` — recorded the third `cwd + "dydo"` call site (the activity stamp) for that issue's cleanup sweep

## Review Summary

Fresh-eyes review PASS (2026-07-23, 2 rounds). Round 1: 1 medium + 3 low + 2 nits — the medium
(spawned daemon inherits the guard hook's stdio pipes → the triggering tool call stalls until hook
timeout; retroactively explains the ns-13 "orphaned 1.5h soak" wrapper hang) was empirically
reproduced in an isolated harness (EOF lagged guard exit by 33.4s) and fixed with the ShellExecuteEx
spawn path (EOF at guard exit, -3ms); stop-vs-autostart race closed by hold-first ordering + the
per-tick hold check. Round 2 (remediation verify): PASS, no new defects; suite independently green
(2507 passed / 10 live-skips). Residuals accepted and recorded below. WindowStyle=Hidden verified
on this host (spawned console child: MainWindowHandle=0, no visible window).

## Accepted residuals (recorded, non-blocking)

- **One tick past stop.** A daemon spawned inside the stop race window completes exactly one sync
  tick before noticing the hold marker (the check is post-tick). Bounded, and the marker guarantees
  it exits after that tick.
- **POSIX refusal-path exit codes.** A detached daemon's config-error / already-running refusal
  lines write to a reader-less stdout pipe on POSIX → EPIPE crash instead of the intended exit
  code. Cosmetic: exit codes of a detached process nobody observes. The Windows path (ShellExecuteEx)
  is unaffected.
- **Guard root resolution** stays `cwd + "dydo"` for sibling consistency — third instance recorded
  in issue 0213 for that cleanup sweep.

## Design note for review

**Env-reach hazard — resolved.** Precondition (b) "Notion connected" reuses `NotionSyncService.DaemonConfigError` verbatim, and that gate accepts a parent page resolved from the `DYDO_NOTION_PARENT_PAGE` env var / Windows User-registry fallback, not only from the project's own `dydo.json`. Left alone, that would let the guard auto-start a daemon for **any** dydo project on a machine with global Notion env vars. Closed by precondition (d): `WatchdogService.AutoStart` now also requires on-disk provision evidence — at least one `provision*.json` in `dydo/_system/.local/notion/` (`WatchdogService.HasProvisionEvidence`, written only after a successful provision/sync in this project). An implicit trigger therefore only ever *resumes* a sync explicitly begun here; it never initiates first contact. Manual `dydo watchdog start` is unchanged (its env reach is fine — it is explicit).

The deletion-fuse precondition from the original sketch was **not** added: no cheap persisted fuse-state was found to gate on without a probe, and the census path is already fuse-guarded; flagging rather than inventing one. Worth a look before this ships.