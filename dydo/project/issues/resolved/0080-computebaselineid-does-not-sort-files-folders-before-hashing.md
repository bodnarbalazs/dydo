---
id: 80
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-27
---

# ComputeBaselineId does not sort Files/Folders before hashing

Resolved low-severity correctness bug: `ComputeBaselineId` hashed `Files` and `Folders` in iteration order, so two snapshots with identical content but different list ordering hashed differently. Fixed by sorting both lists before hashing (matching the existing `DocLinks` pattern); cherry-picked as `17ff84c` with test alignment in `fc548ce`.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

ComputeBaselineId now sorts Files and Folders before hashing (the pattern already used for DocLinks). Two snapshots with identical content but reordered file lists now hash equal. Cherry-picked as 17ff84c (originally dbc3e8b). Test alignment shipped in fc548ce (Henry). Verified by CI run 24998191977.