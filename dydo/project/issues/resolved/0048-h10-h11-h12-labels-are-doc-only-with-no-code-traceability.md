---
id: 48
area: understand
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-08
resolved-date: 2026-04-26
---

# H10/H11/H12 labels are doc-only with no code traceability

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Reworded H10/H11/H12 in dydo/reference/guardrails.md (commit 5ffcb54) to make explicit that they are doc-shorthand for the constraint-type taxonomy in .role.json files (role-transition, requires-prior, panel-limit) — no code IDs claimed. Verified by Charlie.