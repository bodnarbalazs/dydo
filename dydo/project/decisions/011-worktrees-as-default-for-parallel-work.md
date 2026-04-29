---
area: project
type: decision
status: superseded
date: 2026-03-16
---

# 011 — Worktrees as Default for Parallel Development

When an orchestrator dispatches multiple code-writers in parallel, each should run in its own git worktree.

> **Superseded by [020 — Worktree Usage Policy: Power Option, Not Default](./020-worktree-usage-policy-power-option.md)** for the *when-to-use* question. The mechanics below (sequential merges, conflict escalation, `git stash` block) remain in force.

## Context

In practice, parallel agents on the same working tree cause cascading problems:

1. **Build locking.** .NET (and most compiled languages) lock output DLLs during build/test. Agent B can't compile while Agent A is running tests. Agent B tries to kill the locking process, disrupting Agent A.

2. **Cross-contamination.** Agent A saves a file mid-edit. Agent B runs tests. The compiler picks up Agent A's incomplete code. Tests fail. Agent B wastes time debugging "unrelated" failures.

3. **Git state conflicts.** Agents try `git stash` to isolate their changes. But stashes are a global stack — Agent A pops Agent B's stash. `git checkout` in parallel is equally dangerous.

All three problems share one root cause: **shared working tree.** Worktrees eliminate this entirely — each agent gets its own directory, build output, and git index.

## Decision

### Orchestrators must use `--worktree` for parallel code-writer dispatches

When an orchestrator dispatches multiple agents that will edit source code, each dispatch should include `--worktree`. The orchestrator stays on the main branch and coordinates merges.

This applies to:
- Code-writers dispatched in parallel
- Test-writers dispatched alongside code-writers

This does NOT apply to:
- Sequential dispatches (one agent at a time — no contention)
- Non-code roles (docs-writers, planners, co-thinkers — they don't build/test)
- Direct human sessions (human manages one agent at a time)

### Block `git stash` in parallel environments

Add `git stash` to the bash guard's dangerous command patterns. In a multi-agent environment, stash is never safe — it corrupts other agents' state. Agents should commit their work instead.

### Merge coordination

Each worktree task ends with a merge. When multiple tasks finish:
- Merges happen sequentially (orchestrator coordinates ordering)
- Each merge checks for conflicts before committing
- Conflicted merges escalate to the human — agents do not auto-resolve

### Orchestrator template guidance

The orchestrator template should explicitly instruct:
- Use `--worktree` when dispatching parallel code work
- Coordinate merge ordering when results come back
- Expect each task to take slightly longer (merge step) but run truly in parallel

## Consequences

- **Template change**: Orchestrator template updated with `--worktree` as the parallel dispatch pattern
- **Guard change**: `git stash` added to blocked command patterns
- **Workflow change**: Every parallel code task includes a merge phase
- **Track D's scope expanded**: Worktree workflow must be robust — it's no longer opt-in

## Related

- [Decision 010 — Baton-Passing and Review Enforcement](./010-baton-passing-and-review-enforcement.md)
