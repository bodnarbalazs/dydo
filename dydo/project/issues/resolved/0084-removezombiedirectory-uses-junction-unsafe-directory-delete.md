---
id: 84
area: backend
type: issue
severity: high
status: resolved
found-by: inquisition
date: 2026-04-10
resolved-date: 2026-04-10
---

# RemoveZombieDirectory uses junction-unsafe Directory.Delete

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in commit b799726: RemoveZombieDirectory replaced with DeleteDirectoryJunctionSafe that detects junctions via ReparsePoint