---
title: session-context fallback lets hookless processes impersonate the active agent
id: 250
area: project
type: issue
severity: high
status: open
found-by: manual
found-by-agent: Noah
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-08
---

# session-context fallback lets hookless processes impersonate the active agent

Observed live during the codex-mcp-exploration round (2026-07-08): an interactive Codex session
in the live repo ran `dydo agent claim auto` (tripped, as expected — no hook-delivered
session_id) and then `dydo agent role co-thinker --task enable-codex-stop-hook`, which
**succeeded by silently binding to the already-claimed agent Noah**, clobbering the task binding
of the Claude session that legitimately held that identity.

## Description

When a dydo CLI command arrives without a hook-delivered session_id, the guard/CLI path falls
back to the stored session context — the most recent active agent
(`Commands/GuardCommand.cs:311` → `AgentSessionManager.GetSessionContext()`). The fallback
exists so a human can run dydo commands in a plain terminal, but it means **any hookless
process in the repo** — an interactive Codex session, an MCP-spawned Codex agent, a cron job, a
random script — does not *fail* identity resolution; it **impersonates the last active agent**:

- It can mutate that agent's state (role, task binding) out from under the live session.
- Everything it does lands in dydo records attributed to the wrong agent — the cross-vendor
  backlog's "unattributed editor" objection is actually a *misattributed* editor.
- Releasing "its" identity would release the real agent's claim.

Balazs: this behavior should be noted and **tested against before v2.0.6 fully lands**.

## Reproduction

1. In a Claude session, claim an agent and set a role/task (hook-delivered session_id).
2. From any hookless process in the repo (e.g. `codex exec`, or plain terminal), run
   `dydo agent role <role> --task <other-task>`.
3. `dydo whoami` in the original session now shows the other task; the foreign process's
   `dydo whoami` reports the original session's ID and workspace.

Side effect observed: a stray pending task file (`dydo/project/tasks/enable-codex-stop-hook.md`,
assigned Noah) created in the live tree by the foreign session.

## Candidate directions (from the exploration round, not a decision)

- State-mutating commands (`agent role`, `agent release`, `task ...`) should refuse the ambient
  session-context fallback unless the caller can prove it is the process/human that stored it
  (or an explicit `--as-human` style opt-in for terminal use).
- Foreign-vendor / hookless callers belong in a worker-lane-style anonymous path (like Tier-2
  subagent calls carrying agent_id/agent_type): attributable, no claim, no state mutation.

## Resolution

Fixed in the `fix-0250-session-context-ownership` slice. Caller-ownership verification now
extends to the `.session-context` file fallback and to the agent-to-agent attribution path,
with nearest-host-wins semantics disambiguated by command line.

- `AgentRegistry.GetSessionContext()` no longer trusts the file fallback blindly. After the
  manager returns a session id (still cross-checked against the per-agent `.session` per
  #0196), the registry resolves the owning agent and requires
  `IsOwnedByNearestHostCaller(session)` — `IsOwnedByCaller` (ClaimedPid == self, self is a
  descendant of ClaimedPid, or the claimed-host ancestor matches) **plus** the
  nearest-host-wins guard. On failure it returns null: ambient identity resolves to
  "human/unknown terminal" (DR-036), the truthful answer for a foreign process.
- Nearest-host-wins: `ProcessUtils.NoForeignHostNearerThanClaimedHost(claimedPid)` walks from
  the caller toward the claimed host PID and refuses if a *different* agent-host process sits
  nearer than the claimed host. An inner foreign-vendor agent spawned under an outer session
  is a worker, not the agent, so it does not inherit the outer agent's identity.
- **Command-line disambiguation (Windows `node` ancestors).** The npm-installed dydo runs
  `dydo.exe` as a child of a `node` launcher shim (`npm/bin/dydo`), and Claude/Codex CLIs also
  ship as node scripts, so classifying every `node` ancestor by name alone was wrong — it made
  every legitimate Windows+npm CLI call look like it sat under a foreign host, and captured the
  transient launcher PID as the ClaimedPid. A `node` ancestor is now classified by its command
  line (`ProcessUtils.GetProcessCommandLine` → `ClassifyNodeCommandLine`): the dydo launcher
  shim and unrelated node scripts are transparent (keep walking); only a command line that
  names the vendor CLI is a host. When the command line is unreadable the old name-based rule
  applies, except at the launcher position (direct parent of the initial dydo process), which
  is treated as the shim. `FindClaudeAncestor` / `FindCodexAncestor` use the same
  classification, so ClaimedPid capture and #0151's kill-target whitelist share one source of
  truth. Non-Windows behavior is unchanged (claude/codex are named directly there).
- Two resolution paths are now distinct. `GetSessionContext()` (identity-resolving/self-
  mutating commands: `whoami`, `agent role`/`status`/`release`, worktree, wait, …) is the
  gated path above — null for foreign callers. `GetAmbientSessionContext()` (new) returns the
  raw context and backs the agent-to-agent commands (`dydo msg`, `dydo dispatch`), which gate
  ownership via `TryGetCurrentOwnedAgent`. That gate now also applies nearest-host-wins (#0250
  F2): a descendant caller that sits under its own foreign-vendor host is refused, closing the
  MCP-spawned-inner-worker impersonation. It preserves the #0195 "does not own" refusal for
  non-descendant callers and inner workers alike, while still allowing a genuine no-context
  human to dispatch/message anonymously — instead of collapsing into an "Unknown" sender.

Legitimate consumers: a claude session's own `dydo` subprocess is a descendant of the claimed
host (ownership passes), including the Windows+npm shape where the launcher node sits between
dydo and the claude host; dispatched terminals use the `DYDO_AGENT` env path (CORRECTION per
#0256: at the time of this fix the env fast-path was gated only on descendant-only
`IsOwnedByCaller`, NOT nearest-host-wins — a nested foreign-vendor worker inheriting `DYDO_AGENT`
could still role/release the outer agent through it. That gap is closed by #0256, which applies
`IsOwnedByNearestHostCaller` to all four env-path sites); the #0207 resume refresh keys off the hook-delivered `session_id`, which
precedes any CLI subprocess (a CLI call seen before that refresh, against a stale ClaimedPid,
resolves to null rather than the wrong agent). Regression coverage added in `AgentRegistryTests`
(session-context file-fallback ownership region), `ProcessUtilsTests`
(`NoForeignHostNearerThanClaimedHost`, `ClassifyNodeCommandLine`, launcher-shim skipping),
`IdentityHijackMutatingCommandTests` (nested-foreign-worker msg/dispatch), and updated
hijack/worktree/resume fixtures.
