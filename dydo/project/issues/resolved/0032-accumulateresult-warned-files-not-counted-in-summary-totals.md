---
title: AccumulateResult warned files not counted in summary totals
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 32
type: issue
found-by: inquisition
date: 2026-04-08
resolved-date: 2026-04-10
---

# AccumulateResult warned files not counted in summary totals
Resolved low-severity reporting bug: `AccumulateResult` summed errors and clean files but not warnings, so the summary line under-counted total files inspected. Fixed by including warned files in the summary totals.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed: AccumulateResult now counts warned files in summary output