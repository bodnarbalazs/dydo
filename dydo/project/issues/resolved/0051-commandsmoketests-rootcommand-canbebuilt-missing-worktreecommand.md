---
id: 51
area: general
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-10
---

# CommandSmokeTests.RootCommand_CanBeBuilt missing WorktreeCommand

Resolved low-severity test-coverage finding: `CommandSmokeTests.RootCommand_CanBeBuilt` did not include `WorktreeCommand`, so the smoke gate didn't catch wiring regressions for that command. Fixed by adding `WorktreeCommand` to the smoke test.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed: WorktreeCommand now included in CommandSmokeTests at lines 44 and 88