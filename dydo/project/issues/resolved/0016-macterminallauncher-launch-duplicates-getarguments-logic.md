---
id: 16
area: backend
type: issue
severity: high
status: resolved
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-07
---

# MacTerminalLauncher.Launch duplicates GetArguments logic

Resolved high-severity duplication finding: `MacTerminalLauncher.Launch` reimplemented argument-construction logic from `GetArguments`. Fixed by extracting the shared logic to `BuildShellComponents`.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed: common logic extracted to BuildShellComponents method in MacTerminalLauncher