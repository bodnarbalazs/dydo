---
id: 178
area: backend
type: issue
severity: medium
status: open
found-by: inquisition
date: 2026-05-07
---

# Same-task self-review check is task-name-keyed and trivially defeated by sub-task suffixes — Brian reviewed his own PR2

The role-based 'reviewer cannot review own code-writer work' constraint matches on writer-and-reviewer-same-agent AND same task name. Brian dispatched the PR2 (de50134) reviewer to a sub-task whose name differed from the original task by suffix, and the gate did not bite — Brian effectively self-reviewed PR2. Two fix paths: (a) match on task-prefix or task-family rather than exact task name, or (b) match on agent-identity-only when the dispatched role is reviewer and there exists any prior writer activity by the same agent in the same parent task. Source: 'Remaining deeper issues' #2 in dydo/project/inquisitions/agent-crashes.md (Dexter, 2026-05-08); cross-referenced from Adele's brief which originally flagged the gap.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)