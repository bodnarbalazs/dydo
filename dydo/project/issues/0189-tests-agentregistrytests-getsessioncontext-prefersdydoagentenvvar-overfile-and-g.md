---
id: 189
area: backend
type: issue
severity: high
status: open
found-by: inquisition
date: 2026-05-19
---

# Tests AgentRegistryTests.GetSessionContext_PrefersDydoAgentEnvVar_OverFile and GetCurrentAgent_PrefersDydoAgentEnvVar_OverHintFile encode the wrong contract

Both existing DYDO_AGENT tests pass under any implementation (including a PID-binding fix and the current buggy code) — their setup makes the assertions vacuously true and their names mislead a future maintainer into treating the env-var fast path as the intended invariant rather than the bug pivot. Needs rewrite alongside the F1 fix.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)