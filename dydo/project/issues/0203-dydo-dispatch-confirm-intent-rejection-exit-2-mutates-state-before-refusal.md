---
id: 203
area: backend
type: issue
severity: high
status: open
found-by: inquisition
date: 2026-05-19
---

# dydo dispatch confirm-intent rejection (exit 2) mutates state before refusal

An oversight-role --no-wait dispatch rejects with exit 2 and 'run again and it will pass', but the re-run rejects with 'agent already working on task' — the confirm-intent gate either runs after state mutation, or task-name uniqueness is decided in an unobservable race; either way, an error-coded command performs the side effect it nominally refused.

## Description

`dydo dispatch` rejects an oversight-role `--no-wait` call with exit 2 and the message "If you really mean --no-wait (fire-and-forget), run again and it will pass." But the second, byte-for-byte identical invocation then rejects with "Dexter is already working on task '<name>'" — meaning either (a) the first call *did* mutate state (assigned the task, possibly created an inbox item) before printing the "confirm intent" prompt and exiting 2, or (b) some other concurrent process raced in and dispatched the same task name. Either branch is bug-class behaviour: in (a), an error-coded command performs the side effect it nominally refused; in (b), task-name uniqueness was decided in a race the operator can't observe.

This is the same split-brain shape as F15 (claim silently disappearing) and may be its downstream consequence — orphaned `status: working` metadata from a vanished claim blocks the re-dispatch.

Source: `dydo/project/inquisitions/identity-hijack-bug-class.md` §"2026-05-19 — Zelda" finding F19.

Same bug class as #0183 (root primitive) — out of scope for the F1 fix slice; tracked here for future investigation.

## Evidence

Attempting to hand off to a judge per the inquisitor workflow:

**Call 1** — `dydo dispatch --no-wait --auto-close --role judge --task identity-hijack-bug-class-ruling --brief "…"`:

```
Exit code 2
Oversight roles should use --wait so dispatched agents' replies route back to you.
If you really mean --no-wait (fire-and-forget), run again and it will pass.
```

The exit code, the wording ("run again and it will pass"), and convention elsewhere in dydo all signal "no action taken; we want you to confirm intent." A user reading this is led to believe nothing happened.

**Call 2** — same command, byte-for-byte, re-run to confirm intent:

```
Exit code 2
Dexter is already working on task 'identity-hijack-bug-class-ruling'.
If you need to re-dispatch, have them release first.
```

So Dexter has the task. Either:

- (a) Call 1 *did* mutate state (assigned the task to Dexter, possibly created an inbox item) before printing the "use --wait" prompt and exiting 2 — i.e. the "confirm intent" gate runs *after* state mutation, not before, or
- (b) Some other concurrent process independently dispatched the same task name to Dexter in the gap between Call 1 and Call 2.

Either branch is bug-class behaviour.

Side detail: Dexter is also the agent whose claim silently disappeared from this same process earlier (F15). It would not be surprising if Dexter's "still working" state from before the silent claim loss is the same state now blocking the new dispatch — i.e. F15 left orphaned `status: working` metadata, and F19 is the downstream consequence (the registry refuses to re-target Dexter because Dexter's prior state never got reconciled).

## Relation to Brian's surfaces

Possibly extends S0/F2 — `GetCurrentAgent` and dispatch-side validation may be using inconsistent views of "is agent X busy?". Also a candidate **S15** in Brian's surface map: error-path state-mutation leaks, where a command that exits non-zero has already performed observable side effects on the registry/inbox.

## Suggested follow-up

Trace `dydo dispatch`'s confirm-intent gate: confirm whether state mutation occurs before or after the gate, and whether the inbox file (if any) is rolled back on exit 2. Operationally, the inquisitor workflow could not complete cleanly — the judge dispatch is the workflow's final step ("Hand Off to the Judge"); every workflow that uses a confirm-intent dispatch is exposed to the same primitive.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)