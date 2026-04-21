---
type: decision
status: accepted
date: 2026-04-20
area: platform
---

# 019 — Reviewer Verdict Routing and Subject-Aware Send Diagnostics

## Context

Previously, `dydo review complete --status pass` produced no inter-agent
message: the verdict was only reflected in task state and in the reviewer's
terminal output. The code-writer who dispatched the reviewer was expected to
manually forward the verdict to whichever orchestrator spawned them. In
practice that step was frequently skipped — the code-writer would release,
their `.session` would disappear, and the orchestrator's
`dydo wait --task <name>` would fire on subject match but with no inbox
message addressed to them. The orchestrator then had to manually reconstruct
what happened from task state.

A related pair of `dydo msg` pain points surfaced at the same time:

1. When the target agent has already released, the send fails with a terse
   error that tells the sender nothing about where the message SHOULD have
   gone.
2. When the target is active but has only specific `dydo wait` markers
   (e.g. `--task foo`) and the sender types a subject that doesn't match
   any of them (`--subject fooo`), delivery succeeds silently but the
   target's wait never fires. Typos become invisible timeouts.

Frank co-thought five options (A–E) with the user. Option A — auto-CC the
nearest `canOrchestrate` ancestor on PASS, at the `review complete` call
site — was chosen. See the summary under *Rationale* for why the alternatives
were rejected.

## Decision

Three behavioral changes, implemented in `Commands/ReviewCommand.cs` and
`Services/MessageService.cs`:

1. **PASS verdicts auto-route to the dispatcher and CC the nearest
   orchestrator ancestor.** On `dydo review complete --status pass`,
   `RouteVerdictMessages` writes a verdict message addressed to
   `reviewer.DispatchedBy` (the code-writer), clears the reviewer's
   reply-pending marker for the task, then walks the `DispatchedBy` chain
   upward from the code-writer until it finds an agent whose role has
   `canOrchestrate: true`. If such an ancestor exists and is not the
   dispatcher itself or the reviewer, a `[CC]` message is sent to them on
   the same subject. FAIL verdicts intentionally do NOT auto-CC (per
   Frank's co-thinking notes — fail CCs were considered and ruled out
   because a failing review usually needs a redo from the code-writer, not
   intervention from the orchestrator).

2. **Released-target send error lists waiters on the same subject.**
   `MessageService.BuildInactiveTargetMessage` now, when a subject is
   present, appends an `Agents waiting on subject '<subject>'` block to
   the error text. The sender can immediately see that e.g. an
   orchestrator has an active `dydo wait --task X` on the same subject
   and redirect the message.

3. **Subject-mismatch warning on successful send.**
   `MessageService.WarnOnSubjectMismatch` emits a non-blocking warning to
   **stderr** when (a) the recipient is currently `Working`, (b) they
   have at least one specific wait marker, (c) they have no general
   (`_`-prefixed) wait, and (d) none of their specific waits matches the
   outgoing subject. The message still delivers. The warning names the
   recipient's actual waits so the sender sees the diff.

## Rationale

### Why auto-CC at `review complete`, not later in the lifecycle

`review complete --status pass` is the synchronous moment at which the
verdict crystallises. Later hook points considered:

- On reviewer release: racy. The reviewer may send additional messages
  between `review complete` and `agent release`; auto-CCing on release
  would either duplicate the verdict or require state to remember "I
  already CC'd." Both are worse than doing it once, at the verdict.
- On code-writer inbox-clear of the verdict: semantically wrong — the
  CC is a notification to the orchestrator about the review, not about
  the code-writer's reading habits.
- On orchestrator's `dydo wait` fire: out of scope. `wait` is a passive
  consumer; making it the producer of messages inverts the data flow.

### Why walk `DispatchedBy` instead of filtering `WaitMarker`s

`WaitMarker.Since` filtering was one of Frank's ruled-out alternatives.
The problem: a marker's `Since` timestamp doesn't tell you *which*
agent's release should satisfy it — only when the wait began. An
orchestrator who waits on `task X` might be waiting for a code-writer,
a reviewer, or an inquisitor to finish. The dispatch chain is the
correct structural answer to "who should I report to?", and we already
record it in `state.md`.

### Why FAIL doesn't auto-CC

A failing review is information the code-writer acts on (fix the issues,
re-dispatch). Forwarding a failure up the chain invites the orchestrator
to intervene before the code-writer has had a chance to address the
review. If the code-writer fails repeatedly or goes silent, that's a
separate signal (stale dispatch, unread inbox) with its own handling.

### Why `DeliverInboxMessage` is a new public helper instead of reusing `Execute`

`MessageService.Execute` performs ownership checks, reply-pending
bookkeeping on the *sender's* behalf, and user-facing console output.
The reviewer verdict routing path is system-initiated: the reviewer did
not invoke `dydo msg`, and the CC recipient is not the reviewer's direct
obligation. `Execute` would print "Reply obligation fulfilled" banners
in the wrong voice and enforce the wrong validation. Extracting the
pure inbox-write path (`DeliverInboxMessage`) gives the verdict routing
code a clean, side-effect-scoped surface to call. `Execute` now composes
`DeliverInboxMessage` + the reply-pending and subject-mismatch logic,
so there is no duplication.

### Why the waiters helper lives in `MessageService`, not `AgentRegistry`

`AgentRegistry.AgentNames` and `AgentRegistry.GetWaitMarkers(name)` are
already public and sufficient. The "who waits on subject X" question is
specific to the send-diagnostics path in `MessageService`; it does not
belong in the registry's API surface. It is implemented as a
`private static` helper in `MessageService` that composes the existing
registry methods. `Services/AgentRegistry.cs` was not modified by this
lane — Brian's dispatch brief explicitly scoped it out to avoid
conflicts with a parallel lane touching that file.

## Consequences

- Orchestrators that dispatch a code-writer and wait on the task now
  reliably receive the reviewer's PASS verdict without the code-writer
  having to forward it manually. Their `dydo wait --task <name>` fires
  on subject match AND the inbox contains a `[CC]` message explaining
  the outcome.
- `dydo msg` errors and warnings are now subject-aware, shortening the
  feedback loop for typos and stale-target sends.
- `MessageService.DeliverInboxMessage` is now a public API surface.
  Future system-initiated inbox writes (e.g. watchdog-generated
  notifications) can reuse it rather than re-implementing the file
  layout.
- Five reproducer tests in
  `DynaDocs.Tests/Integration/ReviewerVerdictRoutingTests.cs` cover:
  PASS auto-CCs nearest orchestrator ancestor; PASS without an ancestor
  does not CC; FAIL does not CC; released-target error lists waiters;
  subject-mismatch warning on active target.

## Related

- [003 — Agent Messaging](./003-agent-messaging.md) — the inbox/wait
  primitives this decision builds on.
- [005 — Fresh Agent Over Wait-for-Feedback](./005-fresh-agent-over-wait-for-feedback.md)
  — the reviewer-in-fresh-session model that makes manual verdict
  forwarding awkward in the first place.
- [010 — Baton-Passing and Review Enforcement](./010-baton-passing-and-review-enforcement.md)
  — the reply-obligation mechanics that the PASS auto-send clears on
  the reviewer's behalf.
