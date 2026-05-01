---
id: 24
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-10
---

# Test-only pass-through methods on TerminalLauncher have no production callers

Resolved low-severity dead-code finding: `LaunchWindows`, `LaunchMac`, and `TryLaunchTerminals` on `TerminalLauncher` were test-only pass-throughs with no production callers. Fixed in commit `3b554de` by removing them and reorienting tests onto the production paths.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in commit 3b554de: Test-only pass-through methods (LaunchWindows, LaunchMac, TryLaunchTerminals) removed