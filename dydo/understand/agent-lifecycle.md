---
area: understand
type: concept
---

# Agent Lifecycle

How an agent goes from unclaimed to working to released. The full claim → role → work → dispatch/release cycle.

---

## Claiming

An agent starts as **Free** — unclaimed, no session, no role.

**Named claim** (`dydo agent claim Brian`): Claims a specific agent by name. Single-letter abbreviations work (`dydo agent claim B`).

**Auto claim** (`dydo agent claim auto`): Selects the first free agent assigned to the current human (determined by `DYDO_HUMAN` environment variable). If dispatched agents have pending inbox items, a nudge warns you to claim by name instead.

**Session binding**: Each claim ties the agent to the caller's `session_id` (provided by the PreToolUse hook). A `.session` file records the agent name, session ID, and claim timestamp. A `.claim.lock` file prevents race conditions during concurrent claims. Claiming the same agent in the same session is idempotent; claiming an agent already bound to a different session is blocked.

---

## Role Assignment

After claiming, the agent has identity but no role. The guard blocks writes, searches, and most reads until a role is set.

```bash
dydo agent role <role> --task <task-name>
```

Setting a role triggers several things:

1. **Constraint validation** — Role constraints are checked (e.g., no self-review, orchestrator graduation). If any fail, the role change is blocked.
2. **Must-reads computation** — The mode file is scanned for links to files with `must-read: true` in frontmatter. These populate `UnreadMustReads`, blocking writes until all are read.
3. **Permission setup** — `WritablePaths` and `ReadOnlyPaths` are loaded from the role's `.role.json` definition. Path tokens like `{self}`, `{source}`, and `{tests}` are expanded.
4. **Role history tracking** — The role is appended to `TaskRoleHistory[task]`, which persists across sessions and is used for constraint checks like self-review prevention.

---

## Staged Onboarding

The guard enforces a progressive unlock through three stages:

| Stage | State | Reads Allowed | Writes Allowed | Search Tools |
|-------|-------|---------------|----------------|--------------|
| 0 | No identity | Bootstrap files only | None | None |
| 1 | Claimed, no role | Bootstrap + own mode files | None | None |
| 2 | Claimed + role set | All files | Files in `WritablePaths` (after must-reads) | All |

**Bootstrap files**: `dydo/index.md`, root-level project files, and `dydo/agents/*/workflow.md`.

At Stage 2, writes are still gated by must-read enforcement — the agent must read all `must-read: true` files linked from the mode file before any writes are allowed.

---

## Working

Once at Stage 2, the agent can:

- **Read** any file (role provides read-only paths as guidance, not restriction)
- **Write/Edit/Delete** files matching `WritablePaths` from the role definition
- **Search** using Glob, Grep, and Agent tools
- **Dispatch** work to other agents (subject to role constraints and double-dispatch protection)
- **Message** other agents (`dydo msg`)
- **Run bash commands** (subject to safety analysis: dangerous patterns blocked, file operations checked against role permissions)

Certain operations are gated by **soft-blocks** — one-time stops that pass on retry: unread inbox messages, pending wait registration, role mismatch warnings.

---

## Dispatch

An agent can hand off work by dispatching another agent:

```bash
dydo dispatch --no-wait --auto-close --role <role> --task <task> --brief "..."
```

This creates an inbox item in the target agent's workspace and optionally launches a new terminal. The `--wait` flag (restricted to oversight roles) creates a wait marker so the dispatcher blocks until a response arrives.

When dispatching on the **same task** the agent was dispatched for, the reply obligation passes to the new agent (baton-passing). See [Dispatch and Messaging](./dispatch-and-messaging.md) for full details.

---

## Release

When work is complete, the agent releases:

```bash
dydo agent release
```

**Pre-release validation** blocks release if:
- Unprocessed inbox items exist
- Active wait markers exist
- A reply obligation is pending (message not sent back to upstream agent)
- A worktree merge is pending
- A dispatched code-writer hasn't dispatched a reviewer

**Cleanup on release:**
1. State reset — Role, task, permissions, must-reads, and messages cleared. Status set to Free.
2. Session teardown — `.session` file deleted, session hints cleared.
3. Workspace archive — Non-system files moved to `archive/{timestamp}/`. Archive pruned to 30 entries max.
4. Marker cleanup — Wait markers, reply-pending markers, review-dispatched markers, and nudge markers all deleted.

---

## Re-Entry

When a released agent is claimed again in a new session:

- The prior workspace is archived (fresh start)
- Mode files are regenerated from templates
- A new `.session` file is written with the new session ID
- **TaskRoleHistory is preserved** — role constraints carry across lifetimes (e.g., an agent that was code-writer on a task can never review that same task, even in a later session)

---

## State Diagram

```
FREE (unclaimed)
  │
  │  dydo agent claim
  ▼
CLAIMED (identity only, no role)
  │
  │  dydo agent role <role> --task <task>
  ▼
WORKING (identity + role)
  │
  ├──── dydo dispatch ────► target agent DISPATCHED
  │                              │
  │                              │  dydo agent claim (target)
  │                              ▼
  │                         target WORKING (with DispatchedBy set)
  │
  │  dydo agent release
  ▼
FREE (again — TaskRoleHistory preserved)
```

---

## Related

- [Guard System](./guard-system.md)
- [Dispatch and Messaging](./dispatch-and-messaging.md)
- [Roles and Permissions](./roles-and-permissions.md)
