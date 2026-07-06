---
title: Unescaped agentName in PowerShell env var assignment (inconsistent escaping)
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 26
type: issue
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-10
---

# Unescaped agentName in PowerShell env var assignment (inconsistent escaping)
Resolved low-severity injection finding: `agentName` and `windowName` were emitted into PowerShell `env:` assignments without escaping, while sibling assignments did escape. Fixed in commit `7756e7e` by applying the same `.Replace`-based escaping to both fields.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed in commit 7756e7e: agentName and windowName escaped with .Replace for PowerShell env var assignments