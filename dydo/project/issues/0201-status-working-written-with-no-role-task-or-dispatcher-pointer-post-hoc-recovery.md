---
id: 201
area: backend
type: issue
severity: medium
status: open
found-by: inquisition
date: 2026-05-19
---

# status: working written with no role, task, or dispatcher pointer — post-hoc recovery impossible

An agent claim can carry status: working with no role/task/dispatcher metadata, making the operator's natural recovery (ping the dispatcher to re-dispatch) structurally impossible — there is no field on the claim that records who dispatched it.

## Description

An agent claim can carry `status: working` with no role, no task, and no dispatcher pointer — leaving the operator's natural recovery path ("ping the dispatcher and have them re-dispatch cleanly") structurally impossible. Either the dispatch routed `status: working` to the wrong agent (a hijack symptom), or the agent was already in `status: working` from a prior orphaned session that never released and the registry didn't reconcile. Either branch is bug-class symptomatic.

There is currently no field on the claim that records "who dispatched me", so a context-lost agent has no in-band way to notify its originator.

Source: `dydo/project/inquisitions/identity-hijack-bug-class.md` §"2026-05-19 — Zelda" finding F17.

Same bug class as #0183 (root primitive) — out of scope for the F1 fix slice; tracked here for future investigation.

## Evidence

First `dydo whoami` of the live-incident session showed:

```
Role: (none set)
Status: working
```

`status: working` is normally a side effect of dispatch-with-brief. But role was unset and task was unrecorded. Either:

- (a) The dispatch routed `status: working` to the wrong agent (Dexter instead of Emma) — a hijack symptom, or
- (b) Dexter was already in `status: working` from a prior orphaned session that never released and the registry didn't reconcile.

Either branch is a bug-class symptom. Because no field records "who dispatched me", an agent in working state without context cannot ping the originator to recover — the operator's natural recovery is structurally impossible.

## Relation to Brian's surfaces

Possible new identity surface — dispatch metadata (role/task/dispatcher) not bound to the claim it should accompany. A claim in `status: working` should never lack the metadata that explains *why* it's working.

## Suggested follow-up

Add a `dispatch_origin` field on the agent claim state, populated at dispatch time, surfaced by `dydo whoami` and `dydo agent status`, so a hijacked or context-lost agent can at least notify its originator. Pair with F14: if the dispatcher-recorded name and the actual claim diverge, refuse the dispatch or warn loudly.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)