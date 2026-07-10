---
title: Active orchestrator session released mid-sprint - found in free pool with CWD drifted into a worktree
id: 268
area: backend
type: issue
severity: medium
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-10
---

# Active orchestrator session released mid-sprint - found in free pool with CWD drifted into a worktree

During C1's audit-fix wave (2026-07-10), Grace's live orchestrator session was silently released mid-orchestration: the agent found itself in the free pool, CWD drifted into a workflow worktree, then re-claimed cleanly (role+task survived, general wait still live). Candidate causes: watchdog stale-working reclaim misfiring on a session whose liveness signal was disturbed (0130/0201 family), or a worktree sub-agent clobbering the shared session context (0230/0250 family - the CWD drift suggests worktree work leaked into the main session's context resolution). Single occurrence, recovered without loss - but a released-and-reclaimable orchestrator identity mid-sprint is a takeover window (anyone claiming the freed name inherits the sprint context) and a coordination hazard. Needs: watchdog reclaim log inspection for the episode, and a look at whether workflow worktree sub-agents can write .session-context. Reported by Grace via Adele.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)