---
title: Unknown role-constraint type in the on-disk role JSON bricks the running binary (fatal, not forward-compatible) - disables all code-writer dispatch
id: 286
area: backend
type: issue
severity: high
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-12
---

# Unknown role-constraint type in the on-disk role JSON bricks the running binary (fatal, not forward-compatible) - disables all code-writer dispatch

A new constraint type declared in a runtime-read role file makes an older dydo binary throw 'Unknown constraint type' in CanTakeRole, disabling all code-writer dispatch ('No free agents'); unknown types should degrade gracefully, and role-contract changes must deploy binary-first.

## Description

Discovered live during the Wave-1 swarm (2026-07-12). A code-writer (0110) added a NEW constraint type `requires-commit` to the on-disk `dydo/_system/roles/code-writer.role.json`. The role JSON is read at RUNTIME by whatever dydo binary is installed. The installed binary predated the source change that teaches `RoleConstraintEvaluator`/`RoleDefinitionService` about `requires-commit`, so it hit its strict `default` branch and threw `Unknown constraint type: 'requires-commit'` — which made `CanTakeRole` fail for EVERY agent taking the code-writer role. Result: `dydo dispatch --role code-writer` reported "No free agents available for human 'balazs'" and ALL code-writer dispatch was bricked until the on-disk role file was reverted.

## Root cause

`RoleConstraintEvaluator.EvaluateConstraint` and `RoleDefinitionService` validation treat an unrecognized constraint `Type` as FATAL:
- `RoleConstraintEvaluator.EvaluateConstraint` `default:` sets `reason = "Unknown constraint type: '{Type}'."` and returns false → the agent cannot take the role.
- `RoleDefinitionService` validation similarly rejects unknown types.

So the on-disk role contract and the running binary are COUPLED: a role file that declares a constraint type newer than the binary bricks role-taking (and thus dispatch/selection) entirely. There is no forward compatibility and no deploy-ordering guard.

## Why it's dangerous

- The failure is total (all code-writer dispatch) and the error surfaced as a misleading "No free agents available" at the selector, not as "your role file is newer than this binary."
- It bites exactly the intended workflow: land a role-contract change via the swarm, and the still-running orchestrator binary immediately can't dispatch the code-writers needed to finish/land it. Chicken-and-egg.
- Reverting requires `git checkout` or `dydo roles reset`, both of which are off-limits/human-only for a chief-of-staff agent — so an agent cannot self-recover.

## Fix options

1. FORWARD-COMPAT (preferred): unknown constraint types should be NON-FATAL — ignore with a one-line warning (`WARN: role '{role}' declares constraint '{type}' unknown to this binary; ignoring`) rather than failing role-taking. A newer role file then degrades gracefully on an older binary (the new gate simply isn't enforced until the binary catches up) instead of bricking dispatch. Apply symmetrically in `RoleConstraintEvaluator` (take-role) and `RoleDefinitionService` (validation/load).
2. DEPLOY-ORDERING (complementary): document + tooling-enforce "install the binary that supports a new constraint type BEFORE the role file declaring it lands" — e.g. `dydo roles reset`/regeneration is driven by the binary, so regenerating with an OLD binary silently drops new constraints (data loss risk in the other direction). A version stamp on the role schema + a compat check at load would make the mismatch explicit.
3. RECOVERY: a non-human-only escape hatch (or clearer error) so an agent hitting this can recover without `git checkout`/`roles reset`.

## Related
- Surfaced by issue 0110 (first fix to introduce a new constraint type). 0110's own landing MUST sequence binary-install BEFORE re-declaring the constraint in the role file.
- `Services/RoleConstraintEvaluator.cs` (EvaluateConstraint default branch), `Services/RoleDefinitionService.cs` (constraint validation), `Commands/RolesCommand` (reset regeneration).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)