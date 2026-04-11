---
id: 21
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-10
---

# Near-duplicate process runner methods across WorktreeCommand and DispatchService

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in commit 3b554de: RunProcess now calls RunProcessWithExitCode internally, eliminating duplicate PSI setup