---
area: general
type: entry
---

# DynaDocs

Welcome to a **DynaDocs** project — a system combining living documentation with AI agent workflow management.

---

## For AI Agents — Start Here

If you are an AI agent (Claude, GPT, etc.) starting a work session, follow these steps:

### Step 1: Verify Environment

Check that `DYDO_HUMAN` is set (identifies which human is operating the terminal):

```bash
echo $DYDO_HUMAN    # Bash/Zsh
echo $env:DYDO_HUMAN  # PowerShell
```

If not set, ask the human to set it:
```bash
export DYDO_HUMAN=humanname    # Bash/Zsh
$env:DYDO_HUMAN = "humanname"  # PowerShell
```

### Step 2: Choose Your Identity

Each agent session operates under a **named identity**. Pick one and read its workflow file:

| Agent | Workflow File |
|-------|---------------|
| Adele | [workflows/adele.md](workflows/adele.md) |
| Brian | [workflows/brian.md](workflows/brian.md) |
| Charlie | [workflows/charlie.md](workflows/charlie.md) |
| ... | See `dydo/workflows/` for all agents |

Or claim automatically and the system will assign you:
```bash
dydo agent claim auto
dydo whoami  # Shows which agent was assigned
```

Then read your workflow file at `dydo/workflows/{yourname}.md`.

### Step 3: Follow Your Workflow

Your workflow file contains:
- **Immediate action**: Command to claim your identity
- **Must-read documents**: Architecture, coding standards, how-to guide
- **Role permissions**: What files you can edit based on your role
- **Task workflow**: How to start, execute, and complete work

After reading your workflow file, you will:
1. Claim your identity: `dydo agent claim <name>`
2. Read the must-read documents (in order)
3. Set your role: `dydo agent role <role> --task <taskname>`
4. Begin your task

---

## For Humans

DynaDocs provides:

- **Living Documentation** — Validated, cross-linked docs in `understand/`, `guides/`, `reference/`, `project/`
- **Agent Workflow** — Multi-agent orchestration with role-based file permissions
- **Task Management** — Tracked tasks with handoff support between agents

### Quick Setup

```bash
# Initialize a new project
dydo init claude                    # With Claude Code hooks
dydo init none                      # Without hooks

# Join an existing project
dydo init claude --join

# Set your identity (add to shell profile)
export DYDO_HUMAN=yourname
```

### Key Commands

```bash
# Agent workflow
dydo agent claim auto               # Claim first available agent
dydo agent claim Adele              # Claim specific agent
dydo whoami                         # Show current identity
dydo agent role code-writer         # Set role
dydo agent release                  # Release when done

# Task management
dydo dispatch --role reviewer --task mytask --brief "..."
dydo inbox show                     # View inbox

# Documentation
dydo check                          # Validate documentation
dydo fix                            # Auto-fix issues
```

---

## Project Structure

```
project/
├── dydo.json                       # Configuration (agents, assignments)
├── CLAUDE.md                       # Entry point → here
│
└── dydo/
    ├── index.md                    ← You are here
    ├── workflows/                  # Agent workflow files
    │   ├── adele.md
    │   ├── brian.md
    │   └── ...
    │
    ├── understand/                 # Core concepts & architecture
    │   └── architecture.md
    │
    ├── guides/                     # How-to guides
    │   ├── coding-standards.md
    │   └── how-to-use-docs.md
    │
    ├── reference/                  # API & configuration specs
    │
    ├── project/                    # Meta: decisions, tasks, changelog
    │   ├── tasks/                  # Cross-human task dispatch
    │   ├── decisions/              # Architecture decision records
    │   └── changelog/              # Change history
    │
    └── agents/                     # Agent workspaces (GITIGNORED)
        └── AgentName/
            ├── state.md            # Current role, task, permissions
            ├── .session            # Session info (PID, claimed time)
            └── inbox/              # Messages from other agents
```

---

## Configuration (dydo.json)

The `dydo.json` file at project root defines:

```json
{
  "version": 1,
  "structure": {
    "root": "dydo"
  },
  "agents": {
    "pool": ["Adele", "Brian", "Charlie", ...],
    "assignments": {
      "humanname": ["Adele", "Brian", "Charlie"]
    }
  },
  "integrations": {
    "claude": true
  }
}
```

- **pool**: Available agent names
- **assignments**: Which human can claim which agents
- **integrations**: Hook configurations (claude = Claude Code hooks)

---

## Role-Based Permissions

When an agent sets a role, they gain specific file permissions:

| Role | Can Edit | Cannot Edit |
|------|----------|-------------|
| `code-writer` | `src/**`, `tests/**` | `dydo/**`, `project/**` |
| `reviewer` | (read-only) | (all files) |
| `docs-writer` | `dydo/**` | `dydo/agents/**`, `src/**` |
| `interviewer` | `dydo/agents/{self}/**` | Everything else |
| `planner` | `dydo/agents/{self}/**`, `dydo/project/tasks/**` | `src/**` |

The guard system enforces these permissions. If blocked, change your role or dispatch to another agent.

---

## Next Steps

- **AI Agents**: Pick a workflow file from `dydo/workflows/` and follow it
- **Humans**: Run `dydo init` to set up, then see [guides/how-to-use-docs.md](guides/how-to-use-docs.md)
