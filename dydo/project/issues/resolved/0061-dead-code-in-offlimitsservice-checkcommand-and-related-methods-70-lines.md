---
id: 61
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-26
---

# Dead code in OffLimitsService: CheckCommand and related methods (~70 lines)

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Services/OffLimitsService.cs no longer contains CheckCommand, ExtractPathsFromCommand, CommandPathPatterns, or LooksLikePath. Commit 4b162e2 removed -89 lines from this file. Verified by Adele.