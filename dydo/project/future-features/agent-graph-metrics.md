---
area: project
type: concept
status: idea
---

# Agent Graph Metrics

Build a lightweight graph from audit data to measure whether prompt, template, and guardrail changes actually improve orchestration outcomes.

## The Graph

Agents form an implicit graph via dispatch chains and messaging. The audit log, inbox archives, and task files already capture the raw data. A graph built from these sources would have:

- **Session nodes** — one per audit file (agent, role, task, timestamps, event counts)
- **Task nodes** — one per task (status, lifecycle timestamps)
- **Edges** — DISPATCHED (from inbox `from`/`origin` fields), MESSAGED (from `dydo msg` commands), WORKED_ON (from Role events), REVIEWED (inferred from role=reviewer on same task)

## Metrics to Explore

The goal is a small set of numbers that move when you change a prompt, so you can tell if the change helped. Candidates (needs experimentation to find which are actually informative):

- **First-pass approval rate** — % of tasks approved on first review without re-dispatch
- **Rework rate** — % of tasks where a reviewer dispatches another code-writer for fixes
- **Chain completion rate** — % of dispatch chains that finish without human intervention
- **Mean chain depth** — average sessions per task from first dispatch to approval
- **Session duration by role** — median wall-clock time, grouped by role
- **Block rate** — guardrail blocks / total events (high = prompt or onboarding friction)
- **Orphan rate** — sessions without a Release event (agent got stuck)

## Data Gap

Dispatches currently appear as Bash events containing `dydo dispatch ...` strings. A dedicated `Dispatch` audit event (with `from`, `to`, `task`, `role` fields) would make graph construction precise instead of heuristic.

## Future: Stochastic Simulation

Model each task as a parameterized pipeline (code, review, approve/rework) with transition probabilities estimated from the graph. Simulate to predict the impact of changes before deploying them. Needs the graph foundation first.

## Implementation

A `dydo audit metrics` command that builds the graph in-memory, computes metrics, and outputs a summary. Optionally writes JSON to `dydo/_system/audit/reports/metrics.json` for trend tracking.

## Related

- [Audit System](../../reference/audit-system.md) — Audit trail reference
- [Dispatch and Messaging](../../understand/dispatch-and-messaging.md) — How agents communicate
