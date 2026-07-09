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

**SCOPE AMENDED 2026-07-09 (planner fold, routed by Adele from the v2.0.6 campaign inquisition):
this row also resolves issue 0256 (HIGH)** — see "0256 fold" section below. Same files, same
threat class as this row's ownership/liveness work; v2.0.7 holds for 0256.

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

## 0256 fold — env fast-path gets the nearest-host-wins gate

Issue 0256: the 0250 fix (7805e004) gated the `.session-context` file fallback and msg/dispatch
on `IsOwnedByNearestHostCaller`, but the `DYDO_AGENT` env fast-path still checks descendant-only
`IsOwnedByCaller` — a nested foreign-vendor worker inheriting the env from a dispatched terminal
can `role`/`release` the OUTER agent. Fix: apply the nearest-host-wins gate to all four env-path
sites the issue names —
- `GetSessionContext` env branch (`Services/AgentRegistry.cs:1308-1313`)
- `GetAmbientSessionContext` (:1286-1296)
- `TryResolveCurrentAgentFromEnvVar` (:1134-1145)
- `VerifyCallerOwnsAgent` (:1095-1099 — the gate `WaitCommand` calls, so this row's wait work
  sits on the fixed check)

The code's own design comment (`AgentRegistry.cs:1064-1071`) already says nearest-host should
gate `GetSessionContext` — make the code match its comment. Tests: extend
`IdentityHijackMutatingCommandTests` (setup currently NULLS `DYDO_AGENT` — the case is
structurally uncovered) and the env-path `AgentRegistryTests` so an INTERPOSED foreign host is
expressible (multi-ancestor PID chain), asserting role/release/wait-registration refuse. Doc
corrections alongside: issue 0250's Resolution text ("env path already ownership-checked" is
wrong) and `backlog/codex-mcp-delegation-experiment.md`'s "resolve NULL ambient identity" claim
(:124 area). Resolve 0256 on land (Resolution + `resolved/` move). Related 0265 (low,
cmdline-substring vendor classification) is explicitly DEFERRED, not this row.

## Files

- `Commands/WaitCommand.cs` — `--register` mode, host-based auto-selection, cancel path.
- `Models/WaitMarker.cs` — durable variant (a kind/flag on the marker, not a second model).
- `Services/AgentRegistry.cs` — marker CRUD + liveness evaluation for the durable kind.
- `Commands/GuardCommand.cs` — `MissingGeneralWait` recognizes live durable markers (file taken
  over from c1-1; rebase on its extraction).
- `Commands/AgentCommand.cs` — release cleanup: the release subcommand (:47) and its wait-check
  (:732-738) remove/accept the caller's durable marker.
- Tests: extend `DynaDocs.Tests/` wait/guard coverage — durable marker satisfies the guard;
  dead-host marker goes stale; release removes it; Claude-host default behavior unchanged.
  Neighbor patterns: existing WaitCommand + `PendingStateGuardTests`. Plus the 0256 fold's
  hijack tests (`IdentityHijackMutatingCommandTests`, env-path `AgentRegistryTests`).
- 0256 doc corrections: `dydo/project/issues/0250-...md` Resolution text,
  `dydo/project/backlog/codex-mcp-delegation-experiment.md` post-0250 claim.
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
