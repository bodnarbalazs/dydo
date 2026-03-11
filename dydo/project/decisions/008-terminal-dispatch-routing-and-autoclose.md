---
type: decision
status: accepted
date: 2026-03-11
area: project
supersedes: [001, 004]
---

# 008 — Terminal Dispatch Routing and Auto-Close Watchdog

GUID-based window routing for correct tab placement, and a global stateless watchdog for auto-closing released agent tabs.

## Problem

Two bugs in terminal dispatch:

1. **Tab routing**: `wt -w 0 new-tab` opens tabs in the most-recently-focused window, not the dispatching agent's window. This happens because `Process.Start` with `UseShellExecute = true` launches `wt.exe` as a detached process — it has no WT window context, so `-w 0` falls back to MRU. In multi-window workflows (slice A in window 1, slice B in window 2), tabs get entangled across windows.

2. **Auto-close**: The post-claude status check (`; if (dydo agent status X) -match 'free') { exit 0 }`) runs sequentially AFTER `claude` exits. But `claude` is an interactive CLI that doesn't exit when the agent releases — it stays at the prompt. The check never fires.

## Decision

### Part 1 — GUID-Based Window Routing

Replace `-w 0` with named windows using opaque GUIDs.

**Window creation (`--new-window` dispatch):**
```
wt --window a3f7b2c1 new-tab pwsh -Command "$env:DYDO_WINDOW='a3f7b2c1'; ..."
```

**Tab creation (`--tab` dispatch):**
```
wt -w a3f7b2c1 new-tab pwsh -Command "$env:DYDO_WINDOW='a3f7b2c1'; ..."
```

The dispatching agent reads `$env:DYDO_WINDOW` from its own environment (inherited from the shell that launched it) and passes it to `wt -w`. Child agents inherit the same env var.

**Window ID lifecycle:**
- Generated as a short GUID (`Guid.NewGuid().ToString("N")[..8]`) at `--new-window` dispatch time
- Stored in target agent's `.state` file as `windowId`
- Set as `$env:DYDO_WINDOW` in the spawned shell command
- Survives agent release (`.state` is updated, not deleted)
- Overwritten on next claim (fresh state)

**Fallback:** If `DYDO_WINDOW` is unset (user's original terminal, manual launch), `--tab` falls back to `-w 0`. Acceptable for the root case where the user is typically focused on their own window.

**Why GUIDs, not agent names:**
Agent identities are reclaimed — Charlie finishes slice A2, releases, then gets dispatched to C3 in a different window. Name-based window IDs would collide. GUIDs are opaque and collision-free. The user doesn't see them (they're internal WT routing tokens, not tab titles). Claude's green-star attention indicator on tabs is unaffected — that's the tab title, not the window name.

### Part 2 — Global Auto-Close Watchdog

Replace the two-part mechanism from decision 001 (active kill from release + passive wrapper check) with a single global watchdog process.

**Why supersede decision 001:**
- Process tree walking (`FindAncestorProcess`) is complex and platform-specific (PPid parsing on Linux, NtQueryInformationProcess P/Invoke on Windows, `ps` on macOS)
- The 3-second delayed kill has timing edge cases
- Each release spawns a separate delayed-kill process — with 10+ agents, that's 10+ fire-and-forget kills
- A single watchdog is simpler, centralized, and self-healing

**Watchdog design:**

```
dydo watchdog start   → spawns background process (if not already running)
dydo watchdog stop    → kills it via PID file
dydo watchdog run     → hidden subcommand: the actual polling loop
```

`start` is called automatically by `DispatchCommand` when `--auto-close` is set. Users never need to know the watchdog exists. Power users get `start/stop` for manual control.

**The loop (every 10 seconds):**
```
for each agent in dydo/agents/*/:
    read .state
    if autoClose == true AND status == free:
        find process where command line contains "{agentName} --inbox"
        kill it
```

**Process discovery:**
- Cross-platform via .NET `Process.GetProcesses()` + command line inspection
- Windows: `Process.GetProcesses()` then WMI/CIM for command line (`Win32_Process.CommandLine`)
- Linux/macOS: read `/proc/{pid}/cmdline` or `ps -eo pid,args`
- Match pattern: `claude.*{agentName} --inbox` — unique per agent

