---
area: guides
type: reference
---

# Workflow Quick Reference

Quick reference for the multi-agent workflow. For full details, see [how-to-use-docs.md](./how-to-use-docs.md).

---

## Session Flow

```bash
# 1. Start
dydo agent claim <name|auto>
dydo whoami                    # Verify

# 2. Set role
dydo agent role <role> --task <name>
dydo agent status              # Verify

# 3. Work
# (edit files within your role's allowed paths)

# 4. Handoff
dydo dispatch --role <role> --task <name> --brief "..."

# 5. End
dydo agent release
```

---

## Roles

| Role | Can Edit |
|------|----------|
| `code-writer` | `src/**`, `tests/**` |
| `reviewer` | (nothing - read only) |
| `docs-writer` | `dydo/**` (except agents/) |
| `interviewer` | Own workspace only |
| `planner` | Own workspace + tasks |

---

## Key Commands

```bash
dydo whoami                    # Current identity
dydo agent status              # Current role and paths
dydo agent list                # All agents
dydo inbox show                # Check incoming work
dydo dispatch ...              # Send to another agent
```

---

## Multi-Agent Principle

> Different agents handle different phases. The agent that writes code does not do its own code review. Fresh eyes catch what authors miss.

---

## Respecting Other Agents

**Do:**
- Check `dydo agent list` before starting
- Use dispatch for handoffs
- Release when done

**Don't:**
- Edit other agents' workspaces
- Claim tasks another agent is working on

---

See [how-to-use-docs.md](./how-to-use-docs.md) for complete documentation.
