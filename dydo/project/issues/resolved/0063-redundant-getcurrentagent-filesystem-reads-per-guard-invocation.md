---
title: Redundant GetCurrentAgent filesystem reads per guard invocation
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 63
type: issue
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-26
---

# Redundant GetCurrentAgent filesystem reads per guard invocation
Resolved medium-severity perf finding: each iteration of the bash-op loop in the guard re-fetched the current agent from disk, multiplying filesystem reads per guard invocation. Fixed in commit `4b162e2` by adding a `cachedAgent` parameter to `CheckBashFileOperation` so the caller passes the already-fetched agent once.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
CheckBashFileOperation now accepts cachedAgent param (Commands/GuardCommand.cs:823); caller at :806 passes the already-fetched agent so the bash-op loop does not re-read per op. Fix commit 4b162e2. Verified by Adele.