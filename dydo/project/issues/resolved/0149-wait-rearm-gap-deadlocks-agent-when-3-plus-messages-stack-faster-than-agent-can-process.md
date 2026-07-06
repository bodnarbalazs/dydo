---
id: 149
area: backend
type: issue
severity: high
status: resolved
found-by: manual
date: 2026-05-01
resolved-date: 2026-07-04
---

# Wait re-arm gap deadlocks agent when 3+ messages stack faster than agent can process

When an agent receives multiple unread messages in rapid succession, the background `dydo wait` fires and exits on every unread arrival, but the guard's "must keep general wait active" rule has no breathing room between fire-and-rearm. The agent gets stuck unable to run any tool (`dydo inbox show`, Read, Write, etc.) because every fresh wait it starts exits within milliseconds on the next stacked unread, and the next tool call's PreToolUse guard check sees no active wait. `dydo wait --cancel` is the only `dydo` command the guard allows through; the only escape found in lived practice was a single-bash chain `dydo wait --cancel && (dydo wait &) && sleep N && <command>` that sneaks the read in while a fresh wait is still alive in shell-bg.

This is a different shape than #0147 (which fixed the wait re-arm-after-Read race for the single-message case) — #0147 closes the race when *one* message is being processed. #0149 is the deadlock that re-emerges when the **rate of incoming messages exceeds the rate the agent can drain** (agent is paused on a turn boundary and 3 messages land before it gets a tool call back).

## Description

Reproduced live on 2026-05-01 in Noah's session during the v1.4.2 wait-flood smoke test (`smoke-wait-flood-v142`).

Sequence observed:

1. Charlie (test agent) sent 3 messages back-to-back on subject `smoke-wait-flood-v142` while Noah was paused between turns.
2. Noah's general wait fired on msg-1, exited with `Listening=true → false` and a "message received" payload to its stdout.
3. Noah's next tool call — `dydo inbox show` — was blocked: `[dydo guard]: BLOCKED: Agent must keep a general wait active.`
4. Noah re-armed `dydo wait` in the background. The new wait fired on msg-2 and exited within ~10ms (because msg-2 was already-stacked unread at wait-start).
5. Repeat: every re-arm exits immediately on the next stacked unread; every tool call between re-arms is blocked.
6. Noah cannot run `dydo inbox show`, `dydo whoami`, `dydo agent status`, Read, or Write — the entire tool surface is blocked by the guard.

`dydo wait --cancel` is the only command observed to bypass the guard in this state. After `--cancel`, fresh `dydo wait` invocations still exit immediately on stacked unreads.

The escape that worked: a single bash invocation chaining `dydo wait --cancel && (dydo wait &) && sleep 3 && dydo inbox show`. The PreToolUse guard sees one tool call (the bash), and `dydo wait --cancel` at the head of the chain is allowlisted; inside the shell, `(dydo wait &)` runs the wait in shell-level background, the sleep keeps the wait listening, and the inbox show runs while the wait is alive. After the bash call returns, the wait is reaped along with the parent shell and the cycle resumes — but at least one drain has happened.

## Root cause (suspected)

Two interacting design choices:

1. The guard requires `Listening=true` for *every* tool call (`OrchestratorMissingGeneralWait` generalized to `MissingGeneralWait` per Decision 021).
2. `dydo wait` exits on *first* unread observed at-or-after its start, including unreads that were already stacked when the wait was started.

In single-message flow (#0147 territory), this is fine — one fire, one read, one re-arm. The agent's read of the message clears the unread from `state.md.UnreadMessages`, and the re-arm's snapshot starts with empty unreads.

In flood flow, the agent never gets a chance to read between fire-and-re-arm, so each re-arm sees the *next* stacked unread as a fresh trigger and exits immediately. The guard does not allow a Read tool call to happen between fires, so unread state never decreases, and the wait keeps firing on stack.

## Reproduction

1. Onboard agent A as co-thinker with general wait active in background (per standard workflow).
2. Onboard agent B as any role.
3. From B, immediately fire 3 `dydo msg --to A --subject test --body 'msg-N of 3'` calls back-to-back, no delay.
4. Observe A's session: every background `dydo wait` re-arm completes with exit 0 within milliseconds. Every other tool call (`dydo inbox show`, Read, Write) fails with `BLOCKED: Agent must keep a general wait active.`
5. Confirmed on 2026-05-01: messages land correctly (delivery integrity is fine), but the recipient cannot read them through the standard tool surface.

## Suggested fix paths

(Not authoritative — investigator should evaluate.)

- **A. Snapshot semantics for `dydo wait`.** At wait start, snapshot the current `state.md.UnreadMessages` set; only fire on a *new* unread arriving after the snapshot. Stacked unreads at start are treated as already-known; the wait blocks until something genuinely new lands. Agent reads the stacked set whenever it gets to a tool call.
- **B. Guard exemption window for read-side commands.** Guard allows `dydo inbox show`, `dydo inbox read`, and equivalent drain operations even when no general wait is active, on the theory that these are the operations needed to make the next re-arm productive. Narrow allowlist; doesn't open a hole for arbitrary tool calls.
- **C. `dydo wait --keepalive` mode.** A flavor of wait that fires on unread but does not exit; surfaces a notification to stdout (or to a pipe the agent can poll) and continues blocking. Requires a different stdout protocol.
- **D. Guard treats a recently-exited wait as still-listening for N seconds.** Cheap window (e.g. 5s) where the guard considers the wait "draining the message it just received" and lets tool calls through. Risk: hides real deadlocks.

A + B together is probably the cleanest. A fixes the cause; B keeps drain operations possible if A misses an edge case.

## Resolution

Fixed at HEAD: WaitGeneral captures a registration-time unread snapshot and excludes it from firing (WaitCommand.cs:109-145, comments cite #0149), so stacked unreads no longer re-fire the wait. Note: installed binary lags HEAD; behavior goes live with the 2.0 install. Triage sweep 2026-07-04 (Brian, CoS).

## Related

- [Decision 021 — Unified General Wait](../decisions/021-unified-general-wait.md) — the universal-wait policy that sharpens the deadlock.
- [Issue #0147 (resolved)](resolved/0147-wait-fails-to-trigger-on-a-message-that-arrives-during-the-wait-re-arm-gap-race.md) — fix for the single-message wait-re-arm race; this issue is the multi-message flood shape.
- [Issue #0141 (resolved)](resolved/0141-wait-guard-deadlock-dydo-wait-auto-exits-on-already-unread-inbox-state-guard-the.md) — earlier auto-exit-on-already-unread observation; resolution made re-registration nonzero. The fire-on-stacked-unread behavior remained.
- `Commands/WaitCommand.cs` — wait fire/exit logic.
- `Commands/GuardCommand.cs` — `MissingGeneralWait` enforcement.
