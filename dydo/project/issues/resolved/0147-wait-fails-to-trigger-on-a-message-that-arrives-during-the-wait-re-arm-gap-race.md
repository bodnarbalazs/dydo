---
id: 147
area: backend
type: issue
severity: high
status: resolved
found-by: manual
date: 2026-05-01
resolved-date: 2026-05-01
---

# Wait fails to trigger on a message that arrives during the wait re-arm gap (race condition post-#0141 fix)

Open high-severity bug introduced by the #0141 fix: `WaitCommand.WaitGeneral` snapshots the inbox dir at registration time and treats every id in that snapshot as invisible to the wait. Messages that arrive in the multi-second W1-exit → W2-register window are filtered as "already known" and the wait sleeps forever on them. The proposed final fix is to unify "delivered" on `state.md.UnreadMessages` (the canonical set used by `inbox show` and the guard) instead of inventing a snapshot-based heuristic in the wait command.

## Description

`WaitCommand.WaitGeneral` (post-`65705e0`) snapshots the inbox dir at registration via `MessageFinder.GetInboxMessageIds(inboxPath)` and treats every id in that snapshot as invisible to the wait. Only files added *after* registration fire it.

The W1-exit → W2-register window — where W1 has fired on a message, the agent is processing it, and a new `dydo wait` has not yet registered — is multi-second in practice. Any message arriving in that window is on disk by the time W2 takes its snapshot, so W2 marks it as "already known" and never fires on it. The wait sleeps forever on a message it should have surfaced.

User report: "the wait sometimes does not trigger at all for an inbound message. The agent's general wait is registered and alive, but a message arrives and the agent never gets the soft-notice. Had to manually tell the agent to 'check your messages' even though their wait was open."

Quinn flagged this risk in the original #0141 plan but assumed a sub-millisecond window. The W1-exit → W2-register gap, in practice, is much wider because the agent does work (Read tool, processing) between firings.

### Root cause

The wait command and the rest of DyDo disagree on what "delivered" means. The wait command treats *file presence* as the delivery signal. The Read tool treats *id removed from `state.md.UnreadMessages`* as the delivery signal. Read clears one but not the other (`inbox clear` is a separate manual step), so the inbox dir at any moment mixes already-delivered files and just-arrived files.

The #0141 fix bridged that gap with a registration-time snapshot. That stopped the wait from popping on already-known messages but introduced a second gap: anything landing during the agent's re-arm window also gets filtered as "already on disk."

Each prior fix patched the symptom with a new heuristic. The recurring nature of these bugs is the symptom of the underlying disagreement, not of any single derivation being wrong.

## Reproduction

Manual:
1. Dispatch an agent and have them claim. Confirm `dydo wait` is alive (background).
2. From another agent, send two messages back-to-back: `dydo msg --to <agent> --body "first"` and immediately `dydo msg --to <agent> --body "second"`.
3. Observe: the first message fires the wait. The second does not — the agent has to either run `dydo inbox show` manually or be prompted by the human.

Automated (regression test in code-writer's plan):

```csharp
// msg1: file on disk + UnreadMessages depleted (post-Read state)
// msg2: file on disk + UnreadMessages={msg2} (just arrived)
// Run dydo wait. Today: snapshots {msg1, msg2}, sleeps. Bug.
[Fact]
public async Task WaitGeneral_FiresOnSecondMessage_ArrivedDuringRearmGap()
```

## Resolution

Unify the definition of "delivered" on `state.md.UnreadMessages`. It is already the canonical "not yet delivered" set used by `inbox show`, the guard's unread nudge, and `dydo agent list`. Only the wait command invented its own definition.

Concretely:

1. In `Commands/WaitCommand.cs.WaitGeneral`, drop the registration-time `initialUnread = MessageFinder.GetInboxMessageIds(inboxPath)` snapshot. Each poll reads `state.md.UnreadMessages` and uses it as an **inclusion set** (only fire on files whose id is in this set).
2. Add `HashSet<string>? includeIds` parameter to `Services/MessageFinder.FindMessage`. Empty set → no match. Non-null → only listed ids are considered.
3. Bundled side fix: remove the `targetState.Status == AgentStatus.Working` conditional in `Services/MessageService.cs:85-87`. Under the unified definition, "file written" must imply "id in `UnreadMessages`" regardless of target status — otherwise messages sent to released agents land on disk but never enter the canonical set.
4. Update existing tests that drop `*-msg-*.md` files via the test helper to also call `AddUnreadMessage` (or switch to `MessageService.DeliverInboxMessage`). The helper currently bypasses the canonical set, which only worked because the wait used the file-on-disk heuristic.

Why this is final, not another patch: every prior fix tried to derive "is this delivered?" from indirect signals (file presence, snapshot diff, registration time). Each derivation has edge cases. `UnreadMessages` is the **direct** signal, already maintained by the rest of the system. Using it removes the derivation entirely. No #0141 regression possible (already-read messages aren't in the set), no #0147 race possible (no snapshot, no re-arm gap).

Trade-off: there's a µs window in `MessageService.cs:83-87` between `File.WriteAllText` and `AddUnreadMessage`. If a wait polls in that window, the new file is on disk but its id isn't yet in `UnreadMessages` — wait skips it. On the next poll cycle (10s prod, 25ms tests), `AddUnreadMessage` has completed and the wait fires. No message is missed; latency is bounded by poll interval. Tightening this window (swap order, or atomic single-step) is out of scope.

Plan: `dydo/agents/Mia/plan-investigate-wait-race.md`
Notes: `dydo/agents/Mia/notes-investigate-wait-race.md`
