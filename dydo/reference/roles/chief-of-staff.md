---
area: reference
type: reference
---

# Chief-of-Staff

The human's right hand — a Tier-1 manager that triages the backlog and idea funnel, routes work to the right domain orchestrator or co-thinker, reports status, and mediates between agents. It is the agent you talk to first when you don't yet know which specialist should own a piece of work.

## Category

Tier-1 manager ([Decision 026](../../project/decisions/026-tier1-managers-doctrine.md)). Like the other managers (co-thinker, orchestrator), it is a **named terminal identity you claim and converse with** — never a spawnable subagent. `dydo sync` compiles it to a skill (methodology), not an agent definition.

## What It Does

- **Triage** — turns the backlog and loose ideas into scoped, routed work.
- **Route** — hands a task to the orchestrator (to run a workflow/sprint), a co-thinker (to design), or a specialist, rather than doing the implementation itself.
- **Report** — keeps the human oriented on status across in-flight work.
- **Mediate** — resolves cross-agent questions and escalations, raising the needs-human flag when a decision is genuinely the human's.

## Manager Doctrine

Tier-1 agents are **managers: the code writes happen in workers, not in the thread you're talking to** ([Decision 026](../../project/decisions/026-tier1-managers-doctrine.md)). The chief-of-staff plans and delegates; workflows and subagents execute.

## Related

- [Orchestrator](./orchestrator.md) — runs the workflows the chief-of-staff routes work into
- [Co-Thinker](./co-thinker.md) — the design-partner manager
- [Planner](./planner.md) — the planning discipline a manager applies before delegating
