---
id: 87
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-10
resolved-date: 2026-04-26
---

# Off-limits bypass inconsistency for bash reads still present

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Same fix as #0060 — shared ShouldBypassOffLimits helper at Commands/GuardCommand.cs:351 unifies bypass handling across direct and bash reads. Fix commit 4b162e2. Verified by Adele.