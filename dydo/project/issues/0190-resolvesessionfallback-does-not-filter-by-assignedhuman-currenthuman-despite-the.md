---
title: ResolveSessionFallback does not filter by AssignedHuman == currentHuman despite the April-09 plan
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

## Attempted + deferred (2026-07-12)

Swarm attempt (Batch 2): the production fix — filter `ResolveSessionFallback`'s Working-agent scan by `AssignedHuman == DYDO_HUMAN`, return null when DYDO_HUMAN unset — is CORRECT and Claude-reviewed (production callers always have DYDO_HUMAN, so a real fallback resolves). But it broke a broad set of TEST fixtures that assume the old no-filter behavior: they don't set DYDO_HUMAN and/or use a legacy single-line `.session-context` (reads null post-#0196), so GetSessionContext falls through to the now-null fallback and prints "No agent identity assigned." REVERTED to keep Batch 2 landable. Re-do as a focused slice with a test-fixture migration: set DYDO_HUMAN (or update to the two-line `.session-context` shape) across the affected Guard/Inbox/Integration suites, then re-land the (correct) one-line production change. Test-only fallout, not a production defect.

## Resolution

(Filled when resolved)