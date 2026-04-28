---
id: 124
area: backend
type: issue
severity: high
status: open
found-by: inquisition
date: 2026-04-28
---

# Windows launcher silently closes terminal when claude exits non-released

## Description

**Mechanism.** `WindowsTerminalLauncher.GetArguments` (Services/WindowsTerminalLauncher.cs:11-19) builds the PowerShell command as:

```
{noExitFlag}-Command "...claude '{prompt}'; if ((dydo agent status {agent}) -match 'free') { exit 0 }"
```

When `autoClose=true`, `noExitFlag` is empty (no `-NoExit`), and the if-branch has no `else`. PowerShell's `-Command` mode exits when the script completes. So if claude exits without the agent being free — watchdog kill, `/exit`, network drop, context-limit, crash — the if-condition is false, the script ends, and the terminal closes silently with no error and no diagnostic shell.

Compare with the Linux equivalent (`Services/TerminalLauncher.cs:134-135`):

```
if dydo agent status {agent} | grep -q 'free'; then exit 0; fi; exec bash
```

Linux falls back to `exec bash` so the terminal stays open with output if claude exits non-released. Windows has no such fallback.

This is the *diagnosability* layer of agent deaths: even when the underlying cause is finding #1 or #2, the user sees nothing because the terminal vanishes silently. This issue is independent of those root causes.

**Suggested fix.** Mirror the Linux pattern: add an `else { ... }` branch (e.g. `Write-Warning ...; pwsh`) so the terminal stays open with a diagnostic shell on non-free exit.

**Found by inquisition:** dydo/project/inquisitions/agent-deaths.md (Finding #4).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)