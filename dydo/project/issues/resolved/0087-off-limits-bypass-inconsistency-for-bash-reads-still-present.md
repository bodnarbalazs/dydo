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

Resolved low-severity finding: a follow-up sighting of the same off-limits-bypass inconsistency between direct and bash reads addressed under #0060. Resolved by the same shared `ShouldBypassOffLimits` helper introduced in commit `4b162e2`.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Same fix as #0060 — shared ShouldBypassOffLimits helper at Commands/GuardCommand.cs:351 unifies bypass handling across direct and bash reads. Fix commit 4b162e2. Verified by Adele.