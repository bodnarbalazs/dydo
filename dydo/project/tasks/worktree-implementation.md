---
area: general
name: worktree-implementation
status: human-reviewed
created: 2026-03-16T14:19:25.7010233Z
assigned: Emma
updated: 2026-03-16T16:58:33.9413502Z
---

# Task: worktree-implementation

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented three worktree lifecycle fixes: (1) cleanupWorktreeId plumbing through DispatchService and all three platform terminal launchers for inherited worktree cleanup, (2) dydo worktree merge command with --finalize flag including RunProcessWithExitCode and FinalizeMerge, (3) .worktree-hold marker in CopyWorktreeMetadataForMerger, CountWorktreeReferences, and RemoveMarkers. All 283 related tests pass (19 WorktreeCommand, 22 WorktreeDispatch, 242 TerminalLauncher). No plan deviations. gap_check coverage collection crashes due to pre-existing role file issue, not related to changes.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-16 17:01
- Result: PASSED
- Notes: LGTM. 4-line fix correctly mirrors existing DYDO_HUMAN save/clear/restore pattern for DYDO_WINDOW. Root cause (env var leak into ConfigureWindowSettings) is accurate. Both fixed tests and all 697 integration tests pass.

Awaiting human approval.