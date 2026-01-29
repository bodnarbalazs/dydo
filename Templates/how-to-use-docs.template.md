---
area: guides
type: guide
---

# How to Use DynaDocs

Practical guide to dydo commands, the agent workflow, and task management.

---

## Before You Start

You should have already:
1. Read [../understand/architecture.md](../understand/architecture.md) — Project structure
2. Read [./coding-standards.md](./coding-standards.md) — Code conventions

This guide shows you how to use the dydo system to coordinate your work.

---

## The Command Cycle

Every work session follows this pattern:

```
┌─────────────────────────────────────────────────────────┐
│  1. CLAIM      dydo agent claim <name>                  │
│  2. VERIFY     dydo whoami                              │
│  3. SET ROLE   dydo agent role <role> --task <name>     │
│  4. VERIFY     dydo agent status                        │
│  5. WORK       (edit files within your role's paths)    │
│  6. HANDOFF    dydo dispatch --role <role> --task ...   │
│  7. RELEASE    dydo agent release                       │
└─────────────────────────────────────────────────────────┘
```

---

## 1. Claiming Your Identity

```bash
dydo agent claim Adele          # Claim specific agent
dydo agent claim auto           # Claim first free agent
```

**Why this matters:** Your identity determines your workspace, tracks your session, and validates file permissions.

**If claim fails:**

| Error | Cause | Solution |
|-------|-------|----------|
| "DYDO_HUMAN not set" | Environment variable missing | `export DYDO_HUMAN=yourname` |
| "Agent assigned to X, not Y" | Agent belongs to another human | Try a different agent or `claim auto` |
| "Agent is already claimed" | Another session has this agent | Try `claim auto` for a free agent |

---

## 2. Verifying Your Identity — CHECKPOINT

**Always run this after claiming:**

```bash
dydo whoami
```

Expected output:
```
Agent: Adele
Human: yourname
Status: working
Role: (none)
Workspace: dydo/agents/Adele/
```

**Do not proceed if this shows an error.** Fix the issue first.

---

## 3. Setting Your Role

Your role controls which files you can edit:

```bash
dydo agent role code-writer --task implement-auth
```

**Available roles:**

| Role | Can Edit | Typical Use |
|------|----------|-------------|
| `code-writer` | `src/**`, `tests/**` | Writing/modifying code |
| `reviewer` | (nothing) | Code review (read-only) |
| `co-thinker` | Own workspace + `dydo/project/decisions/**` | Collaborative thinking, decisions |
| `docs-writer` | `dydo/**` (except agents/) | Writing documentation |
| `interviewer` | Own workspace only | Requirements gathering |
| `planner` | Own workspace + `dydo/project/tasks/**` | Planning, task creation |

---

## 4. Verifying Your Role — CHECKPOINT

**Before editing any files:**

```bash
dydo agent status
```

This shows your current role and the paths you can edit. The guard will block edits outside these paths.

---

## 5. The Guard System

When you try to edit a file, the guard hook checks:
1. Are you claimed as an agent?
2. Does your role allow editing this path?

**If blocked:**
- Check your role: `dydo agent status`
- Change role if needed: `dydo agent role <correct-role>`
- Or dispatch to an agent with the right role

---

## 6. Dispatching Work

When you need another agent (for review, docs, etc.):

```bash
dydo dispatch \
  --role reviewer \
  --task implement-auth \
  --brief "JWT implementation ready for security review"
```

**What happens:**
- Finds a free agent (same human) with that role capability
- Creates a message in their inbox
- Returns the agent name

**Cross-human dispatch:**
If no local agent is available, creates a task in `dydo/project/tasks/` (committed to git, visible to all).

---

## 7. Checking Your Inbox

Before starting new work:

```bash
dydo inbox show
```

If there's work waiting, that's your priority.

After processing:

```bash
dydo inbox clear
```

---

## 8. Releasing Your Identity

When done with your session:

```bash
dydo agent release
```

This frees the agent for other sessions.

---

## Task Lifecycle

```
[created] → [in-progress] → [review-pending] → [approved] → [closed]
                                   ↓
                             [needs-work] (if review fails)
```

### Creating a Task

```bash
dydo task create "Implement JWT authentication"
```

### Marking Ready for Review

```bash
dydo task ready-for-review implement-auth --summary "Added JWT with refresh tokens, 23 tests"
```

### After Review (human)

```bash
dydo task approve implement-auth
dydo task reject implement-auth --notes "Need rate limiting"
```

---

## Quick Reference

```bash
# Identity
dydo whoami                              # Show current identity
dydo agent claim <name|auto>             # Claim agent
dydo agent release                       # Release agent

# Role
dydo agent role <role> --task <name>     # Set role
dydo agent status                        # Show role and permissions

# Inbox
dydo inbox show                          # View inbox
dydo inbox clear                         # Clear processed items

# Dispatch
dydo dispatch --role <r> --task <t> --brief "..."

# Tasks
dydo task create "description"
dydo task ready-for-review <name> --summary "..."
dydo task list

# Documentation
dydo check                               # Validate docs
dydo fix                                 # Auto-fix issues

# Agent management (humans)
dydo agent list                          # List all agents
dydo agent new <name> <human>            # Create agent
dydo agent reassign <name> <human>       # Reassign agent
```

---

## Common Mistakes

### Forgetting to claim before editing

**Symptom:** Guard blocks all edits

**Fix:** Run `dydo agent claim auto` first

### Wrong role for the task

**Symptom:** Guard blocks edits to specific paths

**Fix:** Run `dydo agent status` to see your current role, then `dydo agent role <correct-role>`

### Editing files outside your role

**Symptom:** "Cannot edit X. Role Y cannot edit this path."

**Fix:** Either change your role or dispatch to another agent

### Not releasing when done

**Symptom:** Agent stays "working" after session ends

**Impact:** Other sessions can't claim that agent

**Fix:** Always run `dydo agent release` when done (stale sessions are auto-cleaned eventually)

---

## Related

- [Coding Standards](./coding-standards.md) — Code conventions
- [Architecture](../understand/architecture.md) — Project structure
