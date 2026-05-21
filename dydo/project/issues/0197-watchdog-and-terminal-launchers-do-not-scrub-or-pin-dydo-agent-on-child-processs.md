---
id: 197
area: backend
type: issue
severity: low
status: open
found-by: inquisition
date: 2026-05-19
---

# Watchdog and terminal launchers do not scrub or pin DYDO_AGENT on child ProcessStartInfo — Windows PowerShell startup window inherits the leaked value

WatchdogService.cs:153 builds the watchdog ProcessStartInfo with no env-var manipulation, so the watchdog inherits DYDO_AGENT from its parent. When the watchdog auto-resumes an agent, WindowsTerminalLauncher.cs:80 places $env:DYDO_AGENT='{agentName}' at the start of the -Command string, but PowerShell profile scripts run before that statement executes — so any dydo invocation in a profile script sees the leaked parent value. Linux export is the first bash statement (narrower window); Mac Terminal.app is effectively immune. Defense-in-depth: pin or scrub DYDO_AGENT on every launcher ProcessStartInfo.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)