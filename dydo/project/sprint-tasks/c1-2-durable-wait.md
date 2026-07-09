---
title: c1-2 Durable Wait Registration for Codex Hosts
blocked-by: c1-1-read-verb
due:
needs-human: false
priority: High
sprint: c1-codex-adoption
status: ready
work-type: feature
area: backend
type: context
---

# c1-2 Durable Wait Registration for Codex Hosts

0254 item (3): `dydo wait` is unusable on a codex host — foreground dies to the codex tool
timeout, `Start-Process` backgrounding creates no `.waiting` marker, and the guard then blocks
with "must keep a general wait active" (GuardCommand.cs:1588-1601 → 1603-1613). Fix direction
from the issue: **durable marker-based wait registration** — the marker's validity keys to the
claimed session's host process, not to a live `dydo wait` process.

## Behavior

- `dydo wait --register` (new mode; also auto-selected when the caller's session host is a
  vendor whose runtime cannot hold a foreground wait — resolve from `AgentSession.Host`): write
  a durable general-wait marker and return immediately.
- Liveness: the durable marker is live while the claimed session's host process is alive — key
  to `AgentSession.ClaimedPid` / `ProcessUtils.FindAgentHostAncestor` (the same chain
  `WaitCommand.ResolveHostLivenessPid` uses, WaitCommand.cs:228-232) instead of the wait
  process's own lifetime. Dead host → marker is stale and cleaned like today's dead waits.
- The guard's `MissingGeneralWait` check accepts a live durable marker as satisfying "general
  wait active".
- Message delivery on codex hosts is poll-based (`dydo inbox show` / `dydo read`) — the durable
  marker satisfies the protocol; it does not simulate push. (Record's out-of-scope: push parity.)
- `dydo agent release` and `dydo wait --cancel` remove the agent's durable marker (release
  cleanup per co-think outcome 2).

## Files

- `Commands/WaitCommand.cs` — `--register` mode, host-based auto-selection, cancel path.
- `Models/WaitMarker.cs` — durable variant (a kind/flag on the marker, not a second model).
- `Services/AgentRegistry.cs` — marker CRUD + liveness evaluation for the durable kind.
- `Commands/GuardCommand.cs` — `MissingGeneralWait` recognizes live durable markers (file taken
  over from c1-1; rebase on its extraction).
- Release cleanup: the release path (follow `dydo agent release` from `Commands/` into
  `AgentRegistry`) removes the caller's durable marker.
- Tests: extend `DynaDocs.Tests/` wait/guard coverage — durable marker satisfies the guard;
  dead-host marker goes stale; release removes it; Claude-host default behavior unchanged.
  Neighbor patterns: existing WaitCommand + `PendingStateGuardTests`.
- Doc surfaces: `--register` is a new flag on an existing command → `dydo-commands.md` +
  template sections for `wait` (Tests 2/3/4 of `CommandDocConsistencyTests`); workflow-template
  wait guidance for codex hosts (after c1-1's template edit).

## Gates (exact commands)

- `python DynaDocs.Tests/coverage/run_tests.py`
- `DynaDocs.Tests/coverage/gap_check.py --force-run`
- `dydo check`

## Sequencing

**After c1-1** (shared `Commands/GuardCommand.cs`, `Services/AgentRegistry.cs`, and the
dydo-commands/template + workflow-template doc surfaces). Blocks c1-6 (same guard file).

## Success criteria

A codex-hosted agent registers a wait that survives its tool timeouts; the guard never blocks it
with "must keep a general wait active" while its session lives; stale markers clean up on host
death; release cleans up the marker. Issue 0254 resolved (jointly with c1-1: move to
`resolved/`, fill Resolution). Suite green.
