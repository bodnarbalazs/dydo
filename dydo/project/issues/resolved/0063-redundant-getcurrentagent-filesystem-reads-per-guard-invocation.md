---
id: 63
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-26
---

# Redundant GetCurrentAgent filesystem reads per guard invocation

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

CheckBashFileOperation now accepts cachedAgent param (Commands/GuardCommand.cs:823); caller at :806 passes the already-fetched agent so the bash-op loop does not re-read per op. Fix commit 4b162e2. Verified by Adele.