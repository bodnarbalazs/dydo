---
area: understand
type: concept
---

# Dispatch and Messaging

How agents communicate: dispatching work, inbox delivery, direct messaging, and waiting for responses.

<!-- PLACEHOLDER — to be filled during docs upgrade sprint -->
<!--
Topics to cover:
- Dispatch: what happens when an agent dispatches work
  - --wait vs --no-wait semantics
  - --auto-close behavior
  - Terminal spawning (tab vs window)
  - Worktree dispatch (--worktree)
  - Double-dispatch protection
  - Auto-transition for reviewer dispatch
- Inbox: how dispatched work arrives
  - Inbox items vs messages
  - --inbox flag on agent startup
  - inbox clear and archiving
- Messaging: dydo msg
  - Direct agent-to-agent messages
  - Subject/task context
  - Restrictions (no self-messaging, no cross-human)
- Wait: dydo wait
  - Polling mechanism
  - Task-specific waits
  - Wait markers and release blocking
  - Channel isolation
- The dispatch→message→release feedback loop
  - Who has to message back? (currently half-baked)
  - Orchestrator wait patterns
-->

---

## Related

- [Agent Lifecycle](./agent-lifecycle.md)
- [Multi-Agent Workflows](./multi-agent-workflows.md)
- [CLI Commands Reference](../reference/dydo-commands.md)
