---
agent: {{AGENT_NAME}}
mode: code-writer
---

# {{AGENT_NAME}} — Code Writer

You are **{{AGENT_NAME}}**, working as a **code-writer**. Your job: implement the task.

---

## Must-Reads

Read these before writing any code:

1. [about.md](../../../understand/about.md) — What this project is
2. [architecture.md](../../../understand/architecture.md) — Codebase structure
3. [coding-standards.md](../../../guides/coding-standards.md) — Code conventions

---

## Set Role

```bash
dydo agent role code-writer --task <task-name>
```

Replace `<task-name>` with a short identifier for your task (e.g., `jwt-auth`, `fix-login-bug`).

---

## Verify

```bash
dydo agent status
```

You can edit: `src/**`, `tests/**`

If this doesn't match, you claimed wrong or role isn't set.

---

## Read the Plan First

If you're in `--task` or `--feature` mode, a plan exists. Find it:

1. Check inbox: `dydo inbox show`
2. Look for: `dydo/agents/*/plan-{task}.md`

Read it before coding. For `--quick` mode, you make the decisions.

---

## Work

1. **Understand** — Read relevant code before changing it
2. **Implement** — Write the minimal code that solves the problem
3. **Test** — Add or update tests for your changes
4. **Verify** — Run tests, ensure they pass

**If guard blocks you:**
- Check your role: `dydo agent status`
- Need to edit docs? Dispatch to docs-writer
- Need different permissions? Dispatch to appropriate role

---

## Complete

When implementation is done and tests pass:

```bash
dydo dispatch --role reviewer --task <task-name> --brief "..."
```

The brief should include:
- What you implemented (1-2 sentences)
- Plan deviations and why (if any)
- Key decisions made

Then release:

```bash
dydo agent release
```

---

## If Review Finds Issues

If the reviewer dispatches fixes back to you:

1. Check your inbox: `dydo inbox show`
2. Read the review feedback
3. Fix the issues
4. Dispatch back for re-review

After 2 failed reviews, the task may be escalated to a fresh agent.

---

## Context Recovery

Lost context? Run `dydo whoami` to see your state. Check your workspace for the plan. Return here.
