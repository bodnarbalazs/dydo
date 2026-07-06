---
title: Thin test coverage for ValidateWorktreeId — missing backslash and path traversal cases
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 93
type: issue
found-by: inquisition
date: 2026-04-10
resolved-date: 2026-04-27
---

# Thin test coverage for ValidateWorktreeId — missing backslash and path traversal cases
Resolved low-severity test-coverage finding: `ValidateWorktreeId`'s tests didn't cover its backslash and path-traversal rejection branches. Fixed in commit `649b95f` by adding two Theory tests under `_RejectsUnsafeCharacters` covering both branches and asserting on discriminating error substrings.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
ValidateWorktreeId backslash and path-traversal coverage added in commit 649b95f (Frank). 2 new Theory tests under _RejectsUnsafeCharacters covering backslash branch (worktree\evil, C:\foo) and path-traversal branch (., .., a/.., a/../b, a/./b), asserting on discriminating error substrings. Reviewed by Charlie.