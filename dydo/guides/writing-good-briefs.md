---
area: guides
type: guide
---

# Writing Good Briefs

The self-containment bar for anything handed to a fresh agent — a slice file's implementation detail, a discovery sub-agent's prompt, a workflow stage's instructions. The plan gate reviews slice files against exactly this bar.

---

## What makes a good brief

- **Self-contained** — The receiving agent can work without asking follow-up questions. It starts fresh: no memory of your conversation, your reasoning, or the files you looked at. Everything it needs must be in the brief.
- **Actionable** — Clear about what needs to be done, not just what happened.
- **Scoped** — One deliverable, not a laundry list. If it needs two reviewers, it's two briefs.

**Never write model choices into a brief.** Which model a worker runs on comes from the dydo config, bound at `dydo sync` time (and rebound by `dydo model cap`/`uncap` during an outage) — it is config's job, not prose. A brief that says "run the reviewer on model X" bypasses the single source of truth and outlives the conditions that motivated it (balazs, 2026-07-08). If the bound model is unavailable, that's a `dydo model cap` decision — escalate, don't route around it in text.

---

## Brief anatomy

Four parts — the same skeleton a slice file's sections carry:

1. **Context — what and why.** One or two sentences of background. Why is this work needed?
2. **Task — what needs doing.** Specific, concrete actions. Not "fix the auth" but "add input validation to the login endpoint in `Services/AuthService.cs`."
3. **File references — where to look.** List the files the agent should read, and the existing pattern to copy with its path. Agents waste time searching when you could just tell them.
4. **Success criteria — how to know it's done.** The exact gate commands that must be green, the behavior that must be observable.

**The test:** could a fresh agent with only this text and the coding standards deliver the work without a single decision or question? Any interpretive latitude is a gap — and in a slice file, a plan-review finding.

## Common mistakes

- **Referencing the conversation** — "as we discussed" means nothing to a fresh context.
- **Describing the problem without the ask** — a bug report is not a brief; say what to do about it.
- **Vague success** — "make it work" forces the agent to guess when to stop.
- **Hidden dependencies** — if the work needs something merged first, say so; the agent can't see your board.

## Related

- [Coding Standards](./coding-standards.md) — the bar the delivered work is held to
- [dydo-glossary.md](../reference/dydo-glossary.md) — slice, lane, gate — the terms briefs are written in
