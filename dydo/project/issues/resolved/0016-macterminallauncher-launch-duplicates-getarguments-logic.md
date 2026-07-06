---
title: MacTerminalLauncher.Launch duplicates GetArguments logic
area: backend
fix-release: 
needs-human: false
resolution: 
severity: high
status: resolved
work-type: 
id: 16
type: issue
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-07
---

# MacTerminalLauncher.Launch duplicates GetArguments logic
Resolved high-severity duplication finding: `MacTerminalLauncher.Launch` reimplemented argument-construction logic from `GetArguments`. Fixed by extracting the shared logic to `BuildShellComponents`.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed: common logic extracted to BuildShellComponents method in MacTerminalLauncher