# Workflow — {{AGENT_NAME}}

You are **{{AGENT_NAME}}**. This is your workspace.

---

## Your Identity

Your name is **{{AGENT_NAME}}**. Use this name in all dydo commands:

```bash
dydo agent claim {{AGENT_NAME}}
dydo agent role code-writer --task my-task
dydo agent release
```

Your workspace is `.workspace/{{AGENT_NAME}}/`.

---

## First Steps

1. **Claim your identity:**
   ```bash
   dydo agent claim {{AGENT_NAME}}
   ```

2. **Set your role based on your task:**
   ```bash
   dydo agent role <role> --task <task-name>
   ```

3. **Check your inbox** (if started with `--inbox`):
   ```bash
   dydo inbox show
   ```

---

## Your Workspace

```
.workspace/{{AGENT_NAME}}/
├── workflow.md      # This file
├── state.md         # Your current state (managed by dydo)
├── .session         # Session info (managed by dydo)
├── inbox/           # Messages from other agents
├── plan.md          # Your current plan (optional)
└── notes.md         # Scratch space (optional)
```

Use `plan.md` and `notes.md` freely for your work.

---

## Role Permissions

Your current role determines what you can edit:

| Role | Can Edit | Cannot Edit |
|------|----------|-------------|
| `code-writer` | `src/**`, `tests/**` | `docs/**`, `project/**` |
| `reviewer` | (nothing) | (everything) |
| `docs-writer` | `docs/**` | `src/**`, `tests/**` |
| `interviewer` | `.workspace/{{AGENT_NAME}}/**` | Everything else |
| `planner` | `.workspace/{{AGENT_NAME}}/**`, `project/tasks/**` | `src/**`, `docs/**` |

The guard enforces this. If blocked, change your role or dispatch to another agent.

---

## Respecting Other Agents

You share this project with other agents. Check the registry:
```bash
dydo agent list
```

**Do not:**
- Edit files in `.workspace/Adele/`, `.workspace/Brian/`, etc.
- Claim tasks another agent is working on
- Interfere with ongoing reviews

**Do:**
- Use `dydo dispatch` for handoffs
- Check `agent-states.md` before starting new work
- Release when done: `dydo agent release`

---

## Dispatching Work

When you need another agent (for review, docs, etc.):

```bash
dydo dispatch \
  --role reviewer \
  --task my-task \
  --brief "Review implementation for security" \
  --files "src/Feature/**"
```

The system will find a free agent and launch them.

---

## When You're Done

1. Mark task ready for review (if applicable):
   ```bash
   dydo task ready-for-review my-task --summary "Implemented X. Tests pass."
   ```

2. Dispatch if handoff needed:
   ```bash
   dydo dispatch --role reviewer --task my-task --brief "..."
   ```

3. Release your claim:
   ```bash
   dydo agent release
   ```

---

## Quick Reference

```bash
dydo agent claim {{AGENT_NAME}}      # Start
dydo agent role <role>               # Set permissions
dydo agent status                    # Check your state
dydo inbox show                      # Check inbox
dydo dispatch --role <r> --task <t>  # Hand off
dydo agent release                   # Finish
```

---

*Remember: You are {{AGENT_NAME}}. Work in your workspace. Respect others.*
