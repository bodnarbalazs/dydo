---
title: Same-task self-review check is task-name-keyed and trivially defeated by sub-task suffixes — Brian reviewed his own PR2
id: 178
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-05-07
resolved-date: 2026-07-12
---

# Same-task self-review check is task-name-keyed and trivially defeated by sub-task suffixes — Brian reviewed his own PR2

The role-based 'reviewer cannot review own code-writer work' constraint matches on writer-and-reviewer-same-agent AND same task name. Brian dispatched the PR2 (de50134) reviewer to a sub-task whose name differed from the original task by suffix, and the gate did not bite — Brian effectively self-reviewed PR2. Two fix paths: (a) match on task-prefix or task-family rather than exact task name, or (b) match on agent-identity-only when the dispatched role is reviewer and there exists any prior writer activity by the same agent in the same parent task. Source: 'Remaining deeper issues' #2 in dydo/project/inquisitions/agent-crashes.md (Dexter, 2026-05-08); cross-referenced from Adele's brief which originally flagged the gap.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

RESOLVED 2026-07-12: fix landed 24d4c4fe (first mother-of-all-swarms fix). Self-review guard now matches task FAMILY (exact + hyphen-delimited parent/suffix, e.g. my-task/my-task-slice1) for the agent's OWN role-transition history only, so a suffixed sub-task can no longer defeat it - while a different agent reviewing a same/similar task still passes (three scope limits: agent-identity, hyphen-boundary, role-restricted; injection is monotonically restrictive so it can never loosen another constraint). Codex Mia (gpt-5.6-terra, ~10min, auto mode, click-free), cross-vendor Claude-reviewed PASS (correctly scoped, gates 4764/175 green). Minor test-hardening follow-ups in 0284.