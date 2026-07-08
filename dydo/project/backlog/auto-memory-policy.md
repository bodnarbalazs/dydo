---
area: project
type: context
name: auto-memory-policy
status: open
---

# Auto-Memory Policy — Implementation Backlog

Follow-ups from the 2026-07-08 co-thinking round (balazs + Leo); design settled in
[Decision 038](../decisions/038-auto-memory-policy.md).

## Code

- **TemplateGenerator: routing line in generated CLAUDE.md** — add the DR 038 §1 paragraph to
  the CLAUDE.md that `dydo init` scaffolds (`Services/TemplateGenerator.cs`, inline content).
  Generic wording — applies to every dydo project. One short paragraph, no list.
- **This repo's CLAUDE.md** — add the same line (trivial edit; can land ahead of the template
  change).

## Methodology / sync surfaces

- **Chief-of-staff sweep item** — add the memory-sweep housekeeping duty (route / retire / keep
  dispositions per DR 038 §3; first sweep human-gated, later sweeps report dispositions in the
  status summary) to the chief-of-staff methodology so `dydo sync` compiles it into every
  project's CoS skill.

## One-time

- **Initial sweep of this machine's store** — execute the routing table at
  `dydo/agents/Leo/notes-memory-sweep-routing.md` once balazs signs it off: create the routed
  issues/guide notes, then delete routed + retired memories.

## Escalation (only if the line proves insufficient)

- **Guard nudge on memory-directory writes** — a custom nudge matching the harness memory path
  that injects the routing reminder at write time (warn, not block). Hold unless post-sweep
  inventories keep growing with project facts (DR 038 Revisit-When).
