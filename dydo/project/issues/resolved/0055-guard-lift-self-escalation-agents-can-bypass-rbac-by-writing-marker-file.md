---
id: 55
area: backend
type: issue
severity: critical
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-10
---

# Guard lift self-escalation: agents can bypass RBAC by writing marker file

Resolved critical-severity security bug: agents could escalate themselves out of RBAC by writing the guard-lift marker file directly, bypassing the human-only guard-lift CLI. Fixed in commit `e97ebf1` by adding a hardcoded off-limits pattern that blocks agent writes to `.guard-lift.json` files.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in commit e97ebf1: Hardcoded off-limits pattern blocks agent writes to .guard-lift.json files