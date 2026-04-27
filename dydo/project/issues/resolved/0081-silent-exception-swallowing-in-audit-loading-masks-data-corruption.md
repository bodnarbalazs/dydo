---
id: 81
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-27
---

# Silent exception swallowing in audit loading masks data corruption

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Four secondary swallowers in audit loading now surface to stderr instead of silently skipping: SnapshotCompactionService.cs:329 (LoadBaselines), :347 (LoadSession), AuditCommand.cs:173 (ResolveSessionSnapshot baseline load), :448 (BuildCombinedSnapshot baseline load). Each empty-catch now writes Console.Error.WriteLine matching AuditService.cs:261-265. Cherry-picked as c381bda (originally 6f802ca). Verified by CI run 24998191977.