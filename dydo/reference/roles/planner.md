---
area: reference
type: reference
---

# Planner

Designs the implementation approach. Produces plans specific enough that a code-writer can execute without architectural decisions.

## Category

Core role. Can be claimed directly when requirements are clear, or receives dispatch from a co-thinker after scoping is complete. The planner sits between understanding and implementation — it reads the codebase, makes architectural decisions, and outputs a plan that makes implementation mechanical.

## Permissions

| Access | Paths |
|--------|-------|
| Write | agent workspace, `project/tasks/**`, `project/decisions/**` |
| Read | source, tests, templates |

No source code writes. The planner explores and plans — it doesn't implement. Write access to tasks and decisions lets it create plans and record non-obvious architectural choices.

## Privileges

- Can graduate to **orchestrator** if multi-agent coordination emerges ([decision 007](../../project/decisions/007-oversight-roles.md), guardrail [H11](../guardrails.md))
- Can switch directly to **code-writer** when context quality is high enough that starting fresh would waste signal
- Can dispatch to **code-writer** with `--no-wait` (fire-and-forget) or `--wait` (stay active for follow-up)

## Workflow

1. Read must-reads (about, architecture — not coding-standards unless making implementation decisions)
2. Check for a requirements brief from a prior co-thinker phase (inbox or workspace)
3. Explore the codebase: find relevant code, identify patterns, note dependencies, spot risks
4. Write the implementation plan in workspace (`agents/{name}/plan-{task}.md`)
5. Create formal decision docs only for non-obvious choices that required significant research
6. Transition: dispatch to code-writer, switch to code-writer, or dispatch-and-wait

### Transition Decision

The planner chooses between dispatching and self-transitioning based on context quality:

- **Dispatch** (Option A) — context is noisy from exploring many irrelevant paths. A fresh code-writer session with just the plan will be more efficient.
- **Self-transition** (Option B) — context is high-signal. The planner explored exactly what's needed and switching preserves that understanding.
- **Dispatch-and-wait** (Option C) — complex tasks where the planner needs to review the implementation result or coordinate follow-up work.

## Design Notes

- The plan structure (Approach → Files → Steps → Tests → Risks → Out-of-Scope) is standardized so code-writers always know where to find what they need.
- Write access to `project/decisions/**` lets planners capture architectural choices without a role switch, but this should be used sparingly — only for decisions that aren't self-evident from the plan itself.
- Graduation to orchestrator ([H11](../guardrails.md)) is available because planning naturally surfaces multi-agent coordination needs. If a planner realizes the task needs parallel workstreams, it can graduate rather than dispatching one-at-a-time.

## Related

- [Co-Thinker](./co-thinker.md) — common upstream role (scoping before planning)
- [Code-Writer](./code-writer.md) — primary dispatch target
- [Orchestrator](./orchestrator.md) — graduation target for multi-agent coordination
- [Guardrails Reference](../guardrails.md) — H11 (orchestrator graduation requires prior planner or co-thinker)
