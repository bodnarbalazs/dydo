---
id: 194
area: backend
type: issue
severity: medium
status: open
found-by: inquisition
date: 2026-05-19
---

# Lifecycle audit events record the hijacked agent because AgentRegistry.SetRole/ReleaseAgent/ClaimAgent pass the poisoned sessionId from GetSessionContext

Under identity hijack, audit attribution is partially corrupted. The guard's own LogAuditEvent (GuardCommand.cs:1453) uses the truthful sessionId from Claude Code's hook input and therefore identifies the actual caller correctly. The corruption is at AgentRegistry.LogLifecycleEvent callers (SetRole:760, ReleaseAgent:561, ClaimAgent:445) — they pass the poisoned sessionId and the hijacked agentName, so Role/Release/Claim audit events name the hijacked target. Cross-correlation with the preceding bash event is possible, but naive audit-replay tools see misleading lifecycle attribution. Transitively fixed by F1 (issue #0183); pin audit attribution to the truthful sid as defense-in-depth.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)