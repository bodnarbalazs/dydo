---
title: DYDO_AGENT env path bypasses the 0250 nearest-host-wins gate - inner foreign-vendor worker can role/release the outer agent
id: 256
area: backend
type: issue
severity: high
status: open
found-by: inquisition
found-by-agent: Leo
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-09
---

# DYDO_AGENT env path bypasses the 0250 nearest-host-wins gate - inner foreign-vendor worker can role/release the outer agent

The 0250 fix gated the .session-context file fallback and msg/dispatch on nearest-host-wins, but the DYDO_AGENT env fast-path in GetSessionContext/GetAmbientSessionContext/TryResolveCurrentAgentFromEnvVar still checks descendant-only IsOwnedByCaller - a nested foreign-vendor worker in a dispatched terminal inherits DYDO_AGENT and can rebind or release the outer agent.

## Description

The 0250 fix (7805e004) added the nearest-host-wins gate (`IsOwnedByNearestHostCaller`) to the `.session-context` file fallback and to `TryGetCurrentOwnedAgent` (msg/dispatch), but the `DYDO_AGENT` env fast-path still gates only on `IsOwnedByCaller` (descendant-of-claimed-host).

**Affected paths** (all check `IsOwnedByCaller` only):
- `GetSessionContext` env branch — `Services/AgentRegistry.cs:1308-1313`
- `GetAmbientSessionContext` — `AgentRegistry.cs:1286-1296`
- `TryResolveCurrentAgentFromEnvVar` — `AgentRegistry.cs:1134-1145`
- `VerifyCallerOwnsAgent` — `AgentRegistry.cs:1095-1099`

**Attack shape** (the exact 0250 threat, via env inheritance instead of the closed file fallback): every dispatched terminal pins `DYDO_AGENT` (`WindowsTerminalLauncher.cs:41`, `LinuxTerminalLauncher.cs:170`) and child processes inherit it. An MCP-spawned foreign-vendor worker (codex-under-claude) is a descendant of `ClaimedPid`, so `IsOwnedByCaller` passes — it would fail `NoForeignHostNearerThanClaimedHost` (`ProcessUtils.Ancestry.cs:200-231`), but that guard is never consulted on the env branch. The inner worker resolves as the OUTER agent for all self-mutating commands: `dydo agent role --task X` rebinds the outer agent's task, `dydo agent release` releases its claim, plus `whoami`, wait-marker registration, and provenance stamping.

**Why per-slice reviews missed it:** msg/dispatch ARE protected (`TryGetCurrentOwnedAgent` re-checks nearest-host at `AgentRegistry.cs:1123`); only the self-mutating command surface is open. Tests null `DYDO_AGENT` in setup (`IdentityHijackMutatingCommandTests.cs:35`) so the case is structurally uncovered, and the env-path `AgentRegistryTests` inject a single ancestor PID that cannot express an interposed foreign host.

**Doc corrections needed alongside the fix:** issue 0250's resolution text claims the env path is "already ownership-checked", and the codex-mcp backlog (d7546fac) asserts inner workers "resolve NULL ambient identity" — both wrong for this path. The code's own design comment (`AgentRegistry.cs:1064-1071`) says `IsOwnedByNearestHostCaller` should gate `GetSessionContext`, contradicting the env branch.

**Preconditions:** nested worker inherits `DYDO_AGENT` (OS default) and is a live descendant of the claimed host — both hold under the in-scope codex-mcp delegation shape.

Found by the v2.0.6 campaign inquisition (cross-campaign lens: 0250 ancestor classification x dispatched-terminal env pinning). Confirmed independently by the correctness and security sweeps; adversarially verified.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)