---
id: 174
area: backend
type: issue
severity: high
status: resolved
found-by: inquisition
date: 2026-05-06
resolved-date: 2026-07-04
---

# Worktree-claimed agent registers watchdog anchor in worktree dydo root; main watchdog cannot see it

`AgentRegistry.SetupAgentWorkspace` writes the watchdog anchor under `_configService.GetDydoRoot(_basePath)`, which inside a worktree resolves to the **worktree's** `dydo/_system/.local/watchdog-anchors/`. The watchdog itself runs against `PathUtils.FindMainDydoRoot()` and reads only the **main** anchors directory. Worktree-claimed leaf agents are therefore invisible to the running watchdog. Once all main-resident anchors die, the watchdog exits via `anchor_gone` even though leaf agents in worktrees are still working — the same family as #0154 but a Windows-specific manifestation that #0154's claim-time anchoring fix did not close.

## Description

Finding #2 of inquisition `dydo/project/inquisitions/agent-crashes.md` (Brian, 2026-05-06).

**Root cause:** asymmetry between the two `RegisterAnchor` callsites.

- `Services/WatchdogService.cs:101-105` `EnsureRunning()` resolves `mainDydoRoot = PathUtils.FindMainDydoRoot() ?? "."`. `FindMainDydoRoot` is documented (`Utils/PathUtils.Discovery.cs:42-52`) as "Used by the watchdog so its PID file and CWD never land inside a worktree."
- `Services/AgentRegistry.cs:415-421` (the claim-time anchor write added to close #0154) calls `_configService.GetDydoRoot(_basePath)`. `_basePath` defaults to `PathUtils.FindProjectRoot() ?? Environment.CurrentDirectory` (constructor at line 39), and inside a worktree the search hits the worktree's `dydo.json` (worktrees carry one — they are working trees of the same repo), so `GetDydoRoot` returns the worktree's `dydo/`.

`dydo/_system/.local/` is **not** in the worktree-junction list (architecture.md §Worktree Dispatch says only `agents/`, `_system/roles/`, `project/issues/`, `project/inquisitions/` are junctioned), so the two anchor directories do not share state.

**Live filesystem proof, captured during this investigation:**

- Main: `dydo/_system/.local/watchdog-anchors/57332.anchor` (Adele's claude — claimed in main).
- Worktree (this inquisition): `dydo/_system/.local/worktrees/investigate-agent-crashes/dydo/_system/.local/watchdog-anchors/67232.anchor` (Brian) and `69896.anchor` (Charlie, this judge — claimed in the worktree).

The watchdog's `Run()` loop (`WatchdogService.cs:305-326`) reads only main's anchors directory. When all main-resident anchors die, `liveAnchorCount == 0` triggers `exitReason = "anchor_gone"` and the watchdog exits even though leaf agents in the worktree are still working — closing the auto-resume coverage on those agents.

**Why this is not closed by #0154's fix:** #0154's resolution wired `RegisterAnchor` into `SetupAgentWorkspace` so dispatched agents anchor themselves on claim. That fix lands the anchor in the wrong directory whenever the claim happens inside a worktree. The Windows variant goes silently undetected because the orphan-cap (`MaxOrphanAge = 24h`) keeps the watchdog alive long enough that the symptom only surfaces after a long-running session.

## Reproduction

1. From main: `dydo dispatch --worktree --role <role> --task <task> --brief "..."` for a leaf agent (e.g. Brian).
2. Inside the worktree the dispatched agent runs `dydo agent claim Brian`.
3. `ls dydo/_system/.local/watchdog-anchors/` (main) — does not contain Brian's anchor.
4. `ls dydo/_system/.local/worktrees/<id>/dydo/_system/.local/watchdog-anchors/` — Brian's anchor is here.
5. Have all main-resident dispatchers release. Watchdog logs `"event":"exit","reason":"anchor_gone"`. Brian's claude is still alive but no longer covered.

## Resolution

`Services/AgentRegistry.cs:417` should resolve to the **main** dydo root. Two equivalent fixes:

1. Replace `var dydoRoot = _configService.GetDydoRoot(_basePath);` with `var dydoRoot = PathUtils.FindMainDydoRoot();` — mirroring `WatchdogService.EnsureRunning`.
2. Route both callsites (and any future ones) through a single helper (e.g. `WatchdogService.GetMainAnchorsDir()`) so the rule is enforced by construction.

Prefer option 2: this is the third site (`EnsureRunning`, `GetAnchorsDirPath`, `RegisterAnchor`) where main-vs-worktree resolution must be respected; a missed callsite is the kind of bug this issue documents.

(Filled when resolved)

## Related

- [#0151](0151-watchdog-never-registers-anchors-on-windows-orphan-cap-is-the-only-thing-keeping.md) — Windows variant of "anchor never registered." This issue is the worktree variant where the anchor IS registered, but to the wrong dir.
- [#0154](0154-linux-mac-watchdog-dies-via-anchor-gone-when-all-dispatchers-exit-while-leaf-dis.md) — Linux/Mac variant of "watchdog exits via anchor_gone while leaf agents are alive." Same exit mechanism; this issue describes a Windows path to the same `anchor_gone` symptom that #0154's fix did not close on worktree dispatches.
- `Services/AgentRegistry.cs:415-421`, `Services/WatchdogService.cs:101-106, 305-326`, `Utils/PathUtils.Discovery.cs:42-52`.