---
title: Dead code in OffLimitsService: CheckCommand and related methods (~70 lines)
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 61
type: issue
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-26
---

# Dead code in OffLimitsService: CheckCommand and related methods (~70 lines)
Resolved medium-severity dead-code finding: `OffLimitsService` carried `CheckCommand`, `ExtractPathsFromCommand`, `CommandPathPatterns`, and `LooksLikePath` with no remaining callers (~70 lines). Fixed in commit `4b162e2` by deleting all four; verified by Adele.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Services/OffLimitsService.cs no longer contains CheckCommand, ExtractPathsFromCommand, CommandPathPatterns, or LooksLikePath. Commit 4b162e2 removed -89 lines from this file. Verified by Adele.