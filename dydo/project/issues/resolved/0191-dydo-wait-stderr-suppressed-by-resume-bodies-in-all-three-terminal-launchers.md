---
title: dydo wait stderr suppressed by resume bodies in all three terminal launchers
id: 191
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-05-19
resolved-date: 2026-07-04
---

# dydo wait stderr suppressed by resume bodies in all three terminal launchers

When a duplicate-wait refusal or any other ExitCodes.ToolError path fires inside dydo wait, its stderr explanation is dropped because the auto-resume bodies redirect stderr (Linux/Mac: '>/dev/null 2>&1', Windows: '| Out-Null'). The operator sees only the exit code with no diagnostic, which is exactly the 'exit code 2, no useful stderr' symptom in issue #0183.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Outdated: resume bodies no longer spawn a backgrounded 'dydo wait' at all (removed with the #0207 rework — WindowsTerminalLauncher.cs:96-101, LinuxTerminalLauncher.cs:60), so there is no suppressed dydo-wait stderr. Triage sweep 2026-07-04 (Brian, CoS).