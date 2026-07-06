---
title: One-type-per-file violations: MergedEvent and TimelineEntry
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 76
type: issue
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-27
---

# One-type-per-file violations: MergedEvent and TimelineEntry
Resolved low-severity hygiene finding: `MergedEvent` and `TimelineEntry` violated the project's one-type-per-file convention. Resolved as a coupled fix to #0075 — collapsing them into a single unified type in its own file in commit `c79d107` removed both the duplication and the convention violation.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Coupled to #0075. One-type-per-file violations resolved by collapsing MergedEvent and TimelineEntry into a single type in its own file. Same commit c79d107 (originally ea49ead). Verified by CI run 24998191977.