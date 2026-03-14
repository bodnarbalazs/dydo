---
area: understand
type: concept
---

# Multi-Agent Workflows

How multiple agents work in parallel: orchestration patterns, worktrees, and coordination.

<!-- PLACEHOLDER — to be filled during docs upgrade sprint -->
<!--
Topics to cover:
- The orchestrator pattern: one agent coordinates, others execute
- Parallel-safe work slicing: disjoint file sets
- Terminal management: tabs vs windows, grouping
- Worktree dispatch: isolated git worktrees for parallel code changes
- Agent tree: dydo agent tree (dispatch hierarchy visualization)
- Team support: multiple humans, agent pools
- Coordination patterns:
  - Dispatch-and-wait (orchestrator waits for results)
  - Fire-and-forget (dispatch --no-wait)
  - Chain dispatch (agent A dispatches B, B dispatches C)
- Common pitfalls in multi-agent setups
-->

---

## Related

- [Dispatch and Messaging](./dispatch-and-messaging.md)
- [Orchestrator Role](../reference/roles/orchestrator.md)
- [Agent Lifecycle](./agent-lifecycle.md)
