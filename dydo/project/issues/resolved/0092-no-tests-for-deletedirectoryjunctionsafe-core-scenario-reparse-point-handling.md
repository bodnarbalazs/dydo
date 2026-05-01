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

Resolved low-severity test-coverage finding: `DeleteDirectoryJunctionSafe`'s core reparse-point-handling scenarios were untested. Fixed in commit `7976dc5` by adding six direct unit tests (missing-path no-op, empty/files-only/nested directories, junctions at top level and depth 2 each preserving their target) cross-platform via a `CreateJunctionOrSymlink` helper.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Direct unit tests for DeleteDirectoryJunctionSafe added in commit 7976dc5 (Frank). 6 tests covering: missing path no-op, empty/files-only/nested-recursive directories, junction at top-level (preserves target), junction at depth-2 (preserves target). Cross-platform via CreateJunctionOrSymlink helper (mklink /J on Windows, ln -s elsewhere). Reviewed by Charlie.