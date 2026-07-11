---
title: gap_check auto-skip watches only 6 source dirs - edits elsewhere (Sync/) silently reuse stale coverage
id: 241
area: backend
type: issue
severity: medium
status: resolved
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-08
resolved-date: 2026-07-11
---

# gap_check auto-skip watches only 6 source dirs - edits elsewhere (Sync/) silently reuse stale coverage

The gap_check staleness auto-skip only watches 6 source directories and does not include Sync/, so editing Sync/ (or other unwatched dirs) silently reuses stale coverage data instead of re-running. Workaround is --force-run. Routed from auto-memory per DR 038 initial sweep; supersedes memory gap-check-staleness-blind-spot. Note issue 0217 may already cover part of this - dedup on triage.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

DUPLICATE of #0217 (identical gap_check.py SOURCE_DIRS staleness blind spot; 0241 body itself said 'dedup on triage'). Fix tracked under 0217 (Wave-1 Batch 1).