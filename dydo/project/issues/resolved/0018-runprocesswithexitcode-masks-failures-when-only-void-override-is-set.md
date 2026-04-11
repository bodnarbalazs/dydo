---
id: 18
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-10
---

# RunProcessWithExitCode masks failures when only void override is set

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed: RunProcessWithExitCode properly checks its own override first, no longer falls through to void override