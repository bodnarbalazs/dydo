---
id: 131
area: backend
type: issue
severity: low
status: open
found-by: inquisition
date: 2026-04-28
---

# ShellProcessNames missing fish/dash/tcsh/csh/nu/ksh

## Description

**Mechanism.** `WatchdogService.ShellProcessNames` (Services/WatchdogService.cs:9-12) contains `{powershell, pwsh, bash, sh, cmd, zsh}`. Linux users running `fish`, `dash`, `tcsh`, `csh`, `nu` (Nushell), or `ksh` as their dispatcher shell would match the kill pattern (`{agent} --inbox` substring) but not be skipped, so the watchdog's kill loop would run `Process.Kill()` against the user's interactive shell.

Note: the dispatch script always uses `bash -c` explicitly (Services/TerminalLauncher.cs:73-90), so the immediate target is bash regardless of the user's shell. The risk is on alternate flows — if a user's shell ever appears in the matching argv (e.g. they run `dydo dispatch` interactively from fish, or a custom dispatch path uses the user's shell).

Low severity given the dominant shells are covered, but the fix is one line.

**Suggested fix.** Add `fish, dash, tcsh, csh, nu, ksh` to `ShellProcessNames`. (See also issue #122 / Finding #2 for the bigger gap — Linux terminal emulators.)

**Found by inquisition:** dydo/project/inquisitions/agent-deaths.md (Finding #6 — missing shells).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)