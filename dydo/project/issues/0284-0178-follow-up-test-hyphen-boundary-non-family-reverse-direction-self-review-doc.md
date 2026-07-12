---
title: 0178 follow-up: test hyphen-boundary non-family + reverse-direction self-review; document synthetic-history assumption
id: 284
area: backend
type: issue
severity: low
status: open
found-by: review
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-12
---

# 0178 follow-up: test hyphen-boundary non-family + reverse-direction self-review; document synthetic-history assumption

Non-blocking hardening from the 0178 Claude review (fix landed 24d4c4fe). (1) TEST GAP: no test asserts a non-hyphen prefix is NOT family (history my-task, review my-taskx MUST be allowed) - a future 'simplify AreTaskNamesInSameFamily to raw StartsWith' would stay green while wrongly blocking prefix-sharing tasks. Add it. (2) TEST GAP: the reverse direction (child-history my-task-slice1, parent-request my-task, AgentRegistry.cs:1041) is correct-by-trace but untested; deleting the clause would go undetected. (3) FRAGILITY: AddTaskFamilyRoleHistoryForRoleTransition mutates the AgentState from GetAgentState, safe only because GetAgentState re-parses state.md every call (no cache) - a one-line why-comment should pin that a future memoization would leak synthetic history to disk/status. (4) COSMETIC: block message substitutes the requested task not the actual (RoleConstraintEvaluator.cs:91); double blank line AgentRegistry.cs:1043-1044. Good tiny codex dogfood.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)