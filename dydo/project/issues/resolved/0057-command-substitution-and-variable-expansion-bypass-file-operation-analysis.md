---
id: 57
area: backend
type: issue
severity: high
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-10
---

# Command substitution and variable expansion bypass file operation analysis

Resolved high-severity security bug: bash command substitution (`$(...)`, backticks) and variable expansion let an attacker hide write paths from the file-operation analyzer. Fixed in commit `e97ebf1` by adding a `HasBypassAttempt` flag that blocks tainted write operations outright.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in commit e97ebf1: HasBypassAttempt flag detects command substitution/variable expansion and blocks tainted write operations