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
- Operates in a dedicated workspace at `dydo/agents/{AgentName}/`
- Has role-based file permissions
- Coordinates with other agents via dispatch

**Key principle:** Different agents handle different phases. The agent that writes code does not do its own code review. Fresh eyes catch what authors miss.

---

## Environment Setup

Before starting, ensure `DYDO_HUMAN` is set to identify which human is operating:

```bash
# Bash/Zsh
export DYDO_HUMAN=yourname

# PowerShell
$env:DYDO_HUMAN = "yourname"
```

This determines which agents you can claim (from your assignment in `dydo.json`).

---

## Your Identity

When you start, read your agent-specific workflow file at:
`dydo/workflows/{yourname}.md`

This file contains:
- Your name (use it in all dydo commands)
- Must-read documents (architecture, coding standards, how-to guide)
- Your permissions based on role
- Instructions specific to you

**First action:**
```bash
dydo agent claim {YourName}   # Or: dydo agent claim auto
dydo whoami                   # Verify identity
```

This registers you in the system and tracks your terminal session.

---

## Agent Roles

Your role determines what you can edit:

| Role | Can Edit | Cannot Edit |
|------|----------|-------------|
| `code-writer` | `src/**`, `tests/**` | `dydo/**`, `project/**` |
| `reviewer` | (read-only) | (all files) |
| `docs-writer` | `dydo/**` | `dydo/agents/**`, `src/**` |
| `interviewer` | `dydo/agents/{self}/**` | Everything else |
| `planner` | `dydo/agents/{self}/**`, `dydo/project/tasks/**` | `src/**` |

Set your role:
```bash
dydo agent role code-writer --task jwt-auth
```

The guard enforces these permissions. If blocked, change your role or dispatch to another agent.

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
1. Finds the first free agent assigned to the current human
2. Writes the request to their inbox
3. Returns: "Dispatched to {AgentName}"

**Cross-human dispatch:** If the target role's agents are assigned to a different human, creates a task file in `dydo/project/tasks/` instead (committed to git, visible to all).

**Do not wait.** Continue your work or release if done.

---

## Processing Your Inbox

Check your inbox:
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
- Edit files in another agent's workspace (`dydo/agents/OtherAgent/`)
- Claim tasks another agent is working on
- Interfere with ongoing work

**Do:**
- Check agent states before starting
- Use dispatch for handoffs
- Release when done: `dydo agent release`

---

## The Guard

The `dydo guard` command is called by hooks (e.g., Claude Code PreToolUse) to enforce permissions.

If you try to edit a file outside your role's allowed paths:
```
Agent Adele (code-writer) cannot edit dydo/guides/setup.md.
code-writer role cannot edit dydo/** paths.
```

This is working as intended. Either:
- Change your role: `dydo agent role docs-writer`
- Dispatch to another agent with the right role

---

## Quick Reference

```bash
# Identity
dydo agent claim {Name}          # Start session
dydo agent claim auto            # Auto-claim first free
dydo whoami                      # Show current identity
dydo agent role {role}           # Set permissions
dydo agent release               # End session

# Tasks
dydo task create "description"
dydo task ready-for-review {name} --summary "..."

# Dispatch
dydo dispatch --role {role} --task {name} --brief "..."

# Inbox
dydo inbox show
dydo inbox clear

# Review
dydo review complete {task} --status pass|fail

# Status
dydo agent list
dydo agent list --free
```

---

## Related

- [Coding Standards](./coding-standards.md) — Code conventions
- [Documentation System](./docs-system.md) — Doc structure
- [Tasks](../project/tasks/_index.md) — Active and completed tasks
