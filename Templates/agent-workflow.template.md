---
agent: {{AGENT_NAME}}
type: workflow
---

# {{AGENT_NAME}} — Workflow

You are **{{AGENT_NAME}}**. Follow these steps in order.

---

## Step 1: Claim Your Identity

Run this command:

```bash
dydo agent claim {{AGENT_NAME}}
```

---

## Step 2: Verify — CHECKPOINT

**Do not proceed until this works.** Run:

```bash
dydo whoami
```

You should see output showing you are {{AGENT_NAME}}. If you see an error:

| Error | Solution |
|-------|----------|
| "DYDO_HUMAN not set" | Ask the human to run: `export DYDO_HUMAN=theirname` |
| "Agent is assigned to X, not Y" | You're claiming an agent assigned to a different human |
| "Agent is already claimed" | Another session has this agent. Try `dydo agent claim auto` |

**Once `dydo whoami` shows {{AGENT_NAME}}, proceed to Step 3.**

---

## Step 3: Read the Must-Reads

Read these documents **in order**. Each builds on the previous:

| # | Document | Purpose |
|---|----------|---------|
| 1 | [../understand/architecture.md](../understand/architecture.md) | How the codebase is structured |
| 2 | [../guides/coding-standards.md](../guides/coding-standards.md) | Code style and patterns to follow |
| 3 | [../guides/how-to-use-docs.md](../guides/how-to-use-docs.md) | DynaDocs commands and task workflow |

After reading:
- You understand the project architecture
- You know the coding conventions
- You know how to use dydo commands

---

## Step 4: Check Your Inbox

Before starting new work, check if work was dispatched to you:

```bash
dydo inbox show
```

If there's a dispatched task, that's your priority. Read the brief and proceed.

If your inbox is empty, proceed with whatever task you were given.

---

## Step 5: Set Your Role

Your role determines what files you can edit. Set it before making changes:

```bash
dydo agent role <role> --task <task-name>
```

**Available roles:**

| Role | Can Edit | Use When |
|------|----------|----------|
| `code-writer` | `src/**`, `tests/**` | Writing or modifying code |
| `reviewer` | (read-only) | Reviewing code, no edits |
| `docs-writer` | `dydo/**` (except agents/) | Writing documentation |
| `interviewer` | `dydo/agents/{{AGENT_NAME}}/**` | Gathering requirements from human |
| `planner` | Own workspace + `dydo/project/tasks/**` | Planning work, creating tasks |

**Example:**
```bash
dydo agent role code-writer --task implement-auth
```

---

## Step 6: Verify Role — CHECKPOINT

Before editing any files, confirm your permissions:

```bash
dydo agent status
```

This shows your current role and allowed paths. If you try to edit outside your allowed paths, the guard will block you.

**Now you can begin work.**

---

## During Work

The guard hook automatically validates edits. If blocked:

1. **Wrong role?** Change it: `dydo agent role <correct-role>`
2. **Need different permissions?** Dispatch to another agent with the right role
3. **Unsure?** Run `dydo agent status` to see your current permissions

---

## Completing Work

When finished:

1. **If code needs review:**
   ```bash
   dydo task ready-for-review <task-name> --summary "What you did"
   dydo dispatch --role reviewer --task <task-name> --brief "Ready for review"
   ```

2. **If dispatching to another role:**
   ```bash
   dydo dispatch --role <role> --task <task-name> --brief "What needs doing"
   ```

3. **When completely done:**
   ```bash
   dydo agent release
   ```

---

## Your Workspace

Your personal workspace is at `dydo/agents/{{AGENT_NAME}}/`:

```
dydo/agents/{{AGENT_NAME}}/
├── state.md     # Current role, task, permissions (managed by dydo)
├── .session     # Session tracking (managed by dydo)
└── inbox/       # Messages from other agents
```

You can create notes in your workspace if needed.

---

## Quick Reference

```bash
# Identity
dydo whoami                        # Show current identity
dydo agent status                  # Show role and permissions
dydo agent release                 # Release when done

# Role
dydo agent role <role> --task <t>  # Set role

# Inbox & Dispatch
dydo inbox show                    # Check incoming work
dydo dispatch --role <r> --task <t> --brief "..."

# Task
dydo task ready-for-review <t> --summary "..."
```

---

## Respecting Other Agents

```bash
dydo agent list                    # See who's working on what
```

- Do not edit other agents' workspaces
- Do not claim tasks another agent is working on
- Use dispatch for handoffs, don't interfere directly

---

**Summary:** Claim → Verify with `whoami` → Read must-reads → Check inbox → Set role → Verify with `status` → Work → Release
