---
id: 47
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-08
resolved-date: 2026-04-27
---

# Panel-limit constraint counts requesting agent against itself

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Panel-limit constraint now skips the requesting agent when iterating active assignments. Idempotent re-set of same role+task no longer counts itself. Cherry-picked from Emma's recovery branch onto master as 8f38040 (originally 551bc5d). Verified by CI run 24998191977.