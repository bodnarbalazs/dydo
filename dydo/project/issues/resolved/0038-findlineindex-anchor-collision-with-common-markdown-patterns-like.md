---
title: FindLineIndex anchor collision with common Markdown patterns like ---
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 38
type: issue
found-by: inquisition
date: 2026-04-08
resolved-date: 2026-04-10
---

# FindLineIndex anchor collision with common Markdown patterns like ---
Resolved medium-severity correctness bug: `FindLineIndexBefore` resolved ambiguous anchors (common Markdown patterns like `---`) to the first occurrence in the file, mis-positioning template re-anchoring operations. Fixed in commit `00b0c99` by resolving to the closest occurrence instead.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed in commit 00b0c99: FindLineIndexBefore resolves ambiguous anchors to closest occurrence instead of first