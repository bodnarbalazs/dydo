---
title: c1-5 Dispatch Role Validation + Caller-Role Resolution
blocked-by:
due:
needs-human: false
priority: Medium
sprint: c1-codex-adoption
status: ready
work-type: bug
area: backend
type: context
---

# c1-5 Dispatch Role Validation + Caller-Role Resolution

Issues 0240 + 0237 together (the issues themselves say to). Three defects, one validation path:

1. **0240:** `dydo dispatch --role planner` was silently accepted though no such role is defined
   (`--role` is a bare required string, DispatchCommand.cs:12-16; nothing downstream checks it);
   the target agent landed on a stale/fallback role — dispatcher intent silently rewritten.
2. **0237(1):** the constraint evaluator resolves the CALLER's role as unknown —
   `RoleConstraintEvaluator.cs:134` interpolates `{current_role}` → "unknown role" even when
   `dydo agent status` shows `chief-of-staff` correctly. Whatever state the evaluator reads is
   not what `agent status` reads; find and fix the divergence (plus the "a unknown role" →
   "an unknown role" grammar nit).
3. **0237(2):** the orchestrator `requires-prior` gating (RoleDefinitionService.cs:184-191)
   blocks the DOCUMENTED chief-of-staff routing path (CoS mode file: top-level dispatch of an
   orchestrator). Fix: a chief-of-staff caller satisfies the orchestrator dispatch gate — the
   routing doctrine and the enforcement must agree. Do NOT add new roles to the
   `requires-prior` list (planner joins in P1 per DR 039 — constraint SET changes are out of
   scope; this slice fixes constraint EVALUATION).

## Behavior

- Dispatch with an undefined `--role` fails fast listing the defined roles (base + custom
  `.role.json` under `dydo/_system/roles/` — resolve via `RoleDefinitionService`, don't hardcode
  the seven). Same fail-fast shape as 0239/DR 037 §6.
- Validation lives in `Commands/DispatchCommand.cs` (parse/handler level, before the service
  call) — deliberately NOT in `DispatchService.cs`, which c1-4 owns.
- Caller-role resolution fixed at the source the evaluator reads; covered by a regression test
  reproducing 0237's exact scenario (claimed chief-of-staff dispatching `--role orchestrator`).

## Files

- `Commands/DispatchCommand.cs` — role-existence validation + error listing defined roles.
- `Services/RoleConstraintEvaluator.cs` — caller-role resolution + grammar fix (:134 area).
- `Services/RoleDefinitionService.cs` — requires-prior evaluation honors the chief-of-staff
  routing path (184-191).
- Tests: `DynaDocs.Tests/Commands/` dispatch-validation tests (undefined role rejected + role
  list in message; defined custom role accepted) and role-constraint tests (CoS→orchestrator
  passes; the 0240 planner-string repro now fails fast; non-CoS caller still gated).
- No new command/flag → no 6-surface work; `CommandDocConsistencyTests` unaffected.

## Gates (exact commands)

- `python DynaDocs.Tests/coverage/run_tests.py`
- `DynaDocs.Tests/coverage/gap_check.py --force-run`
- `dydo check`

## Sequencing

Parallel-safe with c1-1/c1-3/c1-4 (file-disjoint).

## Success criteria

0240's repro fails fast with the defined-role list; 0237's repro (chief-of-staff dispatches an
orchestrator) succeeds; caller role renders correctly in constraint messages. Issues 0240 and
0237 resolved. Suite green.
