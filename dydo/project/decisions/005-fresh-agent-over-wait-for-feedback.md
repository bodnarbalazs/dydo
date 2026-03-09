---
type: decision
status: accepted
date: 2026-03-09
area: project
---

# 005 — Fresh Agent Over Wait-for-Feedback

Use fresh agent sessions for review feedback loops; reserve `dispatch --wait` for oversight roles (orchestrators, inquisitors).

## Problem

When a code-writer dispatches to a reviewer and the reviewer finds issues, the feedback needs to reach an agent who can fix them. Two approaches exist:

1. **Wait model**: The code-writer dispatches with `--wait`, stays idle until the reviewer responds, then fixes issues in-place with full original context.
2. **Fresh agent model**: The code-writer dispatches with `--no-wait` and releases. If the reviewer finds issues, it dispatches to a *new* code-writer session with the review feedback.

## Context

The wait model was the original design intention — the same agent preserves context and can fix issues efficiently. However, the messaging infrastructure (`dydo wait`, `dydo msg`) didn't exist at the time, so in practice dispatching "back" to the code-writer always created a fresh session anyway. This accidental design proved to work well.

With messaging now available (v1.2), the wait model is technically possible. The question is whether it's worth the cost.

## Decision

**Use fresh agents for the standard feedback loop.** `dispatch --wait` is reserved for oversight roles (orchestrators, inquisitors) that need responses to complete their primary function.

### Rationale

| Factor | Wait model | Fresh agent model |
|--------|-----------|-------------------|
| Resource usage | Idle tab consuming memory | No idle resources |
| Context quality | High initially, degrades if wait is long | Fresh — reads review + code, no stale context |
| Blast radius | Stuck waiter = wasted tab forever | No risk of orphaned waiters |
| Complexity | Needs timeout/cleanup for stuck waits | Simpler lifecycle: dispatch and release |
| Code fix quality | May carry original assumptions that caused the bug | Fresh eyes, guided by review feedback |

The "fresh eyes" effect is underrated — a new agent reading the review feedback approaches the fix without the assumptions that led to the original bug.

### Exception: Oversight Roles

Orchestrators and inquisitors *must* wait. Their job is to coordinate or investigate — they can't produce their output without sub-agent responses. This is a fundamental part of their role, not a convenience.

## Implications

- Standard workflow: code-writer → `dispatch --no-wait` → reviewer → (if issues) `dispatch --no-wait` → new code-writer
- The guard can enforce: only `orchestrator` and `inquisitor` roles may use `dispatch --wait`
- Simplifies the agent lifecycle — most agents have a clean claim → work → release cycle with no idle states
