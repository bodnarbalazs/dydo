---
title: Terminal launcher tests verify GetArguments but production uses TryLaunch/Launch
area: backend
fix-release: 
needs-human: false
resolution: 
severity: high
status: resolved
work-type: 
id: 17
type: issue
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-07
---

# Terminal launcher tests verify GetArguments but production uses TryLaunch/Launch
Resolved high-severity test-coverage finding: terminal-launcher tests exercised `GetArguments` while production used the divergent `TryLaunch` / `Launch` paths, so tests gave false confidence. Fixed by reorienting tests onto the production paths and consolidating the shared logic so both paths exercise the same core code.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed: tests now exercise production paths via instance methods, and shared logic (ApplyOverrides/BuildShellComponents) means both paths test the same core code