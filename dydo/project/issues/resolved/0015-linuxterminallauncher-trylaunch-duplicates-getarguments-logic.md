---
title: LinuxTerminalLauncher.TryLaunch duplicates GetArguments logic
area: backend
fix-release: 
needs-human: false
resolution: 
severity: high
status: resolved
work-type: 
id: 15
type: issue
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-07
---

# LinuxTerminalLauncher.TryLaunch duplicates GetArguments logic
Resolved high-severity duplication finding: `LinuxTerminalLauncher.TryLaunch` reimplemented the argument-construction logic already present in `GetArguments`, with drift potential between the two paths. Fixed by extracting the shared logic to `ApplyOverrides`.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed: common logic extracted to ApplyOverrides method in LinuxTerminalLauncher