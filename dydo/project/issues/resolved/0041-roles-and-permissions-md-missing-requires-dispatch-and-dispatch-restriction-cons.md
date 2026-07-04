---
id: 41
area: understand
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-08
---

# roles-and-permissions.md missing requires-dispatch and dispatch-restriction constraint types

`roles-and-permissions.md`'s Constraint Types table omitted the `requires-dispatch` and `dispatch-restriction` constraints, leaving readers without the rules that gate release and dispatch. Resolved by adding both constraint types with their evaluation contexts (CanRelease/CanDispatch) and three new subsections under Role Transitions and Restrictions covering Review Enforcement (H25), Inquisitor Escalation, and Reviewer Dispatch Restriction.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Added `requires-dispatch` and `dispatch-restriction` constraint types to the Constraint Types table in roles-and-permissions.md with their evaluation context (CanRelease/CanDispatch). Added three new subsections under Role Transitions and Restrictions documenting Review Enforcement (H25), Inquisitor Escalation, and Reviewer Dispatch Restriction with concrete examples from the role definitions.