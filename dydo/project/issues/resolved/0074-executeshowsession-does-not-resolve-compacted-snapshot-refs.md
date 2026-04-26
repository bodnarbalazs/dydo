---
id: 74
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-26
---

# ExecuteShowSession does not resolve compacted snapshot refs

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Commands/AuditCommand.cs:125 ExecuteShowSession now calls ResolveSessionSnapshot (:151-182) which delegates to SnapshotCompactionService.ResolveSnapshot, handling inline / baseline / session-delta chains. Fix commit 99a9a33. Verified by Charlie.