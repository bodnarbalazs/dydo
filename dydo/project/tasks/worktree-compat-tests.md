---
area: general
name: worktree-compat-tests
status: human-reviewed
created: 2026-03-20T13:38:24.3594950Z
assigned: Grace
updated: 2026-03-20T14:03:12.9519740Z
---

# Task: worktree-compat-tests

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Created DynaDocs.Tests/Commands/WorktreeCompatTests.cs with 28 tests covering: (1) Guard daily validation with missing _system/.local/ using EnsureLocalDirExists, (2) AgentRegistry.IsWorktreeStale with correct GetDydoRoot-based paths, (3-5) IsInsideWorktree for Init/Template/Workspace commands including CWD and explicit path detection with backslash support, (6) Agent lifecycle file operations through symlinked agents directory, (7-8) InboxService and MessageService read/write/archive through agents directory, (9) ConfigService.GetProjectRoot from worktree subdirectory, (10) NormalizeWorktreePath edge case for _system content inside worktree, and GetWorktreeId marker parsing. Items 10-12 from brief are already covered by Charlie's tests in PathUtilsTests.cs. No plan deviations.

## Code Review

- Reviewed by: Iris
- Date: 2026-03-20 14:07
- Result: PASSED
- Notes: LGTM. 30 tests all pass. Logic traced and verified against source. Coverage gate failures are pre-existing (test-only change). Minor: some overlap with AgentRegistryTests/PathUtilsTests, ThroughSymlink test names slightly misleading.

Awaiting human approval.