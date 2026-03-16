---
area: project
type: decision
status: accepted
date: 2026-03-16
---

# 010 — Baton-Passing and Review Enforcement

When an agent dispatches on the same task, its reply obligation passes to the dispatched agent. Dispatched code-writers cannot release without dispatching a reviewer.

## Context

After the v1.2/v1.3 feature sprint, the messaging system (`dydo msg`), reply-pending markers, and dispatch flow exist but the rules around who owes a reply to whom are unclear. The templates contain incorrect examples (non-oversight roles using `--wait`), and there's no enforcement around code-writers skipping review when dispatched as part of an orchestrated workflow.

The core question: in a chain like orchestrator → code-writer → reviewer, who is responsible for reporting back to the orchestrator?

## Decision

### 1. Baton-passing rule

**When you dispatch another agent on the same task, you pass the baton.** Your reply obligation to the upstream agent is fulfilled by the act of dispatching. The next agent inherits the obligation.

Implementation:
- When an agent dispatches on the same task it was dispatched for, its reply-pending marker is cleared
- The new agent's inbox item carries `reply_required: true` (inherited from the chain, not from `--wait`)
- The last agent in the chain — the one who doesn't dispatch anyone else — sends the message back

Typical chain:
```
Orchestrator (--wait) → Code-writer → Reviewer
                         ↓ dispatches    ↓ PASS: messages orchestrator, releases
                         reviewer,       ↓ FAIL: dispatches code-writer (baton passes back)
                         releases
```

The `reply_required` field on inbox items is **decoupled from `--wait`**. It's inherited through the dispatch chain whenever the original dispatch used `--wait`. Non-oversight roles always use `--no-wait` but can still carry `reply_required: true`.

### 2. Review enforcement for dispatched code-writers

**When a code-writer is dispatched (has an origin / was part of an orchestrated workflow), it cannot release without dispatching a reviewer for the same task.** This is a hard rule (H-tier guardrail).

When a code-writer works directly with the human (no dispatch origin), no review enforcement applies. The human decides if review is needed.

Detection: if the agent's inbox item had an `origin` field (indicating it's part of a chain), the hard rule applies. Direct sessions (human starts the agent) have no origin.

### 3. `--auto-close` in template examples

Template dispatch examples should include `--auto-close` since the primary audience for these templates is dispatched agents (who are always in their own terminal). When agents dispatch further down the chain, `--auto-close` keeps terminal clutter down. Humans who dispatch directly choose whether to include it — templates don't need to account for that case.

## Consequences

- **Code change**: Reply-pending marker cleared on same-task dispatch. `reply_required` inherited through chain independent of `--wait`.
- **Code change**: New hard rule — dispatched code-writers blocked from releasing without dispatching reviewer.
- **Template fixes**: All non-oversight role templates use `--no-wait`. Examples show `--auto-close` only in orchestrated patterns.
- **Guardrails doc**: New entries for the baton-passing clearance and review enforcement rule.

## Related

- [Decision 003 — Agent Messaging](./003-agent-messaging.md)
- [Decision 005 — Fresh Agent Over Wait-for-Feedback](./005-fresh-agent-over-wait-for-feedback.md)
- [Guardrails Reference](../../reference/guardrails.md)
