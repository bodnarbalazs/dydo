---
title: Workflow worktrees cut from last-pushed commit, not local master - stale base under hold-commit accumulation
id: 266
area: backend
type: issue
severity: medium
status: open
found-by: manual
found-by-agent: Grace
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-09
---

# Workflow worktrees cut from last-pushed commit, not local master - stale base under hold-commit accumulation

Every worktree cut during the C1 sprint (run-sprint workflow slices AND a direct worktree-isolated
subagent) was based at `d88102b3` ("2.0.6") — the last pushed commit — while local master had
accumulated 14+ unpushed commits (plan records, merged slices). Consequence: wave-1 workers
implemented against a base missing the sprint's own plan files and sibling seams (c1-4 shipped
blind seams; c1-1 could not flip its row); wave-3 workers hit their base-check tripwire and
stopped cleanly, costing a full round.

## Working hypothesis

The worktree mechanism bases new worktrees on the remote-tracking state (origin/master or a
pinned release ref), not the local branch HEAD. Under the project's hold-commits-until-release
posture, local master is routinely ahead, so every workflow worktree starts stale by exactly the
unpushed accumulation.

## Mitigation in use (C1, works)

Slice briefs carry: STEP 0 — `git merge master --no-edit` in the worktree (fast-forwards the
stale base onto local master), then a BASE CHECK naming concrete predecessor artifacts, with a
STOP-and-raise-hand instruction if the check still fails.

## Fix directions

- Determine the actual base-selection rule (harness behavior, not dydo code) and document it in
  guides/orchestration-pitfalls.md.
- If configurable, pin workflow worktree bases to local branch HEAD.
- Until then, the STEP-0 mitigation belongs in the run-sprint skill/brief template so every
  orchestrator inherits it.

## Reproduction

With local master N commits ahead of origin, launch any run-sprint slice; inspect the worktree
HEAD — it sits at the last pushed commit.

## Resolution

(Filled when resolved)
