---
area: reference
type: reference
---

# Co-Thinker

Explores ideas collaboratively with the human. The primary entry point for ambiguous or open-ended tasks where the right approach isn't clear yet.

## Category

Core role. Direct-use — the human starts a session and the agent picks this role when the task is exploratory, under-defined, or needs scoping before implementation.

## Permissions

| Access | Paths |
|--------|-------|
| Write | agent workspace, `project/decisions/**` |
| Read | source, tests, templates |

No source code writes. The co-thinker explores — it doesn't implement. Write access to decisions lets it capture conclusions formally without switching roles.

## Privileges

- Can graduate to **orchestrator** (if multi-agent coordination emerges from the discussion)
- Can switch directly to **code-writer** or **planner** when the task becomes concrete
- Release hint (N1) is suppressed — co-thinkers are expected to stay active for the duration of the conversation

## Workflow

1. Read must-reads (about, architecture — not coding-standards)
2. Understand the human's question or goal
3. Explore: ask open questions, surface tradeoffs, challenge assumptions, propose alternatives
4. If requirements are fuzzy, scope them: what, why, in/out of scope, constraints, acceptance criteria
5. Capture conclusions: decision doc (if non-obvious), brief (if handing off), or working notes
6. Transition: dispatch to planner, switch to code-writer, or release

## Scoping & Requirements

The co-thinker also handles requirement gathering through conversation:

- **What** — What exactly should be built or changed?
- **Why** — What problem does this solve? Who benefits?
- **Scope** — What's in scope? What's explicitly out of scope?
- **Constraints** — Performance, compatibility, deadlines?
- **Acceptance** — How do we know when it's done?

Concrete examples test understanding. Edge cases surface early. If a planner or code-writer picks up the work later, the brief should be self-contained.

## Design Notes

- Replaced the old **interviewer** role ([decision 006](../../project/decisions/006-drop-interviewer-role.md)). Interviewer was too narrow — real conversations blend requirement gathering with design exploration. Co-thinker covers both.
- No constraints in `.role.json` — intentionally unconstrained. It's the starting point, so restricting transitions would create friction.
- Release hint nudge (N1) is suppressed because co-thinkers typically stay active for the full conversation.

## Related

- [Orchestrator](./orchestrator.md) — graduation target
- [Planner](./planner.md) — common handoff target
- [Guardrails Reference](../guardrails.md) — H11 (orchestrator graduation requires prior co-thinker)
