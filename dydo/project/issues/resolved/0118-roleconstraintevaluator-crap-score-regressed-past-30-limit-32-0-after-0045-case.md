---
id: 118
area: backend
type: issue
severity: low
status: resolved
found-by: review
date: 2026-04-27
resolved-date: 2026-04-27
---

# RoleConstraintEvaluator CRAP score regressed past 30 limit (32.0) after #0045 case-insensitive unification

## Description

`gap_check` reports a tier-coverage failure on `Services/RoleConstraintEvaluator.cs` after the `#0045` (case-insensitive comparator unification) and `#0047` (panel-limit self-exclusion) commits cherry-picked onto master:

```
RoleConstraintEvaluator.cs — CRAP 32.0 (>30 limit)
  Line coverage:   100%
  Branch coverage: 92.7%
```

The CRAP (Change Risk Anti-Pattern) score is a composite of cyclomatic complexity and coverage: `CC^2 * (1 - cov)^3 + CC`. Even at 100% line coverage, the residual branch-coverage gap (92.7%) combined with elevated cyclomatic complexity in the unified comparator paths pushes the score over the project's 30-CRAP tier limit.

The regression is not a correctness issue — the file's logic is correct and its tests pass. It's a code-shape complaint: after `#0045` and `#0047` added new conditional logic to several `CanTakeRole` branches without refactoring the surrounding control flow, the cyclomatic complexity grew enough that the gap_check tier classifier flips this file out of its prior tier.

Surfaced 2026-04-27 by Henry while running gap_check on the audit-recovery merge.

## Reproduction

1. Check out master at or after commit `8f38040` (the `#0047` panel-limit fix).
2. Run `gap_check.py --force-run` (or whatever the project's coverage-tier check is).
3. Observe one FAIL at `Services/RoleConstraintEvaluator.cs` with CRAP 32.0 vs <=30 limit.
4. The failure does not block CI today (gap_check is informational), but it will if the project enforces tier compliance for releases.

## Likely root cause

The `CanTakeRole` method already had per-constraint branches (role-transition, requires-prior, panel-limit). `#0045` added a uniform `OrdinalIgnoreCase` comparator wrapper at each branch — three sites, each adding a small bit of conditional logic. `#0047` added a `name != agentName` filter inside the panel-limit branch, increasing its decision count.

Each individual change is small and reviewable; the cumulative effect is that the method's cyclomatic complexity crossed a threshold the CRAP formula amplifies sharply.

The remaining branch-coverage gap (92.7% vs 100%) is also relevant — likely a few defensive branches (e.g., `agentName == null` checks, empty-collection short-circuits) that the test suite doesn't currently exercise.

## Suggested fix

Two paths, pickable independently:

1. **Reduce cyclomatic complexity by extracting per-constraint helpers.** Move the role-transition, requires-prior, and panel-limit branches into private helpers (`EvaluateRoleTransitionConstraint`, `EvaluateRequiresPriorConstraint`, `EvaluatePanelLimitConstraint`). `CanTakeRole` becomes a dispatcher that picks the helper based on `constraint.Type`. Each helper has its own bounded complexity, the dispatcher's complexity drops to roughly N branches, and the total tier classification on a per-method basis improves.

2. **Close the branch-coverage gap.** Identify the missing branches via the gap_check output's per-line uncovered regions. Add tests for the unexercised paths. Even modest branch-coverage gains (e.g., 92.7% → 96%) significantly drop the CRAP score because of the `(1 - cov)^3` term.

Doing both is best. Doing (2) alone is the lower-risk option — no production-code refactor, just more tests.

If you decide to ship as-is and adjust the tier rules instead, file a follow-up to either raise the limit specifically for `RoleConstraintEvaluator.cs` (with a comment explaining why) or to lower the project-wide limit and explicitly carve out this file. Don't silently raise the global limit without documentation.

## Impact

- gap_check tier reports red on `Services/RoleConstraintEvaluator.cs`. Doesn't block CI today (gap_check is run separately and informationally), but is one of the tier-failure surfaces that release tooling typically gates on.
- Cumulative drift risk: future changes to `CanTakeRole` will compound the complexity unless someone refactors. Better to land the helper-extraction now while the file is fresh in everyone's mind.
- Symptomatic of a wider question: should the project pin gap_check to a stricter floor and act on regressions immediately, or treat them as advisory?

## Related context

- `Services/RoleConstraintEvaluator.cs` — file in question.
- Cherry-picked commits on master: `2fba407` (`#0045`), `8f38040` (`#0047`).
- `gap_check.py` (or wherever the tier classifier lives) — for the CRAP formula and tier thresholds.
- `DynaDocs.Tests/Services/RoleConstraintEvaluatorTests.cs` — where the missing branch-coverage tests would land.
- Henry's surface in his completion message for `fix-ci-after-audit-recovery` (2026-04-27, archived in `dydo/agents/Brian/archive/inbox/`).

## Resolution

RoleConstraintEvaluator refactored + tests added. Refactor in commit 4fdd383 (Henry) extracted EvaluateRoleTransitionConstraint, EvaluateRequiresPriorConstraint, and EvaluatePanelLimitConstraint as private static helpers; CanTakeRole's switch now dispatches. Tests in commit 40c5582 close the branch-coverage gap (3 new tests covering CanDispatch's null-state, null TargetRole, null RequiredRoles paths). Result: CRAP 32.0 -> 26.0, branch coverage 92.7% -> 100%, gap_check tier T1 passes. 3840/3840 tests. Reviewed by Emma.
