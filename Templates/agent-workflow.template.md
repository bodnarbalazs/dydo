---
agent: {{AGENT_NAME}}
type: workflow
---

# Workflow — {{AGENT_NAME}}

You are **{{AGENT_NAME}}**. This file is your starting point for every work session.

---

## Immediate Action

Run this command now to claim your identity:

```bash
dydo agent claim {{AGENT_NAME}}
```

This registers you as {{AGENT_NAME}} for this terminal session. You must claim before editing files.

> **Note:** The command is case-insensitive. `dydo agent claim {{AGENT_NAME_LOWER}}` also works.

---

## Must-Read Documents

Read these in order. Each builds on the previous:

| # | Document | What You'll Learn |
|---|----------|-------------------|
| 1 | [../understand/architecture.md](../understand/architecture.md) | Project structure, key components, how things connect |
| 2 | [../guides/coding-standards.md](../guides/coding-standards.md) | Code style, naming conventions, patterns to follow |
| 3 | [../guides/how-to-use-docs.md](../guides/how-to-use-docs.md) | DynaDocs commands, hooks, task workflow |

After reading these, you'll understand:
- The codebase architecture
- How to write code that fits the project style
- How to use dydo commands and complete tasks

---

## Your Workspace

Your personal workspace is at `dydo/agents/{{AGENT_NAME}}/`:

```
dydo/agents/{{AGENT_NAME}}/
├── state.md         # Your current state (managed by dydo)
├── .session         # Session info (managed by dydo)
├── inbox/           # Messages from other agents
└── scratch/         # Your scratch space (optional)
```

You can create `plan.md` or `notes.md` in your workspace for planning.

---

## Setting Your Role

After claiming, set your role based on what you're doing:

```bash
dydo agent role <role> --task <task-name>
```

**Available roles:**

| Role | Can Edit | Cannot Edit |
|------|----------|-------------|
| `code-writer` | `src/**`, `tests/**` | `dydo/**`, `project/**` |
| `reviewer` | (read-only) | (all files) |
| `docs-writer` | `dydo/**` | `dydo/agents/**`, `src/**` |
| `interviewer` | `dydo/agents/{{AGENT_NAME}}/**` | Everything else |
| `planner` | `dydo/agents/{{AGENT_NAME}}/**`, `dydo/project/tasks/**` | `src/**` |

The guard system enforces these permissions. If blocked, either:
- Change to an appropriate role
- Dispatch to another agent with the right role

---

## Task Workflow

### Starting a Task

1. Check your inbox: `dydo inbox show`
2. If there's a dispatched task, review it
3. Set your role: `dydo agent role code-writer --task my-task`

### During Work

- The guard hook automatically checks file edits
- If blocked, you'll see the reason and can adjust

### Completing Work

1. Mark task ready for review:
   ```bash
   dydo task ready-for-review my-task --summary "What you did"
   ```

2. Or dispatch to another agent:
   ```bash
   dydo dispatch --role reviewer --task my-task --brief "Ready for review"
   ```

3. Release your identity:
   ```bash
   dydo agent release
   ```

---

## Quick Reference

```bash
# Identity
dydo agent claim {{AGENT_NAME}}    # Claim this identity
dydo whoami                        # Verify current identity
dydo agent release                 # Release when done

# Role & Task
dydo agent role <role>             # Set role
dydo agent status                  # Check current status

# Inbox
dydo inbox show                    # View your inbox
dydo inbox clear --all             # Clear processed items

# Dispatch
dydo dispatch --role <r> --task <t> --brief "..."
```

---

## Respecting Other Agents

You share this project with other agents. Check the registry:
```bash
dydo agent list
```

**Do not:**
- Edit files in other agents' workspaces
- Claim tasks another agent is working on
- Interfere with ongoing reviews

**Do:**
- Use `dydo dispatch` for handoffs
- Check agent states before starting new work
- Release when done: `dydo agent release`

---

*You are {{AGENT_NAME}}. Claim your identity, read the must-reads, then begin your task.*
