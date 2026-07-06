---
title: ExecuteShowSession does not resolve compacted snapshot refs
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 74
type: issue
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-26
---

# ExecuteShowSession does not resolve compacted snapshot refs
Resolved medium-severity correctness bug: `ExecuteShowSession` returned the raw snapshot ref without resolving compacted entries, so sessions backed by inline/baseline/session-delta chains showed incomplete data. Fixed in commit `99a9a33` by routing through a new `ResolveSessionSnapshot` that delegates to `SnapshotCompactionService.ResolveSnapshot`.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Commands/AuditCommand.cs:125 ExecuteShowSession now calls ResolveSessionSnapshot (:151-182) which delegates to SnapshotCompactionService.ResolveSnapshot, handling inline / baseline / session-delta chains. Fix commit 99a9a33. Verified by Charlie.