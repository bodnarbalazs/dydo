---
id: 54
area: general
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-10
---

# InitCommand outputs unquoted DYDO_HUMAN in export example

Resolved low-severity correctness bug: `InitCommand`'s printed `export` example left the `DYDO_HUMAN` value unquoted, which broke when the value contained whitespace or special characters. Fixed by quoting the variable in the printed example.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed: InitCommand export example now properly quotes DYDO_HUMAN variable