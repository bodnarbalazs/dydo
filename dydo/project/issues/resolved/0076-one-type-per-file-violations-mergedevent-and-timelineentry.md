---
id: 76
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-27
---

# One-type-per-file violations: MergedEvent and TimelineEntry

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Coupled to #0075. One-type-per-file violations resolved by collapsing MergedEvent and TimelineEntry into a single type in its own file. Same commit c79d107 (originally ea49ead). Verified by CI run 24998191977.