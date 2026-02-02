---
agent: {{AGENT_NAME}}
type: workflow
---

# {{AGENT_NAME}}

You are **{{AGENT_NAME}}**. Follow these steps.

---

## 1. Claim Your Identity

```bash
dydo agent claim {{AGENT_NAME}}
```

---

## 2. Your Assignment

Your prompt contains your task. It may include a workflow flag which determines your starting mode:

| Flag | Mode | Go To |
|------|------|-------|
| `--feature` | Full: interview → plan → code → review | [modes/interviewer.md](modes/interviewer.md) |
| `--task` | Standard: plan → code → review | [modes/planner.md](modes/planner.md) |
| `--quick` | Light: just implement | [modes/code-writer.md](modes/code-writer.md) |
| `--think` | Collaborative exploration | [modes/co-thinker.md](modes/co-thinker.md) |
| `--review` | Code review | [modes/reviewer.md](modes/reviewer.md) |
| `--docs` | Documentation | [modes/docs-writer.md](modes/docs-writer.md) |
| `--test` | Testing & validation | [modes/tester.md](modes/tester.md) |
| `--inbox` | Dispatched work | Check inbox below |

**No flag?**
Infer the mode from intent. If ambiguous, ask.

---

## Before You Start

If you had to infer the mode, state your interpretation:

> "I understand you want [X]. Proceeding as [mode] on [task]. Correct me if I'm wrong."

If the request is unrelated to your current task, point it out:

> "This seems separate from [current-task]. Should I continue here or start fresh?"

You should maintain good context hygiene. If the previous task is largely disjunct, it's better to start fresh.

---

## 3. If `--inbox` Mode

Check what's been dispatched to you:

```bash
dydo inbox show
```

The inbox message tells you:
- What task
- What role to take
- Brief context

Set your role based on the inbox instructions, then go to the appropriate mode file.

---

## 4. Follow Your Mode

Go to the mode file linked above. It contains:
- **Must-reads** specific to your mode
- **Role setup** command
- **Work guidance** for that mode
- **Completion** instructions

---

## Checkpoint

Before making any edits, verify your setup:

```bash
dydo whoami          # Confirm: {{AGENT_NAME}}
dydo agent status    # Confirm: role and permissions set
```

If either command shows an error, fix it before proceeding.

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
dydo inbox clear                         # Clear processed items

# Dispatch
dydo dispatch --role <r> --task <t> --brief "..."
```

---

## Project Terminology

If you encounter unfamiliar project-specific terms, check [glossary.md](../../glossary.md).

---

*Full command reference: [how-to-use-docs.md](../../guides/how-to-use-docs.md)*
