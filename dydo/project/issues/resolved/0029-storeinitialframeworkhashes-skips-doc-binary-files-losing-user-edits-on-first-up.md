---
title: StoreInitialFrameworkHashes skips doc/binary files, losing user edits on first update
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 29
type: issue
found-by: inquisition
date: 2026-04-08
resolved-date: 2026-04-10
---

# StoreInitialFrameworkHashes skips doc/binary files, losing user edits on first update
Resolved medium-severity correctness bug: `StoreInitialFrameworkHashes` only fingerprinted code files and skipped doc/binary files, so the first template update silently lost user edits to those file categories. Fixed by extending the routine to process all three categories.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed: StoreInitialFrameworkHashes now processes all three file categories including doc and binary files