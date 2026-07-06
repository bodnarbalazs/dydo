---
title: DangerousPatterns has gaps for rm -rf ./, PowerShell, and NVMe devices
area: backend
fix-release: 
needs-human: false
resolution: 
severity: high
status: resolved
work-type: 
id: 58
type: issue
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-11
---

# DangerousPatterns has gaps for rm -rf ./, PowerShell, and NVMe devices
Resolved high-severity security bug: `DangerousPatterns` missed `rm -rf ./`, PowerShell deletion variants, and NVMe device paths, leaving destructive operations un-blocked. Fixed by closing the pattern gaps and removing the `MergeSystemNudges` downgrade-bypass that previously let dangerous severities be relaxed.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed by guard security agent - DangerousPatterns gaps addressed with MergeSystemNudges downgrade bypass fix