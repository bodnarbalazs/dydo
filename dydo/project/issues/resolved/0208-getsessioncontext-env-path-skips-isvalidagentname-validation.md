---
id: 208
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-05-21
resolved-date: 2026-07-04
---

# GetSessionContext env path skips IsValidAgentName validation

AgentRegistry.GetSessionContext reads DYDO_AGENT and calls GetSession directly with no IsValidAgentName guard, unlike its sibling TryResolveCurrentAgentFromEnvVar which validates — an inconsistency between two env-path readers hardened in the same slice.

## Description

AgentRegistry.GetSessionContext (Services/AgentRegistry.cs:1076-1086) reads the attacker-controlled DYDO_AGENT env var and passes it straight to GetSession(agentName), which builds Path.Combine(WorkspacePath, agentName, '.session'). There is no IsValidAgentName guard.

Its sibling TryResolveCurrentAgentFromEnvVar (AgentRegistry.cs:950-961), extracted in the same Slice A change, does validate: 'if (string.IsNullOrEmpty(envAgent) || !IsValidAgentName(envAgent)) return null;'. IsValidAgentName checks the configured agent pool, so it rejects path-traversal names.

DYDO_AGENT is attacker-controlled in this slice's own threat model. An unvalidated name is at worst a .session file-existence probe outside the agents directory; the result still must pass IsOwnedByCaller, so this is not directly exploitable. The defect is the inconsistency: two env-path readers hardened in the same slice, only one validates the name. Per coding-standards section 6 (validate at boundaries), add the IsValidAgentName guard to GetSessionContext for symmetry. Low severity; can be a follow-up after Slice A merges.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed at HEAD: GetSessionContext's env path validates the DYDO_AGENT name with IsValidAgentName and IsOwnedByCaller before use (AgentRegistry.cs:1142-1147), matching its sibling reader. Triage sweep 2026-07-04 (Brian, CoS).