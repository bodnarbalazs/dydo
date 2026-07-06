---
title: Duplicated BashPostClaudeCheck in LinuxTerminalLauncher and MacTerminalLauncher
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 25
type: issue
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-07
---

# Duplicated BashPostClaudeCheck in LinuxTerminalLauncher and MacTerminalLauncher
Resolved low-severity duplication finding: `BashPostClaudeCheck` was duplicated between `LinuxTerminalLauncher` and `MacTerminalLauncher`. Fixed by consolidating it as a single method on `TerminalLauncher` referenced from both subclasses.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed: BashPostClaudeCheck consolidated as single method in TerminalLauncher, both Linux and Mac launchers reference it