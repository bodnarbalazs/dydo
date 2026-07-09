---
title: Dispatch role validation sees dispatcher as unknown role and orchestrator gating blocks chief-of-staff routing
id: 237
area: backend
type: issue
severity: medium
status: resolved
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-08
---

# Dispatch role validation sees dispatcher as unknown role and orchestrator gating blocks chief-of-staff routing

dydo dispatch --role orchestrator failed for a chief-of-staff caller: validation resolved the caller role as unknown and the prior-co-thinker-experience rule makes orchestrator dispatch impossible from the documented CoS routing path.

## Description

## Observed

`dydo dispatch --to Grace --role orchestrator --task adopt-orphaned-codex-slices --brief-file ... --auto-close --tab` failed (exit 2) with:

    You are a unknown role. Orchestrator requires prior co-thinker experience on this task. Ask the user for clarification.

The dispatching agent was Adele with role `chief-of-staff`, correctly set and visible in `dydo agent status` (session 55ced0cd, 2026-07-08).

## Two defects

1. **Dispatcher role resolves to "unknown"**: the validation path does not see the caller's actual role (`chief-of-staff`), even though claim + role were set normally and every other command respected them. Whatever state the dispatch validation reads is not the same state `dydo agent status` reads.

2. **Orchestrator gating blocks the chief-of-staff routing path**: requiring "prior co-thinker experience on this task" before an orchestrator dispatch makes it impossible for a chief-of-staff to route work to a fresh orchestrator session — which is exactly the documented routing model (chief-of-staff mode file: "Routing means messaging or, when a fresh session is warranted, a top-level dispatch of an orchestrator or co-thinker"). The rule as implemented forces the workaround of dispatching a co-thinker and having them self-convert, which is what was done here.

Grammar nit while in there: "a unknown role" → "an unknown role".

## Impact

Chief-of-staff cannot dispatch orchestrators at all; routing doctrine and enforcement disagree. Workaround exists (dispatch co-thinker), severity medium.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Resolved by sprint c1-codex-adoption slice c1-5 (2026-07-09). Both defects fixed at the
constraint-evaluation layer (the constraint SET is unchanged — planner joins requires-prior in P1
per DR 039).

1. **Dispatcher role resolves to "unknown"**: the dispatcher's identity is now threaded from
   `AgentSelector` (resolved from the sender's state) through `IAgentRegistry.CanTakeRole` /
   `AgentRegistry.CanTakeRole` into `RoleConstraintEvaluator`. Constraint messages render the
   real caller role (`{current_role}` = dispatcher's role, falling back to the target's own role
   on the self-conversion path) instead of the target's unset role. The
   `a unknown role` → `an unknown role` grammar nit is fixed by agreeing the indefinite article
   with the resolved role at substitution time.

2. **Orchestrator gating blocks chief-of-staff routing**: `EvaluateRequiresPriorConstraint` now
   treats a chief-of-staff dispatcher as satisfying the requires-prior gate — the documented
   top-level dispatch of a fresh orchestrator. This is enforced at BOTH evaluation sites, not just
   at dispatch:
   - **Dispatch time** (`AgentSelector`): the dispatcher role is threaded into `CanTakeRole`, so a
     chief-of-staff's `dispatch --role orchestrator` clears the gate and reserves/launches the
     target.
   - **Role-set time** (`AgentRegistry.SetRole`): the launched target then claims a fresh session
     and runs `dydo agent role orchestrator --task <task>`. SetRole now resolves the dispatch
     provenance (`from_role`, written by the dispatch into the target's inbox) BEFORE the
     requires-prior gate and passes it as the caller identity, so a CoS-dispatched agent clears the
     gate at role-set exactly as the dispatcher cleared it at dispatch. Without this second site the
     dispatch "succeeded" but the reserved, launched agent wedged at role-set — a fail-downstream
     regression where the dispatcher never saw the error (round-2 review finding). The two sites are
     now consistent, so "chief-of-staff cannot dispatch orchestrators at all" is fully resolved
     end-to-end, not just at the command boundary.

   Non-chief-of-staff callers stay gated exactly as before at both sites (the requires-prior
   constraint set is untouched). Regression tests: `DynaDocs.Tests/Services/RoleConstraintEvaluatorTests.cs`
   (evaluator), `DynaDocs.Tests/Services/RoleBehaviorTests.cs` (SetRole role-set gate, positive +
   negative), and `DynaDocs.Tests/Integration/DispatchCommandTests.cs` (dispatch command + the full
   dispatch→claim→role-set end-to-end path).