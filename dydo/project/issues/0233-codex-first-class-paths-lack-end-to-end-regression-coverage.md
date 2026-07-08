---
title: Codex first-class paths lack end-to-end regression coverage
id: 233
area: backend
type: issue
severity: medium
status: in-flight
found-by: inquisition
found-by-agent: Adele
found-by-vendor: unknown
found-by-model: unknown
date: 2026-07-07
---

# Codex first-class paths lack end-to-end regression coverage

Current tests pass while Codex claim hooks, watchdog anchoring, legacy wait ownership, and skill-only sync paths remain weakly covered.

## Description

Inquisition coverage finding: The suite has useful host/model unit and integration checks, but several Codex first-class paths are only indirectly covered. Missing tests: feed a Codex-shaped guard stdin payload for dydo agent claim auto and assert host/model survive into pending/claimed session; dispatch/ensure watchdog from a Codex-owned session and assert a Codex host anchor is registered; write a legacy Codex .session with null ClaimedPid and assert WaitCommand.ResolveHostLivenessPid uses Codex ancestry; assert sync emits skill-only artifacts for planner/orchestrator/co-thinker/chief-of-staff without accidental .codex/agents role files. Consequence: green tests can miss Codex host/model regressions and workflow artifact drift.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)
