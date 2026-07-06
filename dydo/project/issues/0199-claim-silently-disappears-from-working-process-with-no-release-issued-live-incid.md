---
title: Claim silently disappears from working process with no release issued (live-incident corroboration)
id: 199
area: backend
type: issue
severity: high
status: open
found-by: inquisition
date: 2026-05-19
---

# Claim silently disappears from working process with no release issued (live-incident corroboration)

Two consecutive whoami calls in the same process, ~30s apart with no release between them, returned contradictory identities; whoami and agent status also disagreed about the same process at the same instant — claim vanished mid-task, blocking the dispatcher-callback recovery path.

## Description

A claim can vanish from a running process without any `dydo agent release` call. Two `dydo whoami` invocations in the same uninterrupted process, ~30 seconds apart with no state-mutating commands between them, returned contradictory identities — the first reporting a working claim, the second reporting "no agent identity assigned" with the same name reappearing in the Free agents list. Adjacent `dydo agent status` calls disagreed with `whoami` about the same process at the same instant, demonstrating that the two user-facing identity commands take divergent code paths against the same registry state.

Operationally, an agent that loses its claim mid-task cannot `dydo msg` (no identity to send from), so it cannot report the failure back to whoever dispatched it. The natural recovery path — "ping the dispatcher and re-dispatch cleanly" — is structurally unavailable from inside the affected process.

Source: `dydo/project/inquisitions/identity-hijack-bug-class.md` §"2026-05-19 — Zelda" finding F15.

Same bug class as #0183 (root primitive) — out of scope for the F1 fix slice; tracked here for future investigation.

## Evidence

Same uninterrupted process, ~30 seconds apart, no `dydo agent release` between them:

- **Call 1** — `dydo whoami`: returns `Dexter`, `status: working`.
- **Call 2** — `dydo whoami` (no release, no state mutation between calls): returns `No agent identity assigned to this process` and lists Dexter back among **Free agents**.

A `dydo agent status` between the two `whoami` calls *already* reported `No agent identity assigned to this process` — i.e. `whoami` and `agent status` disagreed about the same process at the same instant, then both converged to "no identity". `whoami` reads the in-process claim; `agent status` reads registry state. The two were briefly out of sync.

## Relation to Brian's surfaces

This is S0's asymmetry visible from the *outside*. Brian shows that `GetSessionContext` and `GetCurrentAgent` can disagree under env-var manipulation. This finding shows that the two user-facing commands wrapping those primitives can also disagree, and that a process's claim can vanish under it during routine work. Brian's F1 fix (PID-binding in `GetSessionContext`) needs a companion question: *what cleared the claim?* Per `architecture.md` lines 79, 134–141 the Claim audit event carries `recovery_kind` and `resume_predecessor_session` fields — a follow-up scout should join those against the audit JSONs for this session to determine whether the watchdog/auto-resume path reclaimed the agent under a different SessionId.

## Suggested follow-up

Reviewer scout — claim disappearance audit. What code paths can clear a process's in-memory claim *without* an explicit `dydo agent release`? Suspects: stale-session sweeper, guard read-block side effects, claim-on-claim collision, watchdog auto-resume reclaim. Cross-reference Claim audit `recovery_kind` / `resume_predecessor_session` fields for the session described in the addendum.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)