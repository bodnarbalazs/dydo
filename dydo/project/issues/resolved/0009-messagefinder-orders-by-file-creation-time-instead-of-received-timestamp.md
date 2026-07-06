---
title: MessageFinder orders by file creation time instead of received timestamp
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 9
type: issue
found-by: inquisition
date: 2026-04-03
resolved-date: 2026-04-07
---

# MessageFinder orders by file creation time instead of received timestamp
Resolved low-severity correctness finding: `MessageFinder` ordered messages by filesystem creation time rather than the `received` timestamp embedded in each message, producing wrong order when filesystem timestamps drifted from logical receipt order. Closed under the recent code-quality cleanup.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed in recent code quality work