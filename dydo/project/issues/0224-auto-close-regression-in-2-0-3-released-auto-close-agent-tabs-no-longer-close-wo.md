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

## Resolution

Route to the host-refactor owner (Codex): the completed host-refactor must **restore working auto-close** and **add a regression test** (a released `--auto-close` agent closes its terminal). Bisect `d2a81ac`'s `AgentRegistry.cs` delta against the auto-close flow to find the exact break.
