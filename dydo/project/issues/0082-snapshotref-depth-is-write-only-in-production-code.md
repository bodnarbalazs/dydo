---
id: 82
area: backend
type: issue
severity: low
status: open
found-by: inquisition
date: 2026-04-09
---

# SnapshotRef.Depth is write-only in production code

Open low-severity inquisition finding: production code writes the `Depth` field on `SnapshotRef` but never reads it, so the field carries no operational meaning. Awaiting decision on whether to wire it into the consumer that motivated it or remove it from the model.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)