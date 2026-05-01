---
id: 13
area: backend
type: issue
severity: high
status: resolved
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-07
---

# Unvalidated worktreeId in cleanup CLI enables arbitrary directory deletion

Resolved high-severity security finding: the `worktreeId` argument to the cleanup CLI was passed to filesystem deletion without validation, so a crafted id could direct the deletion at arbitrary directories. Closed under the recent code-quality cleanup that introduced strict id validation before any cleanup-side filesystem call.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in recent code quality work