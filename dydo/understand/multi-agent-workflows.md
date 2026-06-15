---
area: understand
type: concept
---

# Multi-Agent Workflows

How multiple agents work in parallel: orchestration patterns, work slicing, and coordination. Dydo supports running several agents simultaneously, each in its own terminal, coordinated by dispatch and messaging commands.

---

## The Orchestrator Pattern

An orchestrator agent coordinates work by dispatching tasks to other agents. It doesn't implement features or write tests — it slices work into independent pieces, dispatches them, waits for results, and coordinates merges.

The orchestrator stays on the main branch. Each dispatched agent works on its assigned slice. When results come back, the orchestrator handles integration.

---

## Parallel-Safe Work Slicing

Parallel agents share one working tree, so the orchestrator keeps them from colliding by slicing work into **disjoint file sets**. Each agent owns a non-overlapping region of the codebase:

- Agent A: `Services/Auth/**`
- Agent B: `Services/Payment/**`

A shared working tree means overlapping edits cause cross-contamination — one agent's partial save can break another agent's build or tests. Disjoint slicing avoids this. When file overlap is unavoidable, use **sequential** dispatch (one agent at a time) instead of parallel.

If two slices touch the same files, they're one slice — or one goes first.

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

### Dispatch and Coordinate

The orchestrator dispatches work and keeps its general wait running to receive the dispatched agent's messages. The dispatch call itself does not block — the orchestrator continues coordinating other agents in parallel.

```bash
dydo dispatch --auto-close --role code-writer --task auth --brief "Implement auth"
# Returns immediately; the dispatched agent messages back when done
```

### Chain Dispatch

Agent A dispatches Agent B, who dispatches Agent C. This happens naturally in the review flow: a code-writer dispatches a reviewer on the same task. The reviewer reports its verdict back to the origin via `dydo msg`.

---

## Common Pitfalls

| Pitfall | Guard Rule | Fix |
|---------|-----------|-----|
| Two agents dispatched to same task | H23 (double-dispatch) | Release the first agent before re-dispatching |
| Agent releases with unprocessed inbox | H13 | Run `dydo inbox clear --all` |
| Agent releases with active waits | H14 | Cancel waits with `dydo wait --cancel` or wait for responses |
| Overlapping file edits | Not enforced | Orchestrator must slice work with disjoint file sets |

---

## Related

- [Dispatch and Messaging](./dispatch-and-messaging.md)
- [Agent Lifecycle](./agent-lifecycle.md)
