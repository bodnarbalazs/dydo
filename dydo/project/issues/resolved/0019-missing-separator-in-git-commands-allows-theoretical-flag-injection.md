---
id: 19
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-10
---

# Missing '--' separator in git commands allows theoretical flag injection

Resolved medium-severity security finding: several git invocations omitted the `--` separator before branch/path arguments, leaving a theoretical path for flag-injection attacks if those arguments could be controlled. Fixed by adding `--` separators to all git commands that pass branch or path arguments.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed: All git commands with branch/path arguments include -- separator to prevent flag injection