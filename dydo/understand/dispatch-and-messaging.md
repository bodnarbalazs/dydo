---
area: understand
type: concept
---

# Dispatch and Messaging

How agents communicate: dispatching work, inbox delivery, direct messaging, and waiting for responses.

---

## Dispatch

Dispatching reserves the target agent (status becomes `Dispatched`), writes a single assignment inbox item carrying the role and brief, and launches a new terminal for them. The launched agent claims, reads its assignment from the inbox, and sets its role. The dispatcher does not block — it returns as soon as the agent is launched.

```bash
dydo dispatch --auto-close --role <role> --task <task> --brief "Work description"
```

**Key options:**
- `--role` (required): Role for the target agent
- `--task` (required): Task name
- `--brief` / `--brief-file`: Work description
- `--to` / `--agent`: Explicit agent target (skips auto-selection)
- `--files`: File patterns for context (informational, not restrictive)
- `--escalate`: Mark as escalated
- `--auto-close`: Auto-close the target's terminal after release
- `--tab` / `--new-window`: Terminal launch mode
- `--no-launch`: Write to inbox without launching a terminal

### Launch Bridge

Dispatch reserves the agent via `ReserveAgent`, setting its status to `Dispatched`. The launched agent then claims that reservation. If the launch never produces a live session, the watchdog's stale-dispatch reclaim returns the agent to a re-dispatchable state, so a failed launch never strands an agent.

### Auto-Close

When `--auto-close` is set, a watchdog process polls the target agent's state every 10 seconds. When the agent's status returns to Free (after release), the watchdog closes the terminal.

### Terminal Spawning

Dispatch launches a new terminal with `claude "<agentName> --inbox"`:

- **Tab (default)**: Opens in the current window or the most-recently-used window
- **New window** (`--new-window`): Creates a new window with a fresh ID
- **Inherited window**: Child dispatches inherit the parent's `DYDO_WINDOW` environment variable, grouping related agents in the same window

Platform support: Windows Terminal (Windows 11), iTerm2 (macOS recommended).

### Double-Dispatch Protection

Before dispatching, the system scans all agents for one already working on the same task. If found, the dispatch is blocked (H23). The existing agent must release before re-dispatch.

### Auto-Transition for Reviewer Dispatch

When dispatching with `--role reviewer`, the task is automatically transitioned to `review-pending` status. The `--brief` content becomes the review summary. No separate `dydo task ready-for-review` call is needed.

---

## Inbox

Dispatched work arrives as markdown files in the target agent's `inbox/` directory.

### Inbox Items vs Messages

Both live in the same `inbox/` directory but differ in structure:

| | Dispatch Items | Messages |
|---|---|---|
| Created by | `dydo dispatch` | `dydo msg` |
| Contains | Role, task, brief, files | Subject, body |
| Purpose | Assign work | Communicate results/feedback |

### The --inbox Flag

When an agent is launched with `<agentName> --inbox`, the workflow directs them to check their inbox first (`dydo inbox show`), process each item, and set their role accordingly.

### Clearing and Archiving

```bash
dydo inbox clear --all     # Archive all items
dydo inbox clear --id <id> # Archive a single item
```

Items are **moved** (not deleted) to `archive/inbox/`. Unprocessed inbox items block release — the agent must clear its inbox before `dydo agent release` succeeds (the unread-inbox release gate).

---

## Messaging

Direct agent-to-agent messaging for reporting results, requesting clarification, or coordinating work.

```bash
dydo msg --to <agent> --body "Review passed. Ready to merge."
dydo msg --to <agent> --subject <task> --body "..."
```

### Subject and Task Context

The `--subject` field correlates messages to specific tasks. A task-channel `dydo wait --task <subject>` only wakes on messages carrying the matching subject.

### Restrictions

- **No self-messaging** (H21): Cannot send messages to yourself
- **No cross-human messaging** (H22): Cannot message agents assigned to a different human (unless `--force` is used)
- **Released target rejection**: `dydo msg` to a non-Working target hard-rejects. Use `--force` to write to the released inbox anyway, or redirect to an active agent

---

## Wait

Every claimed agent runs a general `dydo wait` in the background to receive messages in real time. Agents can also run task-channel waits to block until a response on a specific subject arrives.

```bash
dydo wait --task <task-name>   # Wait for message with matching subject
dydo wait                      # Wait for any unclaimed message
dydo wait --cancel             # Cancel all wait markers
```

### Polling Mechanism

Wait polls the inbox every 10 seconds for matching messages. When a message arrives, it's displayed and the wait command returns.

### Task-Specific Waits

`dydo wait --task feature-x` only wakes on messages with subject `feature-x`. This enables an orchestrator to dispatch multiple agents and wait for each independently.

### Channel Isolation

Each wait marker claims a subject, preventing other concurrent waits from consuming the same messages. This ensures that when an orchestrator runs multiple `dydo wait --task <name>` commands in background, each one receives only its intended response.

### Release Blocking

Active wait markers block agent release (H14). Cancel waits before releasing:

```bash
dydo wait --task <name> --cancel   # Cancel specific wait
dydo wait --cancel                 # Cancel all waits
```

---

## The Feedback Loop

The dispatch → work → message → release cycle forms a feedback loop that coordinates multi-agent work.

```
Orchestrator → Code-writer → Reviewer
                              │
                              └─ messages Orchestrator on completion
```

Agents report results by messaging the dispatcher (or origin) on the task's subject. The dispatcher's general wait surfaces those messages as they arrive.

### Orchestrator Coordination Pattern

A typical orchestrator workflow:

1. Dispatch multiple agents with `--auto-close`, each on a disjoint file slice
2. Keep the general wait running in the background to receive their messages
3. As responses arrive, assess results
4. Dispatch follow-up work or report back to the human

---

## Related

- [Agent Lifecycle](./agent-lifecycle.md)
- [Multi-Agent Workflows](./multi-agent-workflows.md)
- [CLI Commands Reference](../reference/dydo-commands.md)
