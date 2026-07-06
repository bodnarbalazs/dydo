---
title: AllModes test theories only cover 6 of 9 modes
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 36
type: issue
found-by: inquisition
date: 2026-04-08
resolved-date: 2026-04-10
---

# AllModes test theories only cover 6 of 9 modes
Resolved low-severity test-coverage finding: the `AllModes` test theories enumerated only 6 of the 9 mode templates, leaving inquisitor, judge, and orchestrator untested. Fixed in commit `7756e7e` by adding the three missing mode templates so all nine are covered.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed in commit 7756e7e: Added inquisitor, judge, orchestrator mode templates to AllModes test theories, covering all 9 modes