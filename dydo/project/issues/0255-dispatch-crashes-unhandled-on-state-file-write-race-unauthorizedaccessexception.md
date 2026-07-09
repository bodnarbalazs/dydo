---
title: dispatch crashes unhandled on state-file write race - UnauthorizedAccessException in WriteStateFile leaves half-dispatched agent
id: 255
area: backend
type: issue
severity: medium
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-09
---

# dispatch crashes unhandled on state-file write race - UnauthorizedAccessException in WriteStateFile leaves half-dispatched agent

Live occurrence 2026-07-09 (v2.0.6): dydo dispatch wrote the inbox item, then crashed with an unhandled System.UnauthorizedAccessException at AgentRegistry.WriteStateFile (File.Move, line 1850) inside SetDispatchMetadata - likely a transient Windows file lock race on the target agent's state.md (watchdog poll or AV scan holding the file). Consequences: no terminal launched, no dispatch metadata (windowId/autoClose) recorded, agent stranded in Dispatched with a live inbox item - same stranded shape as a launch failure but caused by dydo itself, and the raw stack trace surfaces to the caller. Fix directions: retry-with-backoff on the state-file move (the FileReadRetry pattern exists for reads - issue 0119 family), and a catch that rolls the dispatch back (or completes it degraded without auto-close metadata) instead of dying between inbox-write and launch. Candidate for C1 or H1.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)