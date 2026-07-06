---
title: Path traversal via '..' in task name allows worktree directory escape
area: backend
fix-release: 
needs-human: false
resolution: 
severity: high
status: resolved
work-type: 
id: 12
type: issue
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-07
---

# Path traversal via '..' in task name allows worktree directory escape
Resolved high-severity security finding: task names containing `..` segments could escape the worktree directory when concatenated into filesystem paths, enabling directory traversal during dispatch and cleanup. Closed under the recent code-quality cleanup that added explicit task-name validation rejecting traversal patterns.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed in recent code quality work