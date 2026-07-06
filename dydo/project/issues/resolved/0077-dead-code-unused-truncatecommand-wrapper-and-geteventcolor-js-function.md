---
title: Dead code: unused TruncateCommand wrapper and getEventColor JS function
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 77
type: issue
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-27
---

# Dead code: unused TruncateCommand wrapper and getEventColor JS function
Resolved low-severity dead-code finding: `AuditCommand.cs` carried an unused `TruncateCommand` wrapper (unrelated to live methods of the same name elsewhere) and an unused `getEventColor` JS function in the embedded script. Fixed by deleting both; cherry-picked as `1b833a4`.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Dropped dead TruncateCommand wrapper at AuditCommand.cs (zero callers in that file; unrelated to live methods of same name in AuditVisualizationService/GuardCommand) and dead getEventColor JS function (zero call sites in embedded script). Cherry-picked as 1b833a4 (originally 09759cd). Verified by CI run 24998191977.