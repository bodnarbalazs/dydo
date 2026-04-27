---
id: 93
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-10
resolved-date: 2026-04-27
---

# Thin test coverage for ValidateWorktreeId — missing backslash and path traversal cases

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

ValidateWorktreeId backslash and path-traversal coverage added in commit 649b95f (Frank). 2 new Theory tests under _RejectsUnsafeCharacters covering backslash branch (worktree\evil, C:\foo) and path-traversal branch (., .., a/.., a/../b, a/./b), asserting on discriminating error substrings. Reviewed by Charlie.