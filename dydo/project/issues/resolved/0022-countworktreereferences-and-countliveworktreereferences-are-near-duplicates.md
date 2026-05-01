---
id: 22
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-10
---

# CountWorktreeReferences and CountLiveWorktreeReferences are near-duplicates

Resolved medium-severity duplication finding: `CountWorktreeReferences` and `CountLiveWorktreeReferences` differed only in whether they counted hold markers. Fixed in commit `3b554de` by deleting `CountLiveWorktreeReferences` and merging it into `CountWorktreeReferences` with an `includeHolds` parameter.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in commit 3b554de: CountLiveWorktreeReferences removed, merged into CountWorktreeReferences with includeHolds parameter