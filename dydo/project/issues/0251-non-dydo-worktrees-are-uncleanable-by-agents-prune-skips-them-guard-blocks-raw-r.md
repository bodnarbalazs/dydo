---
title: non-dydo worktrees are uncleanable by agents - prune skips them, guard blocks raw removal
id: 251
area: project
type: issue
severity: low
status: open
found-by: manual
found-by-agent: Noah
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-09
---

# non-dydo worktrees are uncleanable by agents - prune skips them, guard blocks raw removal

A git worktree created outside dydo (no dydo marker) falls into a cleanup gap: `dydo worktree
prune` only sweeps marker-tracked worktrees, `dydo worktree cleanup` requires a worktree-id
that never existed, and the guard's dangerous-command rule blocks agents from raw
`git worktree remove` (pointing them at the dydo commands that can't handle the case). Result:
only a human hand can remove it.

## Description

Observed 2026-07-09: a foreign-vendor session created `C:/tmp/dydo-codex-identity-fix`
(branch `codex/fix-codex-identity`) directly via git. After the human authorized removal, the
agent's `git worktree remove` was guard-blocked, `dydo worktree prune` skipped the untracked
entry (while correctly sweeping 9 marker-tracked orphans), and the human had to run the
removal himself.

Expected to become more common as cross-vendor/MCP-delegated agents (see
`backlog/codex-mcp-delegation-experiment.md`, revision #4) create worktrees outside dydo's
marker system.

## Candidate direction

Teach `dydo worktree prune` to also list non-dydo worktrees (git worktree entries without
markers, excluding the main tree) and offer removal when the branch is fully merged and the
tree is clean — same safety checks a careful human performs. Alternatively a
`dydo worktree adopt`/`remove <path>` for explicit one-off cases.

## Reproduction

1. `git worktree add C:/tmp/foo -b some/branch` from any non-dydo process.
2. As an agent: `git worktree remove C:/tmp/foo` → guard-blocked; `dydo worktree prune` →
   skips it; `dydo worktree cleanup <id>` → no id exists.

## Resolution

(Filled when resolved)
