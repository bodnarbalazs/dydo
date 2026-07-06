---
title: SnapshotRef.Depth is write-only in production code
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 82
type: issue
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-07-04
---

# SnapshotRef.Depth is write-only in production code
Open low-severity inquisition finding: production code writes the `Depth` field on `SnapshotRef` but never reads it, so the field carries no operational meaning. Awaiting decision on whether to wire it into the consumer that motivated it or remove it from the model.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Outdated by the 2.0 pivot: SnapshotRef was deleted with the audit subsystem (dcf42c7); the write-only Depth field no longer exists in source. Triage sweep 2026-07-04 (Brian, CoS).