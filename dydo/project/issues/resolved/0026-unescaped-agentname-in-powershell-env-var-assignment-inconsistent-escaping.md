---
id: 26
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-10
---

# Unescaped agentName in PowerShell env var assignment (inconsistent escaping)

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in commit 7756e7e: agentName and windowName escaped with .Replace for PowerShell env var assignments