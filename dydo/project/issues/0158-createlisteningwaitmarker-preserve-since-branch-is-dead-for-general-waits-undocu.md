---
id: 158
area: backend
type: issue
severity: low
status: open
found-by: inquisition
date: 2026-05-01
---

# CreateListeningWaitMarker preserve-Since branch is dead for general waits — undocumented asymmetry vs task waits

`CreateListeningWaitMarker`'s `Since` preservation branch is unreachable for general waits because `WaitGeneral`'s `finally` always removes the marker before the next registration, so each re-arm starts with a fresh `Since`. That is fine for the proposed #0149 fix but creates an undocumented asymmetry vs task waits (which keep their dispatcher-supplied `Since`) that the method's contract implies otherwise.

## Description

`Services/AgentRegistry.CreateListeningWaitMarker` (`Services/AgentRegistry.cs:1042-1091`) reads any existing marker file and preserves the existing `Target` and `Since` if found (`:1053-1068`). The comment on the method advertises this as a deliberate feature so callers can flip a dispatcher-pre-created marker to listening without losing dispatch context.

But for the `_general-wait` sentinel, the preservation branch is dead. `Commands/WaitCommand.WaitGeneral`'s `finally` block (`Commands/WaitCommand.cs:144`) calls `RemoveWaitMarker(agentName, GeneralWaitMarker)` on every exit, so the next registration always finds no existing marker and falls through to the `since = DateTime.UtcNow` initialization at line 1051.

The "preserve `Since`" branch only fires for `WaitForTask` (line 157), where the marker is created by the dispatcher pre-launch and `WaitForTask`'s `finally` calls `ResetWaitMarkerListening` instead of `RemoveWaitMarker`.

## Why this matters

This is fine for the proposed #0149 fix (a `Since`-based registration filter would use the *new* `Since`, which is what we want for general waits — each re-arm is a fresh observation window). But it's a code/intent inconsistency worth resolving:

- A future maintainer reading `CreateListeningWaitMarker`'s contract will assume `Since` survives wait re-arms for the general-wait path. It does not.
- The asymmetry between general waits (always-fresh `Since`) and task waits (preserved `Since`) is undocumented anywhere — neither in the marker model, the method comment, nor the wait command itself.

## Suggested fix

Pick one:

- **A. Document the asymmetry** in `CreateListeningWaitMarker`'s summary comment: the preservation branch is intended only for caller-pre-created markers (today: `WaitForTask` after dispatcher pre-creation). Wait paths that remove their marker on exit always get a fresh `Since`.
- **B. Drop the preservation branch from the general-wait path.** Inline a "fresh general wait" helper that always writes `since = DateTime.UtcNow`, leaving `CreateListeningWaitMarker` semantics intact for task waits. More code, but clearer separation of intent.

A is the surgical fix; B is justified only if/when a third caller is added to `CreateListeningWaitMarker` and the asymmetry becomes harder to track.

## Related

- `Services/AgentRegistry.cs:1042-1091` — `CreateListeningWaitMarker`.
- `Commands/WaitCommand.cs:101, 144` — general-wait creation and removal.
- `Commands/WaitCommand.cs:157, 185` — task-wait creation and `ResetWaitMarkerListening`.
- Inquisition: [wait-rearm-flood-deadlock](../inquisitions/wait-rearm-flood-deadlock.md) (Finding #6) — surfaced while tracing the wait re-arm semantics for #0149.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)