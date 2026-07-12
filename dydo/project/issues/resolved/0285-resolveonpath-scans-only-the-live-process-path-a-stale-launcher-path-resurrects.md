---
title: ResolveOnPath scans only the live process PATH; a stale launcher PATH resurrects the #0227 bare-codex launch failure and bypasses preflight
id: 285
area: backend
type: issue
severity: high
status: resolved
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-12
resolved-date: 2026-07-12
---

# ResolveOnPath scans only the live process PATH; a stale launcher PATH resurrects the #0227 bare-codex launch failure and bypasses preflight

Regression of resolved #0227 via environment, not code: dispatched codex tabs fail with 'codex not recognized' when the orchestrator session's inherited PATH predates the codex bin dir, even though it is on the persisted user PATH.

## Description

Regression of resolved #0227 (fixed de0d63f, 2026-07-08). NOT a code regression — same CLI binary — an environmental trigger exposes a coverage gap in the #0227 fix. Every codex tab dispatched from the current orchestrator session died with `codex : The term 'codex' is not recognized` (5/5 in a wave, 2026-07-12).

## Root cause

`TerminalLauncher.ResolveOnPath(command)` resolves the codex executable to an absolute path by scanning **only the launcher process's live `PATH`** (`Environment.GetEnvironmentVariable("PATH")`, PATHEXT-aware). This works only when the dydo process that builds the launch command already has the codex bin dir on its inherited PATH.

On this workstation the launchable codex CLI lives at `%LOCALAPPDATA%\Programs\OpenAI\Codex\bin\codex.exe` (a symlink to `~/.codex/packages/standalone/current/bin`), and that dir **is** present in the persisted **User** PATH (added 2026-07-07). But the current orchestrator session's process tree inherited a **stale PATH snapshot** taken before that entry existed, so `ResolveOnPath` finds nothing on the live PATH.

Two compounding gaps:
1. **Live-PATH-only resolution.** `ResolveOnPath` never consults the persisted User/Machine PATH (`[Environment]::GetEnvironmentVariable('PATH','User'|'Machine')`) nor any known install location. A stale launcher PATH defeats the whole #0227 fix even though codex is installed and on the persisted PATH.
2. **Silent bare-name fallback bypasses preflight.** On a miss `ResolveOnPath` does `return command;` (bare `codex`). Only the WindowsApps-alias branch throws; the genuine not-found case returns a string, so DispatchService's "pre-flight resolution and fail before inbox/state mutation" (the other half of the #0227 fix) does NOT fire. Dispatch reports success, writes the inbox, launches the tab — and the child PowerShell (also stale PATH) dies on bare `codex`.

This is why it "worked before with no version change": #0227 was verified 2026-07-08 in a shell that had the bin on its live PATH; the current session predates/omits it. Same binary, different environment.

## Fix

1. `ResolveOnPath`: when the live-PATH scan misses, fall back to the persisted **User + Machine** PATH, and/or probe the known install dir (`%LOCALAPPDATA%\Programs\OpenAI\Codex\bin`). Return the absolute path so the child is PATH-independent (the original #0227 intent).
2. A genuine resolution miss must **fail the codex dispatch preflight** with an actionable error (before any inbox/state mutation) — never emit bare `codex`. Fold into `DispatchPreflight` alongside the existing WindowsApps-alias rejection.
3. Tests: resolution succeeds when codex is absent from the process PATH but present in the persisted User PATH / known install dir; preflight FAILS (no inbox written) when codex is truly unresolvable.

## Workaround (in effect this session)

Prefix every `dydo dispatch --codex` shell with `export PATH="$PATH:/c/Users/User/AppData/Local/Programs/OpenAI/Codex/bin"` so dydo's `ResolveOnPath` finds codex.exe and bakes the absolute path into the launch command — verified to launch correctly. Retire once the fix above lands.

## Related
- Resolved #0227 (the incomplete fix) — `Services/TerminalLauncher.cs` `ResolveOnPath` / `GetLaunchExecutable`, `Services/DispatchPreflight.cs`.
- Belongs to the codex-infra cluster (TerminalLauncher.cs).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

RESOLVED 2026-07-12 (landed 2ad3d325). TerminalLauncher.ResolveOnPath now falls back, when the executable is absent from the launcher live process PATH, to the persisted Windows User then Machine PATH, then a codex install-dir probe (LocalAppData Programs/OpenAI/Codex/bin) - all PATHEXT-aware, WindowsApps-alias rejected across every source, OS-guarded so Linux CI is unaffected. A genuine codex total-miss now throws so DispatchPreflight fails BEFORE inbox/state mutation (no more doomed bare-codex tab); claude bare-fallback preserved. Fixes the stale-launcher-PATH regression of 0227 that bricked codex dispatch this session. Codex Brian (Terra, 1 round - all 8 vectors covered first pass; DR-037 test-front-loading worked) plus a code-simplifier CC 32-to-6 refactor of ResolveOnPath (cleared the CRAP gate). Adversarially reviewed PASS. NOTE: the export-PATH dispatch workaround stays needed until a binary with this fix is installed; recommended persisted-alias guard test tracked in 0289.