---
title: Worktree system documentation significantly lags implementation
area: general
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 28
type: issue
found-by: inquisition
date: 2026-04-07
---

# Worktree system documentation significantly lags implementation
The worktree section of `architecture.md` and `dispatch-and-messaging.md` had drifted behind the shipped implementation: the junction list was stale, several workspace markers were undocumented, child-dispatch behavior was incomplete, and the docs still referenced `git worktree prune`. Resolved by bringing both docs up to current behavior, including the merge-related markers.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed in architecture.md and dispatch-and-messaging.md: updated junction list from 1 to 4 directories, added all 7 workspace markers, corrected child dispatch behavior to document all three paths (nested child, inheritance, merge dispatch), replaced `git worktree prune` reference with `dydo worktree prune`, and added merge-related markers.