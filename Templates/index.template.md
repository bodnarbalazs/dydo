---
area: general
type: entry
---

# DynaDocs

This project uses **DynaDocs** for documentation and AI agent workflow.

---

## AI Agents — Your Entry Point

You need a **named identity** to work on this project. Each identity has its own workflow file with instructions.

### Option A: You Know Your Identity

If you were told to be a specific agent (e.g., "You are Adele"), go directly to your workflow file:

**→ [workflows/](workflows/_index.md)** — Pick your workflow file and follow it

### Option B: Auto-Assign

If no identity was specified, claim one automatically:

```bash
dydo agent claim auto
```

Then check which identity you received:

```bash
dydo whoami
```

**If `dydo whoami` shows an error or "No agent identity":**
- The `DYDO_HUMAN` environment variable may not be set
- Ask the human to run: `export DYDO_HUMAN=theirname`
- Then try claiming again

Once you have an identity, read your workflow file at `workflows/{yourname}.md`.

---

## Humans — Quick Reference

```bash
# Setup
dydo init claude                    # Initialize with Claude Code hooks
dydo init claude --join             # Join existing project
export DYDO_HUMAN=yourname          # Set your identity (add to shell profile)

# Agent management
dydo agent list                     # See all agents
dydo agent new <name> <human>       # Create new agent
dydo agent reassign <name> <human>  # Reassign agent

# Documentation
dydo check                          # Validate docs
dydo fix                            # Auto-fix issues
```

See [guides/how-to-use-docs.md](guides/how-to-use-docs.md) for full reference.

---

## Project Structure

```
dydo/
├── index.md              ← You are here
├── workflows/            # Agent workflow files (start here)
├── understand/           # Architecture, concepts
├── guides/               # How-to guides
├── reference/            # API specs, configs
├── project/              # Decisions, tasks, changelog
└── agents/               # Agent workspaces (gitignored)
```
