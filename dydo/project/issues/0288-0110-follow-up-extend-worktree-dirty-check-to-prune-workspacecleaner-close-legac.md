---
title: 0110 follow-up: extend worktree dirty-check to prune/WorkspaceCleaner, close legacy-marker skip, handle 0-ahead no-op-task release wedge
id: 288
area: backend
type: issue
severity: low
status: open
found-by: review
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-12
---

# 0110 follow-up: extend worktree dirty-check to prune/WorkspaceCleaner, close legacy-marker skip, handle 0-ahead no-op-task release wedge

Out-of-scope residuals from resolved 0110: prune/WorkspaceCleaner lack the dirty-check that cleanup now has; legacy markers skip the safety check; a legitimately-0-ahead worktree can't release with misleading guidance.

## Description

Follow-up to resolved #0110 (requires-commit release gate + worktree cleanup safety, shipped in 2.0.12). The 0110 review confirmed the fix's mechanics are sound and closes the silent-data-loss path, but flagged several out-of-scope residuals worth tracking:

1. **prune / WorkspaceCleaner have no dirty-check.** `dydo worktree prune` and `WorkspaceCleaner` still remove zero-ref worktrees/markers without checking for pending changes. 0110 hardened `worktree cleanup` (the release path), but these other removal paths (human-invoked) remain destructive. Consider extending the same "refuse-if-dirty-without-force" guard to them.

2. **Legacy-marker skip.** When the cleaning agent's workspace lacks `.worktree-base` (legacy markers), `CheckCleanupSafety` is skipped entirely, so a hand-deleted base marker resurrects the old destructive behavior. Narrow corner (current dispatch always writes `.worktree-base`), but worth closing.

3. **0-ahead no-op-task wedge.** A code-writer whose worktree legitimately ends 0 commits ahead (a genuine no-op task) cannot release at all — the requires-commit gate blocks with no override short of `git commit --allow-empty`, and the error message doesn't mention that escape. Rare but a hard wedge with misleading guidance. Consider an `--allow-empty`-equivalent release path or mentioning it in the error.

4. **Mildly over-blocking cleanup.** An agent that is NOT the last reference to a shared worktree is still refused a plain marker-drop when the worktree is dirty (the safety check runs before the refs check). Safe direction, but over-blocks a benign case.

All low/medium, none reintroduce data loss. Deployment note (existing installs need `dydo roles reset` to arm the gate) is tracked under #0286. Surfaced during the Wave-1 swarm (2026-07-12).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)