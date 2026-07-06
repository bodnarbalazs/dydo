---
title: Interpreter execution bypasses bash file operation analysis
area: backend
fix-release: 
needs-human: false
resolution: 
severity: high
status: resolved
work-type: 
id: 56
type: issue
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-10
---

# Interpreter execution bypasses bash file operation analysis
Resolved high-severity security bug: inline interpreter execution (`python -c`, `node -e`, and similar) ran arbitrary code that the bash file-operation analyzer didn't see, bypassing the guard's path checks. Fixed in commit `e97ebf1` by treating inline interpreter invocations as a dangerous pattern.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed in commit e97ebf1: Inline interpreter execution (python -c, node -e, etc.) added as dangerous pattern