**Tab closure chain:**
Watchdog kills claude process → PowerShell command finishes (no `-NoExit` when `autoClose`) → tab closes. No separate tab-closing logic needed.

**Coordination:**
- PID file at `.dydo/watchdog.pid` — `start` checks if process is alive before spawning
- Idempotent: multiple `start` calls are safe
- Stateless: no internal state, reads `.state` files each cycle
- Self-healing: if watchdog crashes, next dispatch restarts it

**Shutdown:** Watchdog exits when its process is killed (manual `dydo watchdog stop`, system shutdown, or terminal close). No timeout needed — it's lightweight (one poll every 10 seconds).

## State File Changes

Add two fields to agent `.state`:

```json
{
  "agent": "Charlie",
  "status": "working",
  "windowId": "a3f7b2c1",
  "autoClose": true
}
```

Both survive release (state file is updated in place, not deleted). Both are overwritten on next claim.

## Shell Command Templates

**Windows — new window with auto-close:**
```
wt --window {guid} new-tab pwsh -Command "$env:DYDO_WINDOW='{guid}'; Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue; claude '{Agent} --inbox'"
```

**Windows — tab in existing window:**
```
wt -w {guid} new-tab pwsh -Command "$env:DYDO_WINDOW='{guid}'; Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue; claude '{Agent} --inbox'"
```

**Linux (gnome-terminal example) — tab:**
```
gnome-terminal --tab -- bash -c "export DYDO_WINDOW='{guid}'; cd '/path' && unset CLAUDECODE; claude '{Agent} --inbox'; exec bash"
```

**macOS — new window:**
```
osascript -e 'tell app "Terminal" to do script "export DYDO_WINDOW={guid}; cd /path && unset CLAUDECODE; claude \"{Agent} --inbox\""'
```

Omit `-NoExit` (Windows) / replace `exec bash` with `exit 0` (Linux/macOS) when `autoClose` is active. The watchdog handles the actual termination; the launch wrapper just ensures clean exit when claude eventually dies.

## Changes Required

| File | Change |
|------|--------|
| `Services/TerminalLauncher.cs` | Add `windowName` parameter; generate GUID for new windows; use `--window` / `-w` with GUID; inject `DYDO_WINDOW` env var; remove `-NoExit` when autoClose |
| `Commands/DispatchCommand.cs` | Read `DYDO_WINDOW` from env; pass to launcher; store `windowId` and `autoClose` in target agent `.state`; call `dydo watchdog start` on first auto-close dispatch |
| `Models/AgentState.cs` | Add `WindowId` and `AutoClose` properties |
| `Services/AgentRegistry.cs` | Persist new state fields; ensure they survive release |
| New: `Commands/WatchdogCommand.cs` | `start`, `stop`, `run` subcommands |
| New: `Services/WatchdogService.cs` | Polling loop, process discovery, kill logic |
| `Services/ProcessUtils.cs` | Add cross-platform command-line inspection for process matching |

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| `--tab` without `DYDO_WINDOW` set | Falls back to `-w 0` (MRU) — same as current behavior |
| Agent releases, watchdog not running | Tab stays open. Next dispatch restarts watchdog, catches it on next poll |
| Watchdog crashes | Next dispatch restarts it. Released agents caught on first poll |
| Multiple agents release simultaneously | Watchdog handles all in one poll cycle |
| Agent identity reclaimed before watchdog acts | New claim overwrites `.state` with `autoClose: false` (or new value) — watchdog skips it |
| Claude process not found (already exited) | No-op. Idempotent |
| Last tab in window closes | Window closes too (WT default behavior, expected) |
| GUID collision | Negligible probability with 8-char hex (4 billion possibilities) |

## Relationship to Previous Decisions

- **Supersedes 001** (Auto-Close for Dispatched Agents): replaces two-part kill mechanism with global watchdog. Simpler, no process tree walking, no delayed kills.
- **Supersedes 004** (Terminal Grouping): refines `-w 0` approach with GUID-based window routing. The `dydo agent tree` command from 004 is unaffected.
