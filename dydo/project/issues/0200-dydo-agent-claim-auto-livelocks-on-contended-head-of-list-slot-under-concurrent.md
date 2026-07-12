---
title: dydo agent claim auto livelocks on contended head-of-list slot under concurrent claim pressure
id: 200
area: backend
type: issue
severity: medium
status: open
found-by: inquisition
date: 2026-05-19
---

# dydo agent claim auto livelocks on contended head-of-list slot under concurrent claim pressure

Four consecutive claim auto invocations alternated between a too-coarse dispatched-agents gate and a stale CAS-loss retarget pointing at the same just-taken name; operator was locked out of 24 other free agents until they fell back to a specific-name claim.

## Description

`dydo agent claim auto` can livelock on a contended head-of-list slot under concurrent claim pressure. Four consecutive `claim auto` invocations from a single process — no other commands interleaved — alternated between two failure modes (a coarse "dispatched agents waiting" gate and a stale CAS-loss retarget that kept pointing at the same just-taken name) while 24 other free agents remained claimable. Workaround: claim a specific name far from the head of the alphabet.

**Not strictly identity-hijack class.** This is a claim *allocation* race — the allocator's view of the free list lags reality. It's filed here as a sibling case because the operational symptom (an operator who follows instructions still cannot make forward progress) and the underlying shape (two subsystems disagreeing about state) match the hijack family. A future judge should not conflate this with the identity-resolution bug.

Source: `dydo/project/inquisitions/identity-hijack-bug-class.md` §"2026-05-19 — Zelda" finding F16.

Same bug class as #0183 (root primitive) — out of scope for the F1 fix slice; tracked here for future investigation.

## Evidence

Four consecutive `dydo agent claim auto` invocations from the same process, no other commands interleaved:

| # | Output |
|---|--------|
| 1 | `There are dispatched agents waiting to be claimed. … 'auto' is probably not meant for you. If you intentionally want auto-assignment, run the command again.` |
| 2 | `Agent Adele is already claimed by another session. Claimable agents for human 'balazs': Adele, Brian, Charlie, …` |
| 3 | `There are dispatched agents waiting to be claimed. …` (same wording as #1) |
| 4 | `Agent Adele is already claimed by another session. …` (same wording as #2) |

Two distinct issues surface:

1. **Stale head-of-list retarget.** Auto-claim tried Adele on call #2 *and again* on call #4. The free-agent list at the previous `whoami` listed Adele as free; between #1 and #2 another process won Adele. By #4 the allocator was still pointing at Adele as the first candidate — no advancement past a CAS-loss to the next free name.
2. **"Dispatched agents waiting" gate is too coarse.** The gate fires whenever *any* dispatch is outstanding system-wide, not whenever a dispatch is outstanding *for the calling human/process*. In this session the gate produced a misleading "auto is probably not meant for you" when the operator unambiguously wanted auto.

Workaround applied: `dydo agent claim Zelda` (specific name, far down the alphabet) — succeeded immediately on first try.

## Relation to Brian's surfaces

Not directly an identity-resolution hijack — auto-claim allocates a *new* claim rather than resolving an existing one. But it's the same family of split-brain: the allocator's view of the free list lags reality, and the operator can be locked out of claiming any of 25 free agents because the allocator keeps choosing the one that just got taken. Sibling case in the same prosecution because the symptom (an instruction-following operator still cannot progress) matches the hijack class.

## Suggested follow-up

Reviewer scout — `claim auto` allocation logic. Read the allocator, confirm/disprove "no advancement past CAS-loss" (the primary mechanism). If confirmed, a one-line fix (continue the loop) closes the livelock. Pair with a test-writer scout: property test where N processes call `claim auto` in parallel, asserting all N succeed with distinct names; current behaviour is that at least one livelocks.

## Reproduction

(Steps to reproduce, if applicable)

## Attempted + deferred to a design pass (2026-07-12)

Swarm attempt (Batch 2): an advance-on-CAS-loss loop (exclude the taken candidate + re-read the free list, `AgentRegistry.ClaimAuto`) was implemented and is correct+bounded IN ISOLATION, but Claude review found it CANNOT converge in the real hook flow: the pending session is provisioned only under the head-of-list name by `GuardCommand.HandleClaimSessionStorage`, so every advanced candidate fails "No session ID available. Claim must be initiated via hook." — the lockout persists with a MORE misleading error. The unit tests faked convergence by pre-seeding the session under the eventual winner (a state production never reaches). REVERTED. Proper fix needs a guard-side session-provisioning companion — forward/re-provision the pending session to the next candidate on advance, or key the pending session by session-id rather than agent-name, or provision all free candidates. This is a design pass, not a quick patch (matches the issue's original "future investigation" note). The advance-loop code (correct-in-isolation) is a valid building block for the redo but was not preserved.

## Resolution

(Filled when resolved)