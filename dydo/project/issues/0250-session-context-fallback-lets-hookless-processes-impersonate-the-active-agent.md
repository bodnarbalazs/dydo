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

(Filled when resolved)
