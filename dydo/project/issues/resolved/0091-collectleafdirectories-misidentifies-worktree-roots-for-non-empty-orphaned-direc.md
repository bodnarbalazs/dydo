---
id: 91
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-10
resolved-date: 2026-04-26
---

# CollectLeafDirectories misidentifies worktree roots for non-empty orphaned directories

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

CollectLeafDirectories function removed entirely. ExecutePrune (Commands/WorktreeCommand.cs:934-966) now enumerates top-level dirs only via Directory.GetDirectories(worktreesDir). Fix commit def1fa4; also referenced in resolved issue #0105. Verified by Dexter.