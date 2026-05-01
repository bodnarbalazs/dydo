---
id: 44
area: understand
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-08
resolved-date: 2026-04-26
---

# roles-and-permissions.md role schema sample missing CanOrchestrate and ConditionalMustReads

Resolved low-severity docs finding: the role schema sample in `roles-and-permissions.md` omitted the `canOrchestrate` and `conditionalMustReads` fields. Fixed in commit `5ffcb54` by adding both fields to the sample with pointers to `orchestrator.role.json` and decision 013; verified by Charlie.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Role schema sample updated in commit 5ffcb54. canOrchestrate and conditionalMustReads added to dydo/understand/roles-and-permissions.md schema sample with pointer to orchestrator.role.json:15,31 and decision 013. Verified by Charlie.