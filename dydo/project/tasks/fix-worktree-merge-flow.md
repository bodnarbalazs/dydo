---
area: general
name: fix-worktree-merge-flow
status: human-reviewed
created: 2026-03-20T12:15:21.0022022Z
assigned: Charlie
updated: 2026-03-20T12:52:29.4301902Z
---

# Task: fix-worktree-merge-flow

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Updated 3 agent mode templates to document the worktree merge flow. Code-writer template: added Worktree Merge section in Work that tells merge-dispatched agents to run 'dydo worktree merge' instead of git merge, and explains conflict resolution via --finalize. Reviewer template: added Worktree merge dispatch section after review-passes explaining the .needs-merge marker and the required merge dispatch before release. Orchestrator template: added how-merge-works paragraph to Merge Coordination explaining the end-to-end flow. No plan existed — followed the investigation brief. Key decision: docs-only change since the code (WorktreeCommand.cs, ReviewCommand.cs, DispatchService.cs) already implements the full merge flow correctly. NOTE: dydo/reference/dydo-commands.md needs a Worktree Commands section added (dydo worktree merge, cleanup, init-settings) but code-writer role cannot edit dydo/ docs — needs docs-writer dispatch.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-20 12:39
- Result: PASSED
- Notes: LGTM. All 3 template changes accurately document the worktree merge flow as implemented in WorktreeCommand.cs, ReviewCommand.cs, and DispatchService.cs. Every claim verified against source code. Tests pass (3 pre-existing failures unrelated to this change). Coverage gate failures are all pre-existing — docs-only change cannot regress coverage.

Awaiting human approval.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-20 12:56
- Result: PASSED
- Notes: LGTM. All 3 template changes accurately document the worktree merge flow. Every claim verified against WorktreeCommand.cs, ReviewCommand.cs, and DispatchService.cs. Tests pass (2 pre-existing failures unrelated). Coverage gate failures all pre-existing — docs-only change cannot regress coverage.

Awaiting human approval.