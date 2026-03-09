---
type: decision
status: proposed
date: 2026-03-07
area: project
---

# 001 — Auto-Close for Dispatched Agents

Two-part mechanism (active kill from release + passive check in launch wrapper) to automatically close terminal tabs after dispatched agents release.

## Problem

In swarm scenarios, multiple agents are dispatched to tabs/windows. After each agent releases, its tab sits idle with Claude waiting for input. The human must manually check each tab and close it. With 5+ agents, this becomes tedious.

## Constraints

1. **Tab closes only after release** — never while the agent is still working
2. **Agent stays fully interactive** — can ask questions, no behavioral change
3. **Simple mechanism** — minimal flags, no polling scripts or temp files

## Rejected Approaches

| Approach | Why rejected |
|----------|-------------|
| Print mode (`-p`) | Prevents agent from asking questions |
| Remove keep-alive only | Claude never exits on its own, tab stays open |
| Background polling watcher in launch script | Complex, platform-specific shell scripting, fragile |
| Wrapper temp scripts | File management overhead, escaping nightmares |

## Decision

Two-part mechanism: **active kill from release** + **passive check in launch wrapper**.

### How it works

```
Dispatch (--auto-close)
  │
  ├─ Creates .auto-close marker in target agent's workspace
  ├─ Launches terminal with post-Claude status check (instead of exec bash)
  │
  ▼
Agent works normally (fully interactive)
  │
  ▼
dydo agent release
  │
  ├─ Releases agent (existing flow)
  ├─ Detects .auto-close marker → deletes it
  ├─ Walks process tree to find ancestor Claude process
  ├─ Spawns delayed kill (sleep 3s, then SIGTERM)
  ├─ Returns output to Claude ("Auto-close: session will close shortly.")
  │
  ▼
Claude renders final response (3 second window)
  │
  ▼
Delayed kill terminates Claude
  │
  ▼
Launch wrapper runs post-Claude check:
  if agent status is free → exit 0 → tab closes
  else → exec bash → tab stays open (safety net)
```

### Why two parts?

- **Active kill (from release)**: the primary mechanism. Makes Claude exit so the tab can close. Without this, Claude sits at the interactive prompt forever.
- **Passive check (in launch wrapper)**: the safety net. If Claude exits for ANY reason (kill worked, user hit Ctrl+C, crash), the wrapper decides whether to close the tab or keep a shell open. Tab closes only if the agent actually released.

### The 3-second delay

`dydo agent release` can't kill Claude immediately — it's called FROM Claude via the Bash tool. If we kill Claude before the tool returns, Claude never renders its final response. The delay gives Claude time to receive the tool output and render its response. After 3 seconds, the kill fires.

## Changes Required

### 1. `Commands/DispatchCommand.cs` — `--auto-close` flag

- New `--auto-close` boolean option
- Resolves effective value: CLI flag || config default
- Creates `.auto-close` marker file in target agent workspace
- Passes `autoClose: true` to `TerminalLauncher`

### 2. `Models/DispatchConfig.cs` — config default

```csharp
[JsonPropertyName("autoClose")]
public bool AutoClose { get; set; } = false;
```

### 3. `Services/TerminalLauncher.cs` — launch without keep-alive

Add `bool autoClose = false` to `Launch`, `LaunchWindows`, `LaunchMac`, all argument methods.

When `autoClose = true`, replace the keep-alive suffix with a status check:

**Linux (current):**
```bash
claude 'Brian --inbox'; exec bash
```

**Linux (auto-close):**
```bash
claude 'Brian --inbox'; if dydo agent status Brian 2>/dev/null | grep -q 'free'; then exit 0; fi; exec bash
```

**Windows (current):**
```powershell
-NoExit -Command "...; claude 'Brian --inbox'"
```

**Windows (auto-close):**
```powershell
-NoExit -Command "...; claude 'Brian --inbox'; if ((dydo agent status Brian 2>&1) -match 'free') { exit 0 }"
```

`-NoExit` keeps PS alive as the default. The explicit `exit 0` overrides it when the agent released. If the agent didn't release, `-NoExit` keeps the terminal open.

**macOS:** Same pattern as Linux, adapted for AppleScript.

### 4. `Services/ProcessUtils.cs` — ancestor process lookup

Add two methods:

```csharp
public static int? GetParentPid(int pid)
// Linux: parse /proc/{pid}/status PPid line
// Windows: NtQueryInformationProcess P/Invoke
// macOS: ps -o ppid= -p {pid}

public static int? FindAncestorProcess(string nameContains, int maxDepth = 10)
// Walk up from current process, return first PID whose name contains the string
```

### 5. `Commands/AgentCommand.cs` — auto-close on release

In `ExecuteRelease()`, after successful release:

```csharp
var workspace = registry.GetAgentWorkspace(current.Name);
var autoCloseMarker = Path.Combine(workspace, ".auto-close");
if (File.Exists(autoCloseMarker))
{
    File.Delete(autoCloseMarker);
    Console.WriteLine("  Auto-close: session will close shortly.");
    TerminalCloser.ScheduleClaudeTermination();
}
```

### 6. `Services/TerminalCloser.cs` — new, small service

```csharp
public static void ScheduleClaudeTermination()
{
    var claudePid = ProcessUtils.FindAncestorProcess("claude")
                 ?? ProcessUtils.FindAncestorProcess("node");
    if (claudePid == null)
    {
        Console.WriteLine("  Could not detect Claude process. Use Ctrl+C to close.");
        return;
    }

    // Spawn delayed kill — gives Claude 3s to render final output
    if (Windows) → powershell -NoProfile -WindowStyle Hidden "Start-Sleep 3; Stop-Process -Id {pid} -Force"
    else         → bash -c "sleep 3; kill -TERM {pid} 2>/dev/null"
}
```

### 7. Docs — `dydo/reference/dydo-commands.md`

Add `--auto-close` to dispatch options.

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| Agent releases normally | Kill fires → Claude exits → wrapper confirms → tab closes |
| Agent crashes (no release) | Claude exits → wrapper sees agent not free → `exec bash` → tab stays open |
| User Ctrl+C before release | Claude exits → wrapper sees agent not free → tab stays open |
| Kill can't find Claude PID | Fallback message printed, tab stays open (no regression) |
| `--auto-close --no-launch` | Marker created but no terminal launched. Valid: records intent in workspace |
| `--auto-close` + config default | CLI flag wins, config is the default |
| Last tab in window closes | Window closes too (expected — empty window is useless) |
| Release with 3s delay kills Claude mid-output | Possible but unlikely. 3s is generous for a release summary. Work is already saved. |

## Test Plan

### Unit: `ProcessUtilsTests.cs`

- `GetParentPid_ReturnsValidPid_ForCurrentProcess`
- `GetParentPid_ReturnsNull_ForInvalidPid`
- `FindAncestorProcess_FindsCurrentProcess_ByName` (walk up 0 levels)
- `FindAncestorProcess_ReturnsNull_WhenNotFound`
- `FindAncestorProcess_RespectsMaxDepth`

### Unit: `TerminalCloserTests.cs`

- `ScheduleClaudeTermination_SpawnsBashDelayedKill_OnLinux` (with mock)
- `ScheduleClaudeTermination_SpawnsPowerShellDelayedKill_OnWindows` (with mock)
- `ScheduleClaudeTermination_PrintsFallback_WhenClaudeNotFound` (with mock)

### Unit: `TerminalLauncherTests.cs` (additions)

**Argument generation:**
- `GetWindowsArguments_AutoClose_ContainsStatusCheck`
- `GetWindowsArguments_AutoClose_StillContainsNoExit` (keeps -NoExit for safety)
- `GetWindowsArguments_NoAutoClose_NoStatusCheck` (existing behavior unchanged)
- `GetLinuxArguments_AutoClose_ContainsStatusCheck` (×9 terminals)
- `GetLinuxArguments_AutoClose_StillContainsExecBashFallback` (×9 terminals)
- `GetLinuxArguments_AutoClose_TabMode_ContainsStatusCheck` (×3 tab-capable)
- `GetMacArguments_AutoClose_ContainsStatusCheck`

**Behavior:**
- `LaunchWindows_AutoClose_WtArgs_ContainStatusCheck`
- `LaunchWindows_AutoClose_PowerShellFallback_ContainStatusCheck`

### Integration: `DispatchCommandTests.cs` (additions)

- `Dispatch_WithAutoClose_Succeeds`
- `Dispatch_WithAutoClose_CreatesMarkerFile`
- `Dispatch_WithoutAutoClose_NoMarkerFile`
- `Dispatch_AutoClose_CombinesWithTab`
- `Dispatch_AutoClose_CombinesWithEscalate`
- `Dispatch_AutoClose_CombinesWithTo`

### Integration: `AgentLifecycleTests.cs` (additions)

- `Release_WithAutoCloseMarker_DeletesMarker`
- `Release_WithAutoCloseMarker_OutputsAutoCloseMessage`
- `Release_WithoutAutoCloseMarker_NoAutoCloseMessage`

## Scope

~100 lines of production code across 6 files. ~250 lines of tests.
