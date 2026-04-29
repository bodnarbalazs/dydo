---
id: 133
area: backend
type: issue
severity: high
status: open
found-by: manual
date: 2026-04-29
---

# Orchestrator general-wait deadlock recurs (bcff3f4 incomplete)

## Description

**Mechanism.** `dydo wait` (general, no `--task`) returns immediately rather than blocking — both when the inbox is empty AND when there are unread messages — so the orchestrator-must-have-general-wait-active guard check fires before EVERY tool call (Bash, Read, Edit, etc.) for any agent in role `orchestrator`. Net effect: once an orchestrator has any unread inbox message, every subsequent tool call is BLOCKED unless `unread-messages` in `dydo/agents/<orch>/state.md` is hand-cleared.

The recurrence cycle observed during the 2026-04-29 orchestration session (Brian on `orchestrator-handoff`):

1. Code-writer or reviewer messages the orchestrator with a result.
2. dydo populates `unread-messages: [...]` in the orchestrator's `state.md` frontmatter.
3. Orchestrator runs `dydo wait` in background (per the guard's instruction).
4. `dydo wait` exits immediately — does not block on the unread message.
5. Orchestrator's next tool call is blocked: "Orchestrator must keep a general wait active. Run: dydo wait (in background)".
6. Loop indefinitely until a human edits `unread-messages: []` in state.md.

`dydo guard lift Brian N` does NOT bypass this gate — the lift suspends RBAC restrictions only. The orchestrator-general-wait check is workflow enforcement, separate layer.

`bcff3f4` (2026-04-28) was supposed to close this class of bug. It addressed the PowerShell-tool bypass cleanly (PowerShell now routes through the guard), but the underlying `dydo wait` blocking semantics remain broken: the wait does not satisfy the guard's "wait active" check.

## Reproduction

1. Claim an orchestrator role: `dydo agent claim X` then `dydo agent role orchestrator --task <some-task>`.
2. Have any agent send the orchestrator a message: `dydo msg --to <X> --subject foo --body bar`.
3. As the orchestrator, run `dydo wait` (background).
4. Run any tool call (e.g., `dydo inbox show`) — observe: BLOCKED with "general wait active" error, despite the wait being registered.

Manifests during every multi-round multi-agent orchestration session.

## Suggested Fix

Two viable directions:

1. **Make `dydo wait` (general, no `--task`) actually block** until either (a) a new unread inbox message arrives, or (b) the wait is cancelled. Today it appears to exit immediately. The audit-event-driven tracking is fine for `--task` waits but the general-wait variant has no such trigger condition — likely just needs a long sleep + wakeup-on-inbox-poll or equivalent.
2. **Loosen the gate** so any active `dydo wait --task` registration counts as "wait active". That makes the orchestrator-pattern of one task-wait-per-dispatched-agent self-satisfying, and removes the need for a separate general wait altogether.

Either path requires the orchestrator workflow doc (`Templates/mode-orchestrator.template.md` § "Dispatch") to be updated to match.

## Workaround in the meantime

Human edits `unread-messages: []` in `dydo/agents/<orch>/state.md` to break the loop on each cycle. Tedious but functional. The session at 2026-04-29 documented the pattern across 4 dispatch rounds.

## Related

- `bcff3f4` — original (incomplete) deadlock fix
- `dydo/agents/Adele/notes-regression-verify.md` — Issue 3 in Adele's notes; first observation of this class.

## Resolution

(Filled when resolved)