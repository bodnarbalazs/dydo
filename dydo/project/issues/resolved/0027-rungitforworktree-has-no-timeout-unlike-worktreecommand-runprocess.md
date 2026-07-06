---
title: RunGitForWorktree has no timeout unlike WorktreeCommand.RunProcess
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 27
type: issue
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-10
---

# RunGitForWorktree has no timeout unlike WorktreeCommand.RunProcess
Resolved low-severity correctness finding: `RunGitForWorktree` had no process timeout, while the parallel `WorktreeCommand.RunProcess` capped at 30s. A hung git invocation could block worktree operations indefinitely. Fixed by applying the same `ProcessTimeoutMs` (30s) cap.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed: RunGitForWorktree now uses ProcessTimeoutMs (30s) timeout matching WorktreeCommand.RunProcess