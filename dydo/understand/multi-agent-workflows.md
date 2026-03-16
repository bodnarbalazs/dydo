---
area: understand
type: concept
---

# Multi-Agent Workflows

How multiple agents work in parallel: orchestration patterns, worktrees, and coordination. Dydo supports running several agents simultaneously, each in its own terminal, coordinated by dispatch and messaging commands.

---

## The Orchestrator Pattern

An orchestrator agent coordinates work by dispatching tasks to other agents. It doesn't implement features or write tests — it slices work into independent pieces, dispatches them, waits for results, and coordinates merges.

The orchestrator stays on the main branch. Each dispatched agent works on its assigned slice. When results come back, the orchestrator handles integration.

---

## Parallel-Safe Work Slicing

For parallel agents to work safely, their file sets should be disjoint. An orchestrator slices work so agents don't touch the same files:

- Agent A: `Services/Auth/**`
- Agent B: `Services/Payment/**`

When file overlap is unavoidable, use sequential dispatch instead of parallel.

---

## Worktrees: The Default for Parallel Code Work

When dispatching multiple code-writers in parallel, each gets its own git worktree — an isolated copy of the repository with its own working directory, build output, and git index. See [Decision 011](../project/decisions/011-worktrees-as-default-for-parallel-work.md) for the rationale.

```bash
dydo dispatch --worktree --wait --auto-close --role code-writer --task auth-login --brief "Implement OAuth"
```

### Why Worktrees

A shared working tree causes cascading problems in parallel scenarios:

- **Build locking** — compiled languages lock output files during build/test, blocking other agents
- **Cross-contamination** — one agent's partial save causes another agent's tests to fail
- **Git state conflicts** — `git stash` is a global stack; parallel stash/pop corrupts state

Worktrees eliminate all three. Each agent gets full isolation.

### When Worktrees Apply

| Scenario | Use Worktree? |
|----------|---------------|
| Parallel code-writers | Yes |
| Test-writers alongside code-writers | Yes |
| Sequential dispatches (one at a time) | No |
| Non-code roles (docs-writers, planners) | No |

### The Merge Flow

Each worktree task ends with a merge back to the main branch:

1. Merges happen sequentially, coordinated by the orchestrator
2. Each merge checks for conflicts before committing
3. Conflicted merges escalate to the human — agents don't auto-resolve

---

## Terminal Management

Dispatched agents run in their own terminal sessions. Control where they open:

```bash
dydo dispatch --tab ...         # New tab in current terminal
dydo dispatch --new-window ...  # New window
```

The default (tab vs window) is configurable. Use `--auto-close` to clean up the terminal after the agent releases.

---

## Agent Tree

Visualize the dispatch hierarchy of active agents:

```bash
dydo agent tree
```

This shows who dispatched whom, forming a tree from the orchestrator down to leaf agents.

---

## Team Support

Each team member gets their own pool of agents. Agents are bound to their human — no cross-human messaging or agent sharing. Join an existing project:

```bash
dydo init claude --join
```

---

## Coordination Patterns

### Dispatch-and-Wait

The orchestrator dispatches work and waits for a response. The wait blocks release until a message arrives or is cancelled.

```bash
dydo dispatch --wait --auto-close --role code-writer --task auth --brief "Implement auth"
# Agent blocks here until the dispatched agent messages back
```

### Fire-and-Forget

Dispatch without waiting. Useful when the orchestrator doesn't need results.

```bash
dydo dispatch --no-wait --role docs-writer --task update-docs --brief "Update API docs"
```

### Chain Dispatch

Agent A dispatches Agent B, who dispatches Agent C. This happens naturally in the review flow: a code-writer dispatches a reviewer on the same task. The reviewer inherits the obligation to report back to whoever originally dispatched the code-writer (baton-passing).

---

## Common Pitfalls

| Pitfall | Guard Rule | Fix |
|---------|-----------|-----|
| Two agents dispatched to same task | H23 (double-dispatch) | Release the first agent before re-dispatching |
| Code-writer tries to release without review | H25 (review enforcement) | Dispatch a reviewer on the same task first |
| Agent releases with unprocessed inbox | H13 | Run `dydo inbox clear --all` |
| Orchestrator releases with active waits | H14 | Cancel waits with `dydo wait --cancel` or wait for responses |
| Agent forgets to message upstream | H15 (reply pending) | Send `dydo msg --to <origin> --subject <task>` |
| `git stash` in multi-agent setup | H26 | Commit instead, or work inside a worktree |
| Overlapping file edits | Not enforced | Orchestrator must slice work with disjoint file sets |
| `--worktree` with `--no-launch` | Rejected | Worktree lifecycle depends on terminal; use `--tab` or `--new-window` |

---

## Related

- [Dispatch and Messaging](./dispatch-and-messaging.md)
- [Agent Lifecycle](./agent-lifecycle.md)
- [Decision 011 — Worktrees](../project/decisions/011-worktrees-as-default-for-parallel-work.md)
