---
area: understand
type: concept
---

# Dispatch and Messaging

How agents communicate: dispatching work, inbox delivery, direct messaging, and waiting for responses.

---

## Dispatch

Dispatching creates an inbox item in the target agent's workspace and optionally launches a new terminal for them.

```bash
dydo dispatch --no-wait --auto-close --role <role> --task <task> --brief "Work description"
```

**Key options:**
- `--role` (required): Role for the target agent
- `--task` (required): Task name
- `--brief` / `--brief-file`: Work description
- `--to`: Explicit agent target (skips auto-selection)
- `--files`: File patterns for context (informational, not restrictive)
- `--escalate`: Mark as escalated
- `--auto-close`: Auto-close the target's terminal after release
- `--tab` / `--new-window`: Terminal launch mode
- `--no-launch`: Write to inbox without launching a terminal
- `--worktree`: Run the target in an isolated git worktree

### --wait vs --no-wait

One of these is required on every dispatch.

**`--wait`**: The dispatcher creates a wait marker and blocks (via `dydo wait`) until the target sends a message back. Restricted to **oversight roles only** (orchestrator, inquisitor, judge).

**`--no-wait`**: The dispatcher continues immediately. Standard for non-oversight roles. The target works independently and reports back via messaging if needed.

### Auto-Close

When `--auto-close` is set, a watchdog process polls the target agent's state every 10 seconds. When the agent's status returns to Free (after release), the watchdog closes the terminal.

### Terminal Spawning

Dispatch launches a new terminal with `claude "<agentName> --inbox"`:

- **Tab (default)**: Opens in the current window or the most-recently-used window
- **New window** (`--new-window`): Creates a new window with a fresh ID
- **Inherited window**: Child dispatches inherit the parent's `DYDO_WINDOW` environment variable, grouping related agents in the same window

Platform support: Windows Terminal (Windows 11), iTerm2 (macOS recommended).

### Worktree Dispatch

`--worktree` creates an isolated git worktree so the target agent works on a separate branch:

1. A new branch `worktree/{id}` is created
2. The worktree directory is set up at `dydo/_system/.local/worktrees/{id}`
3. Four junctions/symlinks share state: `dydo/agents/`, `dydo/_system/roles/`, `dydo/project/issues/`, `dydo/project/inquisitions/`
4. Markers are stored in the agent's workspace: `.worktree` (ID), `.worktree-path` (directory), `.worktree-base` (target branch), `.worktree-root` (main project root)

Child dispatches from within a worktree have three paths:
- **Nested child** (`--worktree`): Creates a new child worktree with a hierarchical ID (e.g., `parent/child`)
- **Inheritance** (default): Child inherits the parent's worktree markers and works in the same worktree
- **Merge dispatch**: When the agent has a `.needs-merge` marker, the child launches in the main repo to perform the merge, receiving `.merge-source` and `.worktree-hold` markers

Worktrees are cleaned up via `dydo worktree cleanup` on release, with `dydo worktree prune` handling orphans.

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

Items are **moved** (not deleted) to `archive/inbox/`. If an item has `reply_required: true`, clearing it creates a reply-pending marker — the agent cannot release until a message is sent back upstream.

---

## Messaging

Direct agent-to-agent messaging for reporting results, requesting clarification, or coordinating work.

```bash
dydo msg --to <agent> --body "Review passed. Ready to merge."
dydo msg --to <agent> --subject <task> --body "..."
```

### Subject and Task Context

The `--subject` field correlates messages to specific tasks. When a message is sent with a subject matching a reply-pending marker, the obligation is automatically fulfilled and the marker is removed.

### Restrictions

- **No self-messaging** (H21): Cannot send messages to yourself
- **No cross-human messaging** (H22): Cannot message agents assigned to a different human (unless `--force` is used)
- **Inactive target warning**: A soft-block warns if the target agent is not currently in Working status

---

## Wait

Orchestrators and other oversight roles use `dydo wait` to block until a response arrives.

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

The dispatch → message → release cycle forms a feedback loop that coordinates multi-agent work.

### Baton-Passing

When an agent dispatches another agent on the **same task**, the reply obligation passes to the new agent. The sender's reply-pending marker is cleared, and the receiver inherits `reply_required: true`. This chains naturally:

```
Orchestrator (--wait) → Code-writer → Reviewer
                                       │
                                       └─ messages Orchestrator on completion
```

Only the last agent in the chain needs to send a message back. Each intermediate agent's obligation is cleared when it dispatches the next one on the same task.

### Review Enforcement

Dispatched code-writers (those with a `DispatchedBy` origin) cannot release without first dispatching a reviewer for the same task (H25). This ensures every orchestrated code change goes through review. Code-writers started directly by a human are exempt from this rule.

### Orchestrator Wait Pattern

A typical orchestrator workflow:

1. Dispatch multiple agents with `--wait --auto-close`
2. Run `dydo wait --task <name>` in background for each
3. As responses arrive, assess results
4. Dispatch follow-up work or report back to the human

---

## Related

- [Agent Lifecycle](./agent-lifecycle.md)
- [Multi-Agent Workflows](./multi-agent-workflows.md)
- [CLI Commands Reference](../reference/dydo-commands.md)
