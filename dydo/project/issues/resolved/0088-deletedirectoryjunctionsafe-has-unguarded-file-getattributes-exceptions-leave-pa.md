---
id: 88
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-10
resolved-date: 2026-04-10
---

# DeleteDirectoryJunctionSafe has unguarded File.GetAttributes — exceptions leave partial state

Resolved low-severity correctness finding: an early version of `DeleteDirectoryJunctionSafe` called `File.GetAttributes` without exception handling, so a thrown attribute lookup left the cleanup in partial state. Fixed in commit `b799726` along with the rest of the junction-safe redesign — proper reparse-point detection and exception handling at every step.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in commit b799726: DeleteDirectoryJunctionSafe added with proper reparse point detection and exception handling