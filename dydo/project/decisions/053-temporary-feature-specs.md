---
area: project
type: decision
status: accepted
date: 2026-10-13
---

# 053 - Temporary Feature Specs

## Context

Linear keeps the work graph. Features spanning several Issues also need one current anchor for
shared behavior, without keeping obsolete specifications beside the delivered system.
[DYD-330](https://linear.app/bodnar-balazs/issue/DYD-330) adopts the human's agreed hierarchy and lifecycle.

## Decision

Optional specs live at `dydo/reference/working-specs/<stable-project-slug>/<feature-slug>.md`. The Project's
map holder maintains current intent; Linear retains status, assignment, blockers and history.
The [Feature Spec Convention](../../reference/feature-spec-convention.md) owns content, links, pins, reconciliation
and reviewed retirement of the Project subtree after completion or cancellation.

Authority is user instruction, Decision Record, reviewed architecture plan, feature spec, Issue
contract. A conflict holds affected work until the sources are reconciled. Existing review and
human approval gates remain.

## Consequences

This amends [044](./044-linear-canonical-pm-and-dydo-knowledge-boundary.md)'s durable-only Git boundary
and [051](./051-captains-by-default-admiral-on-invocation.md) decisions 1, 6 and 7: Linear remains truth
for coordination, while shared feature intent may live in a temporary spec. Architecture plans keep
their purpose and review; feature-local choices update the spec, and lasting decisions become DRs.
Temporary specs sit under reference, distinct from durable `dydo/project/` evidence. Acceptance
updates existing verification maps and executable scenarios where applicable before retirement. Project closure
preserves lasting knowledge, removes active references and deletes its temporary subtree; canceled
intent is not shipped behavior. Git retains pinned historical evidence.

## Amendment 2026-11-05 - always a Project spec

Decided by the human on 2026-11-05 in the DYD-520 captain thread, recorded on
[DYD-570](https://linear.app/bodnar-balazs/issue/DYD-570). Every other section stands.

- **A scaffolded Project always gets a spec.** A Project the admiral scaffolds gets one working spec
  under `dydo/reference/working-specs/<project-slug>/`, holding the agreed intent: the problem, the
  behavior, the decisions, what is out of scope and the acceptance checks. It follows
  `skills/roles/officers/admiral/resources/scaffold-project.md`.
- **Feature specs stay optional.** Specs for features inside the Project remain optional.
- **Reason.** Balazs: "If the shared understanding from the grilling sessions and brainstorming
  lives there it's much more likely that it's going to be used down the line."

## Related

- [Feature Spec Convention](../../reference/feature-spec-convention.md)
- [Work Model](../../understand/work-model.md)
