---
area: guides
type: guide
---

# Agent General Wait

Every claimed agent runs a single always-active **general wait** in the background. It is how messages reach agents in real time and how the guard knows the agent is reachable.

This page covers when it is required, the lifecycle, and how to recover when something goes wrong.

---

## When It Is Required

A general wait must be active for **every role**, on every claimed agent, before any guarded tool call. The guard short-circuits and blocks tool calls when no general wait is registered.

Until [Decision 021](../project/decisions/021-unified-general-wait.md), this requirement was orchestrator-only. It now applies uniformly.

---

## Lifecycle

The wait sits in the standard agent flow:

```
claim  →  role  →  general wait  →  work  →  release
```

1. **Claim** — `dydo agent claim <name>` assigns identity.
2. **Role** — `dydo agent role <role> --task <task>` unlocks must-reads and writable paths.
3. **General wait** — start `dydo wait` in the background. This is the new step. Mode templates spell it out right after the role step.
4. **Work** — proceed with your task.
5. **Release** — `dydo agent release` tears down the wait. The background process is reaped via parent-PID liveness check (~10 s).

The wait blocks until a new unread message arrives that is not claimed by an active task wait, then exits. Rearm it after handling the message — the same polling pattern orchestrators have always used.

```bash
dydo wait    # run in background
```

---

## Dispatch and the General Wait

`dydo dispatch` reserves the target agent, writes a single assignment inbox item, and launches a terminal for that agent. The launched agent claims, reads its assignment, sets its role, and starts its own general wait.

The dispatcher does not block on dispatch. If you want a reply from a dispatched agent, coordinate via messaging — your general wait surfaces incoming messages when they land.

---

## Troubleshooting

### "MissingGeneralWait" when running a tool

The guard could not find an active general wait for your agent. Causes and fixes:

- **You forgot to start the wait.** Run `dydo wait` in the background and retry.
- **The background process died.** The marker self-heals on a dead PID; rerun `dydo wait` to register a new one.
- **The wait was started but the marker has not landed yet.** Rare. If it persists, cancel and restart: `dydo wait --cancel` then `dydo wait` again in the background.

### Release blocked by an unread inbox

You have unread inbox items. Process them first:

```bash
dydo inbox show
dydo inbox clear --all
```

Then `dydo agent release`.

### Stale wait marker after a crashed agent

The watchdog's parent-PID liveness check prunes orphaned markers. If a manual cleanup is needed, `dydo agent clean` removes wait markers belonging to dead processes.

---

## Related

- [Decision 021 — Unified General Wait for All Roles](../project/decisions/021-unified-general-wait.md) — why the general wait is universal.
- [Decision 005 — Fresh Agent Over Wait-for-Feedback](../project/decisions/005-fresh-agent-over-wait-for-feedback.md) — the unchanged guidance on review feedback loops.
- [Dispatch and Messaging](../understand/dispatch-and-messaging.md) — agent communication overview.
