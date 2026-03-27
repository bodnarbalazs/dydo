---
area: general
type: changelog
date: 2026-03-27
---

# Task: fix-queue-defaults-merge

Merged worktree/fix-queue-defaults into master. Changes: atomic TryAcquireOrEnqueue replacing two-step TryEnqueue+SetActive, UpdateActivePid, WithQueueLock file locking, default 'merge' queue in ConfigFactory, EnsureDefaultQueues for template updates. All 3252 tests pass, coverage gate green.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Merged worktree/fix-queue-defaults into master. Changes: atomic TryAcquireOrEnqueue replacing two-step TryEnqueue+SetActive, UpdateActivePid, WithQueueLock file locking, default 'merge' queue in ConfigFactory, EnsureDefaultQueues for template updates. All 3252 tests pass, coverage gate green.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-26 20:16
- Result: PASSED
- Notes: LGTM. Code is clean, tests pass. Key findings: (1) TryAcquireOrEnqueue correctly replaces the racy two-step TryEnqueue+SetActive with file-lock-guarded atomicity; (2) PID=0 placeholder is safe — IsProcessRunning(0) returns false so watchdog cleans up stale entries; (3) Guard correctly expands git merge blocking to .merge-source marker on main; (4) cleanupWorktreeId fix in merge dispatch ensures proper worktree cleanup; (5) WithQueueLock has solid stale-lock detection via PID liveness check; (6) EnsureDefaultQueues follows established EnsureDefaultNudges pattern. All 3252 tests pass, 131/131 modules pass coverage gate.

Awaiting human approval.

## Approval

- Approved: 2026-03-27 13:14
