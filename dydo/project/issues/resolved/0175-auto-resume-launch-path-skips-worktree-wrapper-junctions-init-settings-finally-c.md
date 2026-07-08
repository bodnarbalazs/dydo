---
title: Auto-resume launch path skips worktree wrapper (junctions, init-settings, finally cleanup)
id: 175
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-05-06
resolved-date: 2026-07-04
---

# Auto-resume launch path skips worktree wrapper (junctions, init-settings, finally cleanup)

`WindowsTerminalLauncher.GetResumeArguments` (lines 75-85) emits a much thinner script body than `GetArguments` (lines 32-69). For a worktree-resident agent, the original dispatch wraps the claude invocation in a worktree-setup-and-cleanup harness; the resume path emits none of it. Symptoms: a resumed agent in a worktree may lack expected junctions (off-limits / role-permission lookups fail); the worktree is never cleaned up by the resume tab when the agent eventually releases.

## Description

Finding #4 of inquisition `dydo/project/inquisitions/agent-crashes.md` (Brian, 2026-05-06).

**Original dispatch** (`Services/WindowsTerminalLauncher.cs:32-44, 47-54, 58-69`) emits:

```
Set-Location {wtDir};
{junction-creation};                                  // GeneratePsJunctionScript
try { dydo worktree init-settings --main-root '...' } catch ...;
Start-Sleep -Seconds 1;
try { ... claude '{prompt}' ... }
finally { Set-Location '{escapedRoot}'; dydo worktree cleanup {worktreeId} --agent {agentName} }
```

**Resume launch** (`Services/WindowsTerminalLauncher.cs:75-85 GetResumeArguments`) emits:

```
-NoExit -Command "
  $env:DYDO_AGENT='{agentName}';
  Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue;
  Start-Process -WindowStyle Hidden -FilePath dydo -ArgumentList 'wait' | Out-Null;
  claude --resume '{sessionId}' '{prompt}'
  {TerminalReset}
"
```

The resume path:

- Has no explicit `Set-Location` (relies on `wt --startingDirectory` in `LaunchResume` lines 102-104; the PowerShell-only fallback at lines 119-127 sets only `psi.WorkingDirectory` — both work, but the asymmetry is invisible to a reader).
- Does **not** recreate junctions or run `dydo worktree init-settings`. If a worktree was migrated, manually fixed, or had its junctions cleared between the original dispatch and the resume, the resumed claude lands in a worktree without the junctions it expects (no `dydo/agents/`, `_system/roles/`, etc.). Symptom: dydo guard fires on every read because off-limits / role-permission lookups can't find the role file.
- Has **no `finally { dydo worktree cleanup ... }` block.** When the resumed agent eventually releases, the worktree is never cleaned up by the resume tab. The dispatcher's tab that originally created the worktree has long since exited (that's why we resumed). The agent is gone, the worktree dir lingers; only `dydo worktree prune` periodically catches it.

**Linux/Mac variant:** `Services/LinuxTerminalLauncher.cs` `BuildResumeBashCommand` (line 53) also constructs the resume command without applying `WorktreeSetupScript` / `WorktreeCleanupScript` (which `ApplyOverrides` lines 14-21 wire in for the original dispatch). Same family of asymmetry.

## Reproduction

1. Dispatch an agent into a worktree: `dydo dispatch --worktree --role <role> --task <task> --brief "..."`.
2. After the agent has claimed and started working, force-crash its claude.
3. Watchdog auto-resumes via `LaunchResumeTerminal`.
4. Inspect the new tab's command line — no `init-settings`, no `finally { worktree cleanup ... }`.
5. Have the resumed agent release. The worktree directory at `dydo/_system/.local/worktrees/<id>/` remains until `dydo worktree prune` runs.

## Resolution

The resume launch should mirror the dispatch launch's worktree wrapper. Two options:

1. Have the resume tab read the agent's `.worktree-id` and `.worktree-path` markers and emit the same `Set-Location → init-settings → Start-Sleep → try { claude --resume ... } finally { worktree cleanup }` structure.
2. Centralise the worktree wrapper in a single reusable PowerShell-script generator and call it from both the launch and resume paths (preferred — fixes both Windows and Linux/Mac with one helper).

(Filled when resolved)

## Related

- [Decision 022 — Auto-Resume Crashed Agents](../../decisions/022-auto-resume-crashed-agents.md)
- [#0144](../0144-auto-resume-opens-in-new-window-should-reuse-the-original-window-as-a-new-tab-wh.md) — companion polish on the resume launch path.
- `Services/WindowsTerminalLauncher.cs:32-127` (`GetArguments` vs `GetResumeArguments` / `LaunchResume`).
- `Services/LinuxTerminalLauncher.cs:53-74` (`BuildResumeBashCommand`).
- `Commands/WorktreeCommand.cs` — `GeneratePsJunctionScript`, `init-settings`, `cleanup`.