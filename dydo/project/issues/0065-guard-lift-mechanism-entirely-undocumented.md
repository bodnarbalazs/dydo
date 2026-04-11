---
id: 65
area: project
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-09
---

# Guard lift mechanism entirely undocumented

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Added a "Guard Lift" section to guard-system.md documenting: CLI usage (lift with optional time limit, restore), how the marker file works, RBAC bypass behavior, off-limits protection still applies, audit trail logging with `lifted: true` flag, self-escalation protection, and intended use cases.