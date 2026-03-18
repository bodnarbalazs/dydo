---
type: decision
status: accepted
date: 2026-03-18
area: project
---

# 012 — Release Constraints and Role Capabilities

Extend the data-driven role system (decision 008) to cover release-time constraints and role capabilities, replacing hardcoded role-specific checks.

## Problem

Decision 008 moved role-assignment constraints to JSON. But two behavioral rules remain hardcoded in C#:

1. **H25**: Dispatched code-writers must dispatch a reviewer before releasing — an if-statement in `AgentRegistry.ValidateReleasePreconditions()`.
2. **Dispatch-wait privilege**: Only orchestrator/inquisitor/judge can use `--wait` — a hardcoded string array in `DispatchService`.

The inquisitor role now needs the same pattern as H25 (must dispatch a judge before releasing). Adding another hardcoded check would repeat the mistake. Both rules are role capabilities that custom roles should be able to express.

## Decision

### New constraint type: `requires-dispatch`

A new building block for `RoleConstraint` — evaluated at release time, not role-assignment time. Fields:

- `requiredRoles`: target role(s) that must have been dispatched on the same task
- `onlyWhenDispatched`: when `true`, constraint only applies to agents dispatched by other agents (not human-initiated). Default `false`.
- `message`: authored error text with `{task}` substitution

`RoleConstraintEvaluator` gains a `CanRelease()` method. `ValidateReleasePreconditions()` delegates to it. The hardcoded H25 logic is removed.

### Generalized dispatch markers

The `.review-dispatched` marker system is renamed to a generic dispatch marker (`.dispatch-markers/{task}-{targetRole}.json`). `DispatchService` reads the sender's role constraints: if any `requires-dispatch` constraint lists the target role, it creates a marker. The evaluator checks for markers when evaluating release constraints.

### Role capability: `canOrchestrate`

A boolean on `RoleDefinition`. When `true`, the role can use `--wait` dispatch (blocking the target agent from releasing until they respond). Replaces the hardcoded role array. Default `false`. Set on orchestrator, inquisitor, judge.

### Where we draw the line

Not everything about default roles should be soft-coded. Roles like inquisitor and reviewer are tightly coupled to specific commands and workflows (inquisition commands, task state transitions). Making those data-driven would hit diminishing returns — the complexity of a generic mechanism would exceed the complexity of the hardcoded check. The principle: if a custom role would genuinely need to express the capability, it belongs in role JSON. If it's inherent to a specific role's purpose, it can stay in code.

## Implications

- `RoleConstraint` model gains `OnlyWhenDispatched` (bool).
- `RoleDefinition` model gains `CanOrchestrate` (bool).
- `RoleConstraintEvaluator` gains `CanRelease()` alongside existing `CanTakeRole()`.
- `DispatchService` marker creation becomes data-driven.
- `DispatchService` wait-privilege check reads `CanOrchestrate` from role definition.
- `code-writer.role.json` and `inquisitor.role.json` gain `requires-dispatch` constraints.
- `orchestrator.role.json`, `inquisitor.role.json`, `judge.role.json` gain `"canOrchestrate": true`.
- Existing tests for H25 and dispatch-wait must be updated to verify data-driven behavior.
