---
id: 111
area: backend
type: issue
severity: high
status: open
found-by: manual
date: 2026-04-26
---

# Stale active-queue marker not auto-cleared; dead-agent active slot blocks all subsequent --queue merge dispatches indefinitely

## Description

When a queued agent's process dies while holding the merge queue's "active" slot, `dydo queue show` correctly annotates the entry `[stale]` (so the queue subsystem already knows the PID is dead) but the queue advancement logic never recovers. Pending entries sit forever behind the dead active. New `dydo dispatch --queue merge` calls add to the pending list rather than starting.

The stale-active state is a single JSON file at `dydo/_system/.local/queues/merge/_active.json`:

```json
{
  "agent": "<dead-agent>",
  "task": "<merger-task>",
  "pid": <dead-pid>,
  "started": "<timestamp>"
}
```

`os.kill(pid, 0)` (or platform equivalent) returns `ProcessLookupError`. The annotation logic in `dydo queue show` is doing this check correctly. The advancement logic isn't. Recovery today requires either (a) a human guard-lift plus manual `rm` of the file, or (b) a privileged role that can edit `dydo/_system/.local/queues/**`.

This bug is the second half of a cascading failure pattern documented in a downstream post-mortem (see "Related context" — Adele/LC project, 2026-04-26). It compounds with #0109 (reviewer chain-origin block on merge dispatch) to stall multi-agent worktree work indefinitely:

- Reviewer can't dispatch merger (#0109) → orchestrator dispatches manually → that merger dies in any way (terminal close, OS context, dydo update propagation killing in-flight processes — all observed in the same downstream session) → dead merger holds active slot (#0111) → all subsequent mergers stack behind it → multiple reviewers hold `.needs-merge` markers indefinitely → orchestrator must request guard-lift and manually delete `_active.json` to recover.

In a single observed downstream session, this cascade fired three times within hours, with the third occurrence stalling four mergers and four reviewers simultaneously.

## Reproduction

Three observed occurrences in a single 2026-04-26 session at the LC project:

1. **~13:00 UTC:** Quinn held active for `frontend-slice-15-frontend-hooks-cluster-merge` with PID dead. Cleared by manual `rm dydo/_system/.local/queues/merge/_active.json`.
2. **~14:30 UTC:** Same shape, different agent. Same cleanup.
3. **Reported in post-mortem:** Rose held active for `frontend-refactor-RA3-ActionDetailPanelRenderer-merge` PID=49032 (dead). Four mergers stuck pending behind her (Uma A4, Paul B-3, Xavier A1, Yara getUiColorName-fix). Four reviewers (Brian, Tara, Iris, Unknown) holding `.needs-merge` markers and unable to release.

Synthetic repro:

1. `dydo dispatch --queue merge --role code-writer --task <merger-task-A> --brief "..."` — agent claims active slot.
2. Kill the agent's terminal/process out-of-band (the active-slot writer process — not via `dydo agent release`).
3. `dydo queue show` — shows `Active: <agent> ... [stale]`.
4. `dydo dispatch --queue merge --role code-writer --task <merger-task-B> --brief "..."` — claim added to pending; never starts.
5. Wait arbitrarily long. The queue does not advance.

## Likely root cause

The PID staleness check exists in the read-side code path (`dydo queue show` annotates `[stale]`). The advancement code path (whatever fires when a new `--queue merge` dispatch is queued, or what should fire when reading queue state) does not consult the staleness signal — it treats the active slot as occupied unconditionally.

Likely fix is in whichever service owns `_active.json` lifecycle — probably a `QueueService` or similar — adding a "is active stale → clear and promote first pending" branch on every read of queue state. This is a single-file change with bounded blast radius.

## Suggested fix

Two reasonable approaches:

1. **Auto-recover (preferred default).** When any queue read detects an `[stale]` active entry, atomically clear the active slot and emit a `[recovered]` log line. Next pending entry promotes to active automatically. Document the auto-recovery in `dydo/reference/dydo-commands.md` so agents know what `[recovered]` means when they see it.

2. **Manual recover with first-class command.** Refuse to start new pending entries until the stale marker is manually cleared, but expose `dydo queue clear-stale <queue-name>` as an agent-callable command (not gated behind guard-lift). Today the only path is raw `rm`, which requires guard-lift for non-system roles.

Option (1) is the right default — a dead agent should never block live work indefinitely. The PID check is already in place, so the data is there; it just needs to be wired into the advancement logic.

Useful regression test: kill a queued worker mid-merge (or simulate via PID file edit), then run `dydo queue show` followed by a new `dydo dispatch --queue merge` — assert the queue advances within a small bounded number of operations (or immediately, if option (1)).

## Impact

- **Cascading agent stall.** A single dead merger blocks an arbitrary number of downstream reviewers and mergers. In the post-mortem's third occurrence, four mergers + four reviewers were stuck simultaneously.
- **Manual recovery requires guard-lift.** The only fix today is `rm` on a guarded path, which non-system roles can't perform without a human-issued guard lift. Each occurrence costs 10–30 minutes of orchestrator attention.
- **Recurrence frequency is high.** Three occurrences in a single observed session — not an edge case.
- **Compounds with #0109.** Until #0109 is also fixed, the cascading-stall pattern's source (orchestrator-driven manual merge dispatches) is going to keep triggering this whenever any one of those manual mergers dies for any reason.

## Related context

- `dydo/_system/.local/queues/merge/_active.json` — the marker file at the centre of this.
- `Services/QueueService.cs` (or whatever owns queue lifecycle — confirm) — likely fix site.
- `Commands/QueueCommand.cs` — for any new `clear-stale` subcommand under option (2).
- `dydo/agents/Brian/incoming-post-mortem-LC-2026-04-26.md` — full downstream post-mortem detailing all three observed occurrences and the cascade interaction with #0109.
- Issue #0109 — the upstream half of the cascade (reviewer cannot dispatch merger when chain originates from test-writer). Both should ideally land together.
- Issue #0107 — the worktree-merge self-merge bug; not directly causal for #0111 but the post-mortem documents that crash-prone manual recovery flows around #0107 are one of the contributors that produces the dead-merger that ends up wedging the queue.

## Resolution

(Filled when resolved)
