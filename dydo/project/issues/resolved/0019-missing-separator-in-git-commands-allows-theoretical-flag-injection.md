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

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed: All git commands with branch/path arguments include -- separator to prevent flag injection