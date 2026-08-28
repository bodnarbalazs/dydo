---
title: Agent Graph Metrics
area: project
type: concept
status: idea
---

# Agent Graph Metrics

Explore a lightweight graph over supported task and thread execution evidence to measure whether prompt, template, and guardrail changes improve orchestration outcomes. This remains a hypothetical idea, not a current dydo capability.

## Evidence Boundary

The audit logs, inbox archives, repository task files, and dispatch or messaging commands from dydo 1.x are retired machinery, not current inputs. Any implementation must consume current host-native task and thread execution evidence or first define a durable, supported evidence import.

## The Graph

When supported evidence is available, coordination and review relationships could form an implicit graph. A graph built from that evidence could have:

- **Thread nodes** — one per recorded execution thread, with its available timing and role context
- **Task nodes** — one per task represented by the evidence
- **Edges** — handoff, collaboration, work, and review relationships when the supported evidence records them

## Metrics to Explore

The goal is a small set of numbers that move when you change a prompt, so you can tell if the change helped. Candidates (needs experimentation to find which are actually informative):

- **First-pass approval rate** — % of tasks accepted on first review without further work
- **Rework rate** — % of tasks returned from review for further work
- **Chain completion rate** — % of coordination chains that finish without human intervention
- **Mean chain depth** — average recorded execution steps per task from work start to approval
- **Execution duration by role** — median wall-clock time, grouped by role
- **Block rate** — guardrail blocks / total events (high = prompt or onboarding friction)
- **Orphan rate** — execution threads without a recorded terminal outcome

## Data Gap

dydo does not define a current execution-evidence schema for this analysis. The idea needs either a supported host-native source with stable relationship fields or a deliberately designed durable import before graph construction can be precise.

## Future: Stochastic Simulation

Model each task as a parameterized pipeline (work, review, approve/rework) with transition probabilities estimated from the graph. Simulate to predict the impact of changes before deploying them. This depends on the evidence foundation first.

## Implementation

After the evidence contract exists, evaluate a small analysis capability that builds the graph in memory and reports the selected metrics. It must not assume a current dydo command or repository audit output exists.

## Rationale

FutureFeature is a repo-native idea record. It remains unpromoted until a separate human decision creates Linear work.

## Related

- [Work Model](../../understand/work-model.md) — Current boundary between durable knowledge and live work
