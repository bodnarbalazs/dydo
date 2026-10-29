---
area: general
type: decision
status: accepted
date: 2026-10-28
---

# 057 - T3 Dispatch Follows the Child's Lifetime

Under T3, a dispatcher picks a child's channel by where it awaits the result, not by vendor. A
native foreground subagent only when the dispatcher waits for it within its own turn;
`delegate_task` for everything else that returns to the dispatcher. Ruled by Balazs on 2026-10-28
in the admiral's grilling on [DYD-397](https://linear.app/bodnar-balazs/issue/DYD-397), reversing
the dydo 3.5.0 "same vendor → native subagent" Dispatch rule.

## Context

dydo 3.5.0 (DYD-369, PR #166) had officers dispatch crew as children of their own turn: a native
subagent for the same vendor, T3 `delegate_task` for the other
([3.5.0 changelog](../changelog/2026/2026-10-23/dydo-3-5-0-dispatch-decisions-overnight-mode.md)).
That applied [DR 037](./037-cross-vendor-dispatch-same-vendor-default.md)'s same-vendor default
inside T3.

T3's sidebar now separates threads that are Working from the user's queue. Officers end their turn
while their children run, so a parked admiral or captain lands in the queue as if it needed the
user. On the live dydo and LC boards on 2026-10-27, two causes showed:

1. **Native subagents have no live status in T3.** A Claude-native child is mirrored as a child
   thread that always reads `idle`, with no run, so T3 can neither show it Working nor keep its
   parent there. An admiral whose captains were all native showed none of them running.
2. **No status rollup to the parent.** T3 keeps a parent Working only for its first delegated
   turn, even for `delegate_task` children; a grandchild still running does not hold it there.

Upstream t3code [PR #22890](https://github.com/pingdotgg/t3code/pull/22890), "parents stay working
while delegated children still run", closes the second cause for `delegate_task` children only and
explicitly excludes provider-native subagents. dydo gets that fix only by not using native
subagents for children that outlive the turn.

The 3.6.0 session retro ([DYD-405](https://linear.app/bodnar-balazs/issue/DYD-405), attachment
"DYD-405 full verifier reports") verified two related frictions:

- **Native dispatch fit** (`verify-coordination-native-dispatch-fit`): in one session on
  2026-10-26 and 27, four native ticket-captains (DYD-385, DYD-386, DYD-394, DYD-396) made six
  `delegate_task` calls for Codex review that T3 refused with `parent_not_active`: "Delegated tasks
  require an active run owned by this MCP provider session." Each captain relayed the dispatch up
  to its admiral. A native child lives only inside its dispatcher's turn and cannot delegate.
- **Premature handback** (`verify-coordination-premature-handback`): in two sessions on 2026-10-25
  to 27, native captains and crew ended a waiting turn while their crew, reviews or gates were
  still pending, and were forced to hand back unfinished work, for example "released DYD-373:
  forced handback mid-hop".

Codex fan-out met its own limit. On 2026-10-28, about 48 concurrent Codex readers wedged the T3
Codex app-server for 1h44m, and 16 hit OpenAI's turn-rate limit on both accounts (recorded on
DYD-397).

## Decision

1. **Under T3, the channel follows where the result is awaited.** A native foreground subagent
   only when the dispatcher waits for it within its own turn. `delegate_task` for every other child
   that returns to its dispatcher: captains, crew and reviewers. A new top-level T3 thread only for
   work the user takes over (HITL), or when the user asks for one. No native background subagents
   under T3. Without T3, the host's native subagents, as before.
2. **Child lifetime is named where officers dispatch.** A native child lives only inside its
   dispatcher's turn and cannot itself call `delegate_task`. The existing `parent_not_active`
   hand-up stays: a captain asks its officer, the chief of staff asks the user, and the admiral
   dispatches a captain's crew for it.
3. **Wait inside the turn.** A dispatcher waiting on a native child, or a child awaiting a result
   it owns (a reviewer, a suite), stays in its turn until the result arrives. Work that cannot
   finish inside the turn goes through `delegate_task`.
4. **Codex fan-out is capped in the brief.** Every brief that sends out Codex agents names a
   concurrency cap: start around 8, go toward 16 only while runs stay clean, and back off on
   `429 turn_rate_limit_exceeded` ("Codex is at capacity"). A capacity 429 means retry after a few
   minutes, never a blocker. No brief asks for maximum parallelism.
5. **Questions to the user go through the host.** An officer that needs the user while its work
   runs asks through the host's question tool, which in T3 lifts the thread into the user's queue.
   The Linear Question ticket stays the record.
6. **Crew ask their captain.** Crew hand their questions up to their captain and never ask the
   user directly.

The rule lives in the Delegation section of the
[working-tree contract](../../guides/working-tree-contract.md) and its Scaffold copy; each
officer's Dispatch line briefs it and points there.

### Not doing

- **No agent-side stopgap** while PR #22890 is pending: no parked footer, no self-snooze, no dydo
  keep-alive process to hold a parent in Working.
- **No upstream comment or issue**, neither on PR #22890 nor for native subagents reading `idle`.

## Consequences

- Captains, crew and reviewers under T3 run as `delegate_task` children, so PR #22890, once merged,
  keeps their dispatchers Working for as long as any descendant runs.
- A `delegate_task` captain dispatches its own Codex reviewer; the upward relay through the admiral
  is needed only when a native child still meets `parent_not_active`.
- Native subagents stay for short lookups the dispatcher waits on in its turn.
- Until PR #22890 lands, a parent whose grandchildren still run may still show in the queue. That
  is accepted rather than worked around.
- Codex throughput is bounded by the brief's cap rather than by the provider's capacity errors.

## Reverses

This reverses the 3.5.0 "same vendor → native subagent" Dispatch rule from DYD-369 in the officer
skills and the working-tree contract. It overrides [DR 037](./037-cross-vendor-dispatch-same-vendor-default.md)'s
same-vendor default only as applied to choosing a child's channel under T3; DR 037 itself stands
and is not edited.

## Related

- [Working-Tree Contract](../../guides/working-tree-contract.md#delegation) - The Delegation rule
- [DR 037 - Cross-Vendor Dispatch](./037-cross-vendor-dispatch-same-vendor-default.md) - The
  same-vendor default this narrows under T3
- [dydo 3.5.0](../changelog/2026/2026-10-23/dydo-3-5-0-dispatch-decisions-overnight-mode.md) - The
  Dispatch rule this reverses
