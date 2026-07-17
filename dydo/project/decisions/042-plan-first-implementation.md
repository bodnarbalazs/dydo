---
area: project
type: decision
status: accepted
date: 2026-07-17
participants: [balazs, Adele (Fable)]
---

# 042 — Plan-First Implementation: the Spec + Plan Record, and the No-Code-Without-Plan Rule

[DR-039](./039-planner-role-review-target-subskills-and-the-plan-gate.md) under
[DR-041](./041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md), plus two
additions. DR-039's doctrine survives the simplification campaign whole; its enforcement rails
(role constraints, dispatch validation) died with the runtime. This record restates what carries
it now — prompts and records — and adds the specification and the prohibition.

## The format — root + slices, spec + plan

The plan is Sprint + SprintTask records (DR-039 §3, "the plan is the rows"). No new folders.

**Root** — `dydo/project/sprints/<name>.md`, statuses `planning → plan-review → active → audit → done`:

1. **Specification** — intent; in/out of scope (binding); acceptance criteria (they become the
   audit checklist); **questions & answers — a plan enters plan-review with zero open
   questions**. An unanswerable question is a spec gap: back to co-thinking, not into code.
2. **Prior art** — search performed, evidence recorded even when rejected (DR-039 §4).
3. **Design** — root-level how: touchpoints, named patterns with paths, data/migration hazards,
   rollback.
4. **Slice map** — table: slice → SprintTask file, disjoint files touched, dependencies, gate.
5. **Ordering & isolation** — lanes, hot files, why slices can't collide.
6. **Watch-outs.**

**Slices** — one `dydo/project/sprint-tasks/<sprint>-<n>-<slug>.md` per row, `ready →
in-progress → done`: spec fragment (this slice's deliverable + acceptance), implementation
detail to the DR-039 bar ("detailed enough that implementation becomes mechanical" — files,
steps, concrete examples, the pattern to copy and where it lives), out-of-scope, exact gate
commands. **Self-contained**: a fresh implementer with only this file and the standards can
execute it. No model names in plan text.

## The gate, post-DR-041

Unchanged from DR-039 §2/§3, now prompting-and-records only: a **separate** reviewer subagent
(fresh eyes — never the context that produced the plan, receives only the artifacts) reviews
against the plan rubric and writes a **verdict block into the sprint root**. Pass ⇒ status
`active`. Fail ⇒ findings back to the planner. The rubric lives as a reviewer skill reference
file (`references/plan.md`), compiled by `dydo sync` — DR-039's subskills, landed as skill-folder
reference files.

## The prohibition (coding-standards level)

**No implementation without a plan.** Code changes require an `active` sprint and a covering
slice file. Sole exception: the trivial-edit rule (Managers Doctrine) — if it needs a reviewer,
it needs a plan. An agent asked to implement without a covering slice stops and routes to
planning. Enforced by prompt (coding standards + worker skills), per DR-036's precedent: if
discipline erodes, escalation is a CLI check, not more prose.

## Related

- [DR-039](./039-planner-role-review-target-subskills-and-the-plan-gate.md) — the foundation:
  gate, rubric, plan-as-rows, review targets. Its §4 rubric is incorporated unchanged.
- [DR-034](./034-pm-record-taxonomy.md) — Sprint/SprintTask records and status vocabulary.
- [DR-041](./041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md) — why prompts
  and records are the only rails.
- Exemplar: `dydo/project/sprints/c1-codex-adoption.md` — a sprint that lived the full gate.
