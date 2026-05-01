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

Resolved low-severity correctness bug: the panel-limit constraint counted the requesting agent against itself when iterating active assignments, so an idempotent re-set of the same role+task was rejected as over the limit. Fixed by skipping the requesting agent in the iteration; cherry-picked from Emma's recovery branch as `8f38040`.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Panel-limit constraint now skips the requesting agent when iterating active assignments. Idempotent re-set of same role+task no longer counts itself. Cherry-picked from Emma's recovery branch onto master as 8f38040 (originally 551bc5d). Verified by CI run 24998191977.