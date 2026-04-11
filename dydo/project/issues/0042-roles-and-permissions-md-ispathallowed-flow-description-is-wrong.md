---
id: 42
area: understand
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-08
---

# roles-and-permissions.md IsPathAllowed flow description is wrong

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Rewrote the IsPathAllowed flow in roles-and-permissions.md to match the actual 4-step implementation: no-role check, ReadOnlyPaths check (with WritablePaths override), empty-writable check, WritablePaths match. Clarified that off-limits is a separate guard pipeline stage. Also fixed the same inaccurate 3-step description in guard-system.md. Updated glob pattern documentation to include all 4 conversions (`**/`, `**`, `*`, `?`).