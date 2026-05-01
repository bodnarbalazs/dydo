---
id: 71
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-26
---

# O(n^2) cumulative I/O from session file rewrites on every guard event

Resolved medium-severity perf bug: each guard event triggered a full rewrite of the session JSON file, producing O(n²) cumulative I/O over a session. Fixed in commit `99a9a33` by introducing an append-only sidecar (`{sessionId}.events`, single-line JSON per event, O(1) per append); the in-memory cache path is bounded to one live session per process.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Cross-process append-only sidecar at Services/AuditService.cs:60-66 plus AppendEventToSidecar :184-188 — single-line JSON append per event to {sessionId}.events, O(1). Same-process session-cache path (:47-58) does in-memory rewrites bounded to one live session per process. Fix commit 99a9a33. Verified by Charlie.