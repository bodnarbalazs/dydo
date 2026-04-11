---
id: 88
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-10
resolved-date: 2026-04-10
---

# DeleteDirectoryJunctionSafe has unguarded File.GetAttributes — exceptions leave partial state

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in commit b799726: DeleteDirectoryJunctionSafe added with proper reparse point detection and exception handling