---
title: Duplicated file-lock pattern in DispatchService and QueueService
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 6
type: issue
found-by: inquisition
date: 2026-04-03
resolved-date: 2026-04-07
---

# Duplicated file-lock pattern in DispatchService and QueueService
Resolved medium-severity duplication finding: the file-lock pattern was hand-rolled in both `DispatchService` and `QueueService`. Closed under the recent code-quality cleanup that consolidated the lock helpers.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed in recent code quality work