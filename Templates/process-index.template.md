---
area: project
type: hub
---

# Process Workflows

Formalized workflows that agents follow for different types of work. The key value of multi-agent workflows is **fresh context for validation** — when a different agent reviews code, they haven't been "in the weeds" and can spot drift, missing pieces, and over-engineering.

---

## When to Use Which Process

| Task Type | Process | Trigger Keywords |
|-----------|---------|------------------|
| New feature | [feature-implementation](./feature-implementation.md) | "plan", "design", "figure out how to", "non-trivial", "complex", "careful" |
| Bug fix | [bug-fix](./bug-fix.md) | "fix", "bug", "broken", "error" |
| Refactoring | [refactoring](./refactoring.md) | "refactor", "clean up", "restructure", "reorganize" |

---

## Planning Threshold

Use the full `feature-implementation` workflow when **ANY** of these are true:

### Explicit Triggers (User Says)

- "plan", "design", "figure out how to"
- "non-trivial", "complex", "careful"
- "think through", "let's discuss"

### Complexity Indicators

- Task touches **3+ files**
- Introduces **new patterns or abstractions**
- Estimated implementation time **> 30 minutes**
- Requires **architectural decisions**
- Affects **public APIs or interfaces**

### When in Doubt

If you're unsure whether to plan, **plan**. It's cheaper to skip an unnecessary plan than to redo work because you missed something.

---

## Core Rule: No Self-Review

**An agent who was `code-writer` on a task CANNOT become `reviewer` on that same task.**

This is enforced by the system. If you try:

```bash
dydo agent role reviewer --task my-feature
```

And you were previously `code-writer` on `my-feature`, you'll get:

```
ERROR: Agent Adele was code-writer on task 'my-feature' and cannot be reviewer on the same task.
Dispatch to a different agent for review.
```

**Why this matters:** Fresh eyes catch what tired eyes miss. The whole point of multi-agent review is that the reviewer sees the code for the first time.

---

## Sub-Processes

- [code-review](./code-review.md) — Detailed review checklist used by all workflows

---

## Human in the Loop

Humans are the scarce resource. The workflow minimizes human interruptions while ensuring humans have final say:

1. **Agents handle the work** — planning, implementing, reviewing
2. **Changelog marks items "for human review"** — decisions queue up
3. **Human reviews at their convenience** — batch approval, not constant interruption
4. **Human is the final gate** — nothing ships without human sign-off

The goal: prepare decisions so humans can make them quickly and confidently.
