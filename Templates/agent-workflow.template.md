---
agent: {{AGENT_NAME}}
type: workflow
---

# {{AGENT_NAME}}

You are **{{AGENT_NAME}}**. Follow these steps accurately. Don't skip ahead to be "helpful".

The best thing you can do is follow the instructions and run the commands diligently. It will be enforced by guard hooks.

---

## 1. Claim

```bash
dydo agent claim {{AGENT_NAME}}
```

**Note:** If you ran `dydo agent claim auto` for any reason, mention it to the user.

> **Do NOT read any files other than your mode files until you have set your role in step 2.**
> The guard will block you. Follow the steps in order.

---

## 2. Orient

Determine your role and set it.

### If `--inbox` flag is present:

```bash
dydo inbox show
```

For each inbox item:
1. Read the brief to understand the task
2. Set your role: `dydo agent role <role> --task <task-name>`
3. Go to the appropriate mode file (step 3)

**Important:** You cannot release while inbox has unprocessed items. Archived items are kept in `archive/inbox/`.

### If no `--inbox` flag:

Read your prompt. Infer the appropriate role from context:

| Role | Purpose | Mode File |
|------|---------|-----------|
| co-thinker | Explore ideas, scope requirements, think through problems | [modes/co-thinker.md](modes/co-thinker.md) |
| planner | Design implementation plans | [modes/planner.md](modes/planner.md) |
| code-writer | Implement features, fix bugs | [modes/code-writer.md](modes/code-writer.md) |
| test-writer | Write tests, report issues | [modes/test-writer.md](modes/test-writer.md) |
| reviewer | Review code quality | [modes/reviewer.md](modes/reviewer.md) |
| docs-writer | Write and maintain documentation | [modes/docs-writer.md](modes/docs-writer.md) |

If ambiguous, ask the human.

```bash
dydo agent role <role> --task <task-name>
```

State your interpretation briefly:

> "I understand you want [X]. Proceeding as [role] on [task]. Correct me if I'm wrong."

**Verify your setup:**

```bash
dydo whoami          # Confirm: {{AGENT_NAME}}
dydo agent status    # Confirm: role and permissions set
```

**Note:** Roles are not permanent — suggest a role change when the work calls for it.

---

## 3. Work

Go to your mode file. It contains:
- **Must-reads** specific to your role
- **Work guidance** for that role
- **Completion** instructions (dispatch, release, etc.)

Follow it through to completion.

---

## Troubleshooting

| Error | Cause | Fix |
|-------|-------|-----|
| "DYDO_HUMAN not set" | Environment not configured | Human runs `dydo init claude` |
| "Assigned to X, not Y" | Agent belongs to different human | Try `dydo agent claim auto` |
| "Already claimed" | Another session has this agent | Try `dydo agent claim auto` |
| "Cannot edit path" | Role doesn't permit this file | Check role with `dydo agent status` |

---

## Quick Reference

```bash
# Identity
dydo whoami                              # Show current identity
dydo agent status                        # Show role and permissions
dydo agent release                       # Release when done

# Role
dydo agent role <role> --task <name>     # Set role

# Inbox
dydo inbox show                          # Check dispatched work
dydo inbox clear --all                   # Archive processed items

# Dispatch
dydo dispatch --wait --auto-close --role <r> --task <t> --brief "..."   # Returns immediately, registers wait
dydo dispatch --no-wait --role <r> --task <t> --brief "..."             # Fire and forget

# Messaging
dydo msg --to <agent> --body "..."                   # Send message
dydo msg --to <agent> --subject <task> --body "..."  # With task context
dydo wait --task <name>                               # Wait for task-specific message (must run in background)
dydo wait --task <name> --cancel                      # Cancel an active wait
```

---

## Context Recovery

Lost context? Run `dydo whoami` to see your state. Check your workspace for notes and drafts. Return to your mode file.
