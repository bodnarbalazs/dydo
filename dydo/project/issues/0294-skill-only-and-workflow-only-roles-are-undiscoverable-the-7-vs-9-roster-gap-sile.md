---
title: Skill-only and workflow-only roles are undiscoverable - the 7-vs-9 roster gap silently manufactures false bug reports
id: 294
area: backend
type: issue
severity: medium
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-13
---

# Skill-only and workflow-only roles are undiscoverable - the 7-vs-9 roster gap silently manufactures false bug reports

planner (skill-only, DR-024) and sprint-auditor (workflow-only, DR-026) are correctly excluded from the claimable roster, but nothing says so - dispatch silently falls back and two independent agents each filed it as a bug.

## Description

## Observed

Two independent, competent agents — an orchestrator in a downstream project, and a code-writer during the 1.x->2.x migration — **each independently filed the same false bug report**: "`dydo roles list` shows 7 roles while `dydo sync` emits 9; planner and sprint-auditor aren't registered; planner-shaped work silently runs under co-thinker."

The behavior they observed is **correct and deliberate**. The reports are false. But the fact that two capable agents both read it as a defect is itself the defect.

## The actual (correct) design

`Services/RoleDefinitionService.cs`:

```csharp
SkillOnlyRoles    = { "planner" }         // Decision 024
WorkflowOnlyRoles = { "sprint-auditor" }  // Decision 026
NonClaimableRoles = SkillOnlyRoles ∪ WorkflowOnlyRoles
```

- **`planner`** compiles into a **skill** but is not a claimable Tier-1 identity: planning is a methodology an orchestrator/co-thinker applies, not a separate dispatchable agent. It appears in `GetBaseRoleDefinitions()` only to drive skill generation and is excluded from the on-disk roster (which feeds the guard's claimable-role set).
- **`sprint-auditor`** compiles into a native agent + skill but is spawned by the `run-sprint` workflow, never dispatched by hand.

So: **9 base roles − 2 non-claimable = the 7 in the roster.** The 7 and the 9 are intentionally different numbers measuring different things (claimable identities vs compiled skills). "Planner-shaped work runs under co-thinker" is the design working — the co-thinker carries the planner skill.

## The real defect: silent, undiscoverable, and it manufactures false bug reports

Nothing anywhere surfaces this:
- `dydo roles list` prints 7 with no indication that skill-only / workflow-only roles exist and were deliberately excluded.
- `dydo sync` emits 9 with no indication that 2 of them are non-claimable.
- Attempting to dispatch `--role planner` does **not** clearly refuse with the reason — it apparently falls through to something else, so the operator's intent is silently not honored.

A silent fallback that leads competent agents to conclude the tool is broken — and to waste time diagnosing and reporting it — is a UX defect regardless of the underlying behavior being right. It also risks someone "fixing" it by making planner claimable, quietly undoing Decision 024.

## Fix direction

1. **Explicit refusal on dispatch.** `dydo dispatch --role planner` (and `--role sprint-auditor`) must refuse with an actionable message naming the reason and the remedy, e.g.:
   > `planner is a skill-only role (Decision 024) and cannot be claimed. Planning is a methodology carried by the orchestrator and co-thinker roles — dispatch one of those; they carry the planner skill.`
   > `sprint-auditor is workflow-only (Decision 026) — it is spawned by the run-sprint workflow, not dispatched directly.`
   Never silently fall back to a different role.
2. **Surface the distinction in `dydo roles list`.** Show the non-claimable roles in a clearly-labelled section (e.g. "Skill-only (not claimable): planner" / "Workflow-only: sprint-auditor") so the 7-vs-9 is self-explaining rather than looking like drift.
3. **Document it** where an onboarding agent will actually hit it: the roles/dispatch reference docs and the agent-workflow template.

## Acceptance

- Dispatching a skill-only or workflow-only role produces a clear, actionable refusal naming the decision and the remedy — never a silent fallback.
- `dydo roles list` makes the claimable-vs-compiled distinction visible, so the 7-vs-9 gap is self-explaining.
- A fresh agent reading the docs/output does not conclude the roster is broken.

## Note

This is a **discoverability/UX fix, NOT a behavior change**. Do not make `planner` claimable — that would silently reverse Decision 024. If direct planner dispatch is ever wanted, it must be a deliberate new decision, not a side effect of this fix.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)