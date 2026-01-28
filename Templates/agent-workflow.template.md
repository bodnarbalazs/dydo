---
agent: {{AGENT_NAME}}
type: workflow
---

# {{AGENT_NAME}}

Follow these steps in order.

---

## 1. Claim

```bash
dydo agent claim {{AGENT_NAME}}
```

---

## 2. Must-Reads

| Document | Purpose |
|----------|---------|
| [architecture.md](../understand/architecture.md) | Codebase structure |
| [coding-standards.md](../guides/coding-standards.md) | Code conventions |

---

## 3. Checkpoint

```bash
dydo whoami
```

Confirm you see `{{AGENT_NAME}}`. If error, see **Troubleshooting** below.

---

## 4. Inbox

```bash
dydo inbox show
```

Work waiting? That's your priority. Otherwise, continue with your assigned task.

---

## 5. Role

Your prompt may have included a workflow flag:

| Flag | Workflow | Start As |
|------|----------|----------|
| `--feature` | Full: interview → plan → code → review | `interviewer` |
| `--task` | Standard: plan → code → review | `planner` |
| `--quick` | Light: just implement | `code-writer` |
| `--review` | Code review only | `reviewer` |
| `--inbox` | Process dispatched work | (check inbox) |

Set your role:

```bash
dydo agent role <role> --task <task-name>
```

| Role | Can Edit |
|------|----------|
| `code-writer` | `src/**`, `tests/**` |
| `reviewer` | (read-only) |
| `docs-writer` | `dydo/**` (not agents/) |
| `interviewer` | Own workspace |
| `planner` | Own workspace + tasks/ |

---

## 6. Verify

```bash
dydo agent status
```

Shows your allowed paths. Guard blocks edits outside these.

---

## 7. Work

If blocked:
- Wrong role? `dydo agent role <correct-role>`
- Need other permissions? Dispatch to another agent

---

## 8. Complete

Review needed:
```bash
dydo dispatch --role reviewer --task <name> --brief "..."
```

Done:
```bash
dydo agent release
```

---

## Troubleshooting

| Error | Fix |
|-------|-----|
| "DYDO_HUMAN not set" | Human runs `dydo init claude` or `dydo init claude --join` |
| "Assigned to X" | Different human owns this. Try `dydo agent claim auto` |
| "Already claimed" | Another session has it. Try `dydo agent claim auto` |

---

*Full command reference: [how-to-use-docs.md](../guides/how-to-use-docs.md)*
