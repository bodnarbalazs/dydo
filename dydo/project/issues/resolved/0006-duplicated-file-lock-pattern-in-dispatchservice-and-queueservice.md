---
id: 6
area: backend
type: issue
severity: medium
status: resolved
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