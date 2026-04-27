---
id: 92
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-10
resolved-date: 2026-04-27
---

# No tests for DeleteDirectoryJunctionSafe core scenario (reparse point handling)

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Direct unit tests for DeleteDirectoryJunctionSafe added in commit 7976dc5 (Frank). 6 tests covering: missing path no-op, empty/files-only/nested-recursive directories, junction at top-level (preserves target), junction at depth-2 (preserves target). Cross-platform via CreateJunctionOrSymlink helper (mklink /J on Windows, ln -s elsewhere). Reviewed by Charlie.