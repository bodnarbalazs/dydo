---
area: project
type: decision
status: accepted
date: 2026-03-25
participants: [balazs, Brian]
---

# 012 — Dispatch Queue: Deferred Terminal Launch

Named dispatch queues serialize work by deferring only the terminal launch — not the dispatch itself.

## Context

When multiple worktree agents finish around the same time, their reviewers each dispatch merge code-writers independently. Multiple mergers running `git merge` against the same branch simultaneously cause conflicts that wouldn't exist if serialized. The orchestrator doesn't control merge dispatch timing — reviewers dispatch mergers via a console hint in `ReviewCommand.cs`.

## Decision

### 1. Queues defer terminal launch only

When `--queue <name>` is used on `dydo dispatch`, all normal dispatch operations happen immediately: agent selection, inbox write, marker creation, worktree metadata setup. The only thing deferred is `LaunchTerminalIfNeeded()`.

**Why not defer the entire dispatch?** Deferring everything means agent selection happens at dequeue time, when the pool state may have changed (agents occupied, freed, or reassigned). Failing at dequeue time — minutes after the dispatch command returned successfully — is a worse UX than failing immediately. It also means `--wait` can't create markers upfront, requiring special handling.

With launch-only deferral, the dispatch either succeeds fully (agent reserved, inbox written, markers created, terminal pending) or fails fast. `--wait` works naturally — markers exist from dispatch time, the wait resolves whenever the agent eventually finishes.

The tradeoff is agent pool reservation: queued agents are occupied but idle. Acceptable because merge queues are short-lived (minutes) and shallow (2-3 items typical).

### 2. Persistent vs transient queues

Queues come in two flavors:

- **Persistent** — defined in `dydo.json`, survive even when empty. The `merge` queue is persistent by default.
- **Transient** — created on demand via `dydo queue create <name>`, auto-deleted when empty.

`--queue <name>` requires the queue to exist (persistent or previously created). This provides typo protection: `--queue marge` fails with "no queue 'marge', available: merge" rather than silently creating a new queue.

Transient queue cleanup is redundant: inline when the last item launches, watchdog sweep as fallback, and `dydo clean` for manual housekeeping.

## Consequences

- `dydo dispatch` gains `--queue <name>` flag; intercepts at `LaunchTerminalIfNeeded()`
- `dydo.json` gains a `queues` section (persistent queue definitions)
- `ReviewCommand.cs` merge hint updated to include `--queue merge`
- `dydo agent release` checks if the releasing agent is active in any queue and dequeues next
- Watchdog extended: stale active detection (dead PID), empty transient queue cleanup
- New CLI: `dydo queue create/show/cancel/clear`
- Agent state gains "queued" visibility via `.queued` marker

## Related

- [Decision 011 — Worktrees](./011-worktrees-as-default-for-parallel-work.md)
- [Decision 010 — Baton-Passing](./010-baton-passing-and-review-enforcement.md)
