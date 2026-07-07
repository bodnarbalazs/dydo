---
title: Auto-close regression in 2.0.3: released --auto-close agent tabs no longer close (worked 2 days ago and at this session's start); regression rode in on d2a81ac's 45-line AgentRegistry.cs change from the codex host-refactor
id: 224
area: backend
type: issue
severity: high
status: open
found-by: manual
date: 2026-07-07
---

# Auto-close regression in 2.0.3: released --auto-close agent tabs no longer close (worked 2 days ago and at this session's start); regression rode in on d2a81ac's 45-line AgentRegistry.cs change from the codex host-refactor

A dispatch run WITH `--auto-close` no longer closes the agent's terminal tab when the agent releases; tabs leak and must be closed manually. **Confirmed regression** — auto-close worked 2 days ago and at the start of this session (a `--auto-close` retest closed cleanly then); it fails now on 2.0.3.

## Description

The failing retest (agent Jack, dispatched `--auto-close --tab`, told to release immediately) did NOT auto-close — ruling out a missing flag (the flag was present). So the **mechanism** regressed, not the invocation.

**Likely cause:** commit `d2a81ac` ("nuget fix among other things", part of what shipped as 2.0.3) changed `Services/AgentRegistry.cs` by **45 lines** as part of the codex **host-refactor** (`FindClaudeAncestor` → `FindAgentHostAncestor`, `--host` support, `ResolveLaunchHost`). `AgentRegistry` owns the release → auto-close handoff (`CleanupAfterRelease` sweeps the marker; the watchdog polls release + auto-close state to close the terminal). Every other recent commit to `WatchdogService`/the launchers is old; `d2a81ac`'s `AgentRegistry` change is the only fresh touch on this path. So the host-refactor most likely broke the auto-close flag persistence or the release-detection the watchdog keys on.

**Complication:** the host-refactor is *still in flight* (large uncommitted WIP touching `AgentRegistry`, `WatchdogService`, all launchers, `DispatchService`, currently non-compiling). So the fix belongs WITH the host-refactor owner (Codex), not a separate agent that would collide on `AgentRegistry`.

## Reproduction

`dydo dispatch --auto-close --tab --role docs-writer --task X --brief "onboard then immediately release"` → observe the tab does NOT close after release (watchdog leaves it open). Worked pre-2.0.3.

## Corrected diagnosis (Emma, 2026-07-07 — supersedes the codex-regression theory above)

**NOT a codex/`49b2981` regression.** Emma bisected the diff + pulled live evidence: the `AgentRegistry`/`WatchdogService` kill-path is **intact and unit-tested** (`PollAndCleanup_ProcessesRunning_KillsImmediately` etc. pass; kill whitelist covers claude/codex/node; release preserves auto-close). The tab lingers because **no watchdog is running** — `watchdog.log`'s last tick was ~6h ago (it died hard), and `watchdog.pid` still holds PID 22548, which has since been **reused by a `cmd` process**. `EnsureRunning()` (WatchdogService.cs:119–130) only checks `IsProcessRunning(pidfilePid)` — it does **not** verify the PID is actually a watchdog — so the reused PID reads as "alive" and the watchdog is **never restarted**. Every `--auto-close` dispatch after the reuse (incl. the Jack retest) silently got no watchdog → tab lingers. This is a **pre-existing PID-reuse fragility exposed once the watchdog died for any reason**, not the codex diff.

## Resolution

Emma implementing: (1) harden `EnsureRunning` to confirm the pidfile PID is a dydo watchdog-run process (command-line match) before skipping restart, + regression test (reused non-watchdog PID → restart); (2) immediate remediation — clear the stale pidfile so a watchdog restarts now. **Related but distinct:** the general-wait intermittent `exit 1` (my flaky wait) *is* a genuine `49b2981` regression — the host-liveness check now walks `FindAgentHostAncestor`→`FindClaudeAncestor`, which matches transient `node` ancestors of the backgrounded wait shell; fix = anchor `WaitCommand` to persisted `session.ClaimedPid`. Both fixed under task `fix-224-autoclose-wait-regression`.
