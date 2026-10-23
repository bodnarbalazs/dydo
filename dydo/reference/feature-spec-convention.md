---
area: reference
type: reference
---

# Feature Spec Convention

Use a temporary feature spec when several tickets share behavior or decisions that need one current
anchor. Keep an atomic outcome in its ticket; use an existing reviewed architecture plan when it
already supplies that anchor. The spec governs the change under delivery; the maintained
`verify-<app>` feature map gives the user a route and proof. Executable scenarios own expected values
when available; link them instead of copying their values.

## Location and content

`dydo/reference/working-specs/<stable-project-slug>/<feature-slug>.md` groups specs by their Linear Project.
Keep the Project slug through renames. The singular `dydo/project/` holds durable decisions and evidence.

Start each spec with:

```markdown
---
area: reference
type: reference
linear-project: <Linear Project URL>
---

# <Feature>

## Problem

## Behavior

## Shared decisions

<!-- Link governing decisions and architecture; record feature-local choices here. -->

## Out of scope

## Acceptance checks

## Related

- [<Related document>](<relative path>)
```

The Project map links each spec; affected tickets link the relevant sections and keep their own
bounded outcomes and exact gates. Linear owns status, assignment, blockers and delivery history.

## Changes and evidence

The Project's map holder—the human or an explicitly invoked admiral—maintains current intent in
place. When it shifts, hold affected work until open ticket contracts are reconciled; existing human
approval rules for scope, acceptance and architecture still apply. Git
supplies history; the body states today's intent.

Authority runs from the user's live instruction through Decision Records, reviewed architecture
plans, feature specs, then ticket contracts. Hold conflicting work and escalate through the Captain
to the map holder; reconcile the sources rather than silently overriding one.

Use current-file links for navigation. Before authoring or reviewing, the Captain pins the applicable
spec commit and sections in the brief alongside the ticket contract. Read only those sections and
the governing decisions they link. Changed intent requires a reconciled contract and fresh review
of affected candidates; existing review stages suffice.

## Retirement

Retain all specs until Project acceptance, including specs of delivered pieces. Before completion,
update existing verification maps and executable scenarios where applicable to cover accepted behavior.
On completion or cancellation, the map holder commissions reviewed delivery: preserve lasting knowledge,
remove active references, then delete only `dydo/reference/working-specs/<stable-project-slug>/`.
Canceled intent is not shipped behavior. The permanent `_working-specs.md` stays; historical
evidence retains exact commit permalinks.

## Related

- [Working Specs](./working-specs/_working-specs.md)
- [Linear Workspace Standard](./linear-workspace-standard.md)
- [Writing Documentation](./writing-docs.md)
