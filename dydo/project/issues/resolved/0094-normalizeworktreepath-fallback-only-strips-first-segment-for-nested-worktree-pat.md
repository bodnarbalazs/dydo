---
id: 94
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-10
resolved-date: 2026-04-10
---

# NormalizeWorktreePath fallback only strips first segment for nested worktree paths

Resolved low-severity correctness bug: the `NormalizeWorktreePath` fallback only stripped the first path segment, so nested worktree paths normalized incorrectly. Fixed in commit `b799726` by using the first segment after the worktree marker as the normalized base.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in commit b799726: NormalizeWorktreePath fallback uses first segment after marker for nested worktree paths