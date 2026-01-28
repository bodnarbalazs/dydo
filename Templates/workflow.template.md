---
area: general
type: reference
---

# Workflow

How work flows through this project using the multi-agent system.

---

## Overview

This project uses a multi-agent orchestration system. Each agent:
- Has a unique identity (Adele, Brian, Charlie, etc.)
- Operates in a dedicated workspace
- Has role-based permissions
- Coordinates with other agents via dispatch

**Key principle:** Different agents handle different phases. The agent that writes code does not do it's own code review. Fresh eyes catch what authors miss.

---

## Workflow Modes

Your prompt may include a flag that determines your workflow:

| Flag | Mode | Steps |
|------|------|-------|
| `--feature X` | Full | Interview → Plan → Implement → Review → Docs |
| `--task X` | Standard | Plan → Implement → Review |
| `--quick X` | Light | Just implement |
| `--inbox X` | Inbox | Process pending dispatches |
| `--review X` | Review | Code review only |

The letter `X` determines your agent identity:
- A = Adele, B = Brian, C = Charlie, D = Dexter, E = Emma...

**If no flag provided:** Ask the human which workflow to follow.

---

## Your Identity

When you start, read your agent-specific workflow file:
`.workspace/{YourName}/workflow.md`

This file contains:
- Your name (use it in all dydo commands)
- Your current permissions based on role
- Instructions specific to you

**First action after reading index.md:**
```bash
dydo agent claim {YourName}
```

This registers you in the system and tracks your terminal session.

---

## Agent Roles

Your role determines what you can edit:

| Role | Can Edit | Cannot Edit |
|------|----------|-------------|
| `code-writer` | `src/**`, `tests/**` | `docs/**`, `project/**` |
| `reviewer` | (nothing) | (everything) |
| `docs-writer` | `docs/**` | `src/**`, `tests/**` |
| `interviewer` | `.workspace/{self}/**` | Everything else |
| `planner` | `.workspace/{self}/**`, `project/tasks/**` | `src/**`, `docs/**` |

Set your role:
```bash
dydo agent role code-writer --task jwt-auth
```

The guard will enforce permissions. Trust it.

---

## Task Lifecycle

```
pending → active → review-pending → human-reviewed → closed
                        ↓
                   review-failed → active
```

### Creating a Task

```bash
dydo task create "JWT authentication implementation"
```

### Working on a Task

Update progress in your workspace `state.md` or the task file.

### Ready for Review

When code is complete:
```bash
dydo task ready-for-review jwt-auth --summary "Implemented JWT with refresh tokens. 23 tests, all passing."
```

This marks the task `review-pending` and includes a summary for the reviewer.

### After Review

- **Pass:** Reviewer creates changelog, dispatches docs task if needed
- **Fail:** Same agent fixes issues (context continuity), then re-requests review

### Human Approval

Tasks marked `review-pending` need human approval:
```bash
dydo task approve jwt-auth      # Human runs this
dydo task reject jwt-auth "..."  # Human runs this
```

---

## Cross-Agent Dispatch

When you need another agent (for review, docs, etc.):

```bash
dydo dispatch \
  --role reviewer \
  --task jwt-auth \
  --brief "Review JWT implementation for security" \
  --files "src/Auth/**"
```

This:
1. Finds the first free agent alphabetically
2. Writes the request to their inbox
3. Launches a new terminal for them
4. Returns: "Dispatched to {AgentName}"

**Do not wait.** Continue your work or release if done.

---

## Processing Your Inbox

If started with `--inbox X`:

```bash
dydo inbox show
```

Process each item:
1. Read the brief and files
2. Perform the requested role (review, docs, etc.)
3. Complete the request:
   ```bash
   dydo review complete jwt-auth --status pass --notes "LGTM"
   ```
4. Clear processed items:
   ```bash
   dydo inbox clear
   ```

---

## Respecting Other Agents

Check who's working on what:
```bash
dydo agent list
```

**Do not:**
- Edit files in another agent's workspace
- Claim tasks another agent is working on
- Interfere with ongoing work

**Do:**
- Check `agent-states.md` before starting
- Use dispatch for handoffs
- Release when done: `dydo agent release`

---

## The Guard

Hooks call `dydo guard` to enforce permissions.

If you try to edit a file outside your role's allowed paths:
```
DENIED: code-writer cannot edit docs/
```

This is working as intended. Change your role or dispatch to another agent.

---

## Quick Reference

```bash
# Lifecycle
dydo agent claim {Name}          # Start session
dydo agent role {role}           # Set permissions
dydo agent release               # End session

# Tasks
dydo task create "description"
dydo task ready-for-review {name} --summary "..."
dydo tasks --needs-review        # List pending reviews

# Dispatch
dydo dispatch --role {role} --task {name} --brief "..."

# Inbox
dydo inbox show
dydo inbox clear

# Review
dydo review complete {task} --status pass|fail

# Status
dydo agent list
dydo agent status
```

---

## Related

- [Coding Standards](./coding-standards.md) — Code conventions
- [Documentation System](./docs-system.md) — Doc structure
- [Tasks](./../tasks/_index.md) — Active and completed tasks
