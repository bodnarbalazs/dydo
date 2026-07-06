---
title: Reanchor insertion order reversal with shared upper anchors
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 30
type: issue
found-by: inquisition
date: 2026-04-08
resolved-date: 2026-04-10
---

# Reanchor insertion order reversal with shared upper anchors
Resolved low-severity correctness bug: when two reanchor operations shared an upper anchor, `FindLineIndexBefore` resolved both to the first occurrence, reversing their insertion order. Fixed in commit `00b0c99` by resolving to the closest occurrence so insertion order is preserved.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed in commit 00b0c99: FindLineIndexBefore resolves shared upper anchors to closest occurrence, preserving insertion order