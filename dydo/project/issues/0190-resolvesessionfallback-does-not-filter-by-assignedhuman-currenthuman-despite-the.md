---
id: 190
area: backend
type: issue
severity: low
status: open
found-by: inquisition
date: 2026-05-19
---

# ResolveSessionFallback does not filter by AssignedHuman == currentHuman despite the April-09 plan

AgentSessionManager.ResolveSessionFallback scans all agents for the single Working status match with no human filter — in a multi-human project it can return another human's session id when there is exactly one Working agent system-wide. Single-human projects are bounded today but the plan explicitly called for the filter. Pre-existing latent bug, separate from the round-2 hijack.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)