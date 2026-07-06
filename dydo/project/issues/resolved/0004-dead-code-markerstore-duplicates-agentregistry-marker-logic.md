---
title: Dead code: MarkerStore duplicates AgentRegistry marker logic
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 4
type: issue
found-by: inquisition
date: 2026-04-03
resolved-date: 2026-04-07
---

# Dead code: MarkerStore duplicates AgentRegistry marker logic
Resolved medium-severity dead-code finding: `MarkerStore` carried marker-management logic that duplicated `AgentRegistry`'s implementation, with no remaining production callers. Closed under the recent code-quality cleanup that consolidated marker handling on `AgentRegistry`.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed in recent code quality work