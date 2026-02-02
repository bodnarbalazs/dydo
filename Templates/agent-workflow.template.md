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

**Note:** If you ran `dydo agent claim auto` for any reason, mention it to the user.

---

## 2. Understand Your Assignment

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
| `--inbox` | Dispatched work | See step 4 |

**No flag?** Infer the mode from intent. If ambiguous, ask.

---

## 3. Confirm Your Interpretation

If you inferred the mode, state your interpretation:

> "I understand you want [X]. Proceeding as [mode] on [task]. Correct me if I'm wrong."

If the request seems unrelated to your current task:

> "This seems separate from [current-task]. Should I continue here or start fresh?"

Maintain good context hygiene. If the previous task is largely disjunct, it's better to start fresh.

---

## 4. Handle Inbox

**No `--inbox` flag?** Skip to step 5.

If you have dispatched work:

```bash
dydo inbox show
```

For each inbox item:
1. Read the brief to understand the task
2. Set your role: `dydo agent role <role> --task <task-name>`
3. Go to the appropriate mode file

**Important:** You cannot release while inbox has unprocessed items. Archived items are kept in `inbox/archive/` (last 10 preserved).

---

## 5. Checkpoint

Before making any edits, verify your setup:

```bash
dydo whoami          # Confirm: {{AGENT_NAME}}
dydo agent status    # Confirm: role and permissions set
```

If either command shows an error, fix it before proceeding.

---

## 6. Follow Your Mode

Go to your mode file. It contains:
- **Must-reads** specific to your role
- **Role setup** command
- **Work guidance** for that role
- **Completion** instructions

---

## 7. Complete

Follow your mode's completion instructions. Generally:

1. **Dispatch** to the next role if handing off work
2. **Clear inbox** if you processed dispatched items: `dydo inbox clear --all`
3. **Release** your identity: `dydo agent release`

**Note:** After dispatching, you may receive a response (e.g., reviewer feedback). Check your inbox if re-engaged.

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
dydo dispatch --role <r> --task <t> --brief "..."
```

---

## Context Recovery

Lost context? Run `dydo whoami` to see your state. Check your workspace for notes and drafts. Return to your mode file.
