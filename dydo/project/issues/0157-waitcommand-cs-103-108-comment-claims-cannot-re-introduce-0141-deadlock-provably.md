---
id: 157
area: backend
type: issue
severity: low
status: open
found-by: inquisition
date: 2026-05-01
---

# WaitCommand.cs:103-108 comment claims 'cannot re-introduce #0141 deadlock' — provably false under Decision 021

## Description

## Description

The defending comment in `Commands/WaitCommand.cs:103-108`:

```
// Wait fires on inbox-files ∩ state.md.UnreadMessages, re-read each poll. The
// canonical "not yet delivered" set: writer adds via MessageService.DeliverInboxMessage
// → AddUnreadMessage; Read removes via GuardCommand.TrackReadCompletion → MarkMessageRead.
// No registration-time snapshot — eliminates the W1-exit → W2-register race (#0147)
// and cannot re-introduce the #0141 deadlock because already-read ids are no
// longer in the set. (#0141 / #0147)
```

The "cannot re-introduce the #0141 deadlock" claim is provably false in any state where unread ids are present and the agent is unable to drain — i.e., the deadlock #0149 reports.

The "already-read ids are no longer in the set" premise quietly assumes the agent **can** read between fires. After Decision 021 generalised the must-keep-general-wait gate to all roles, that premise no longer holds: `Read` is blocked by `GuardCommand.MissingGeneralWait` (`GuardCommand.cs:387` → `CheckPendingState`) the moment the wait has exited, so unread ids cannot be cleared between wait fires when the wait keeps re-firing on the stacked set.

The comment encodes the design intent post-#0147 but does not survive the addition of Decision 021's "every tool call requires a live general wait" rule.

## Suggested fix

Two paths, depending on what happens with #0149:

- If #0149 is fixed by adding a `WaitMarker.Since`-based registration filter (the leading suggestion in the inquisition report), rewrite the comment to reflect the new invariant: the wait fires only on messages whose `Received` >= the wait's `Since`, which closes both #0141 and #0147 without depending on inter-fire `Read`.
- If #0149 is fixed differently, the comment should still be updated to honestly state the current trade-off — the canonical-unread design depends on the agent being able to issue a `Read` tool call between wait fires, which is no longer guaranteed under Decision 021.

Either way, do not leave the current claim in place: it gives a future maintainer false confidence that the canonical-set design is sufficient.

## Related

- `Commands/WaitCommand.cs:103-108` — the comment.
- Issue [#0149](./0149-wait-rearm-gap-deadlocks-agent-when-3-plus-messages-stack-faster-than-agent-can-process.md) — the lived deadlock the comment denies.
- Decision [021](../decisions/021-unified-general-wait.md) — universal must-keep-general-wait gate that broke the comment's invariant.
- Inquisition: [wait-rearm-flood-deadlock](../inquisitions/wait-rearm-flood-deadlock.md) (Finding #5) — drift discovered during #0149 investigation.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)