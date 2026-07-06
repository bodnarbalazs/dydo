---
id: 189
area: backend
type: issue
severity: high
status: resolved
found-by: inquisition
date: 2026-05-19
resolved-date: 2026-07-04
---

# Tests AgentRegistryTests.GetSessionContext_PrefersDydoAgentEnvVar_OverFile and GetCurrentAgent_PrefersDydoAgentEnvVar_OverHintFile encode the wrong contract

Both existing DYDO_AGENT tests pass under any implementation (including a PID-binding fix and the current buggy code) — their setup makes the assertions vacuously true and their names mislead a future maintainer into treating the env-var fast path as the intended invariant rather than the bug pivot. Needs rewrite alongside the F1 fix.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed at HEAD: the two mis-contracted tests were rewritten to assert the ownership-gated env-var contract (AgentRegistryTests.cs:2631 'Closes #0189', new OnlyTrustedWhenCallerOwnsAgent tests). Triage sweep 2026-07-04 (Brian, CoS).