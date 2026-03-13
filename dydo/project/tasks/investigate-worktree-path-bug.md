---
area: general
name: investigate-worktree-path-bug
status: human-reviewed
created: 2026-03-13T14:21:41.2903295Z
assigned: Adele
---

# Task: investigate-worktree-path-bug

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

---
area: general
name: investigate-worktree-path-bug
status: human-reviewed
created: 2026-03-13T14:21:41.2903295Z
assigned: Adele
---

# Task: investigate-worktree-path-bug

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review fix round 2. Addressed both issues: (1) Out-of-scope WatchdogService/WatchdogCommand changes restored to working tree but excluded from this task — only the 3 in-scope files (TerminalLauncher.cs, WindowsTerminalLauncher.cs, TerminalLauncherTests.cs) belong to this task. Note: HEAD has a pre-existing break where committed WatchdogServiceTests reference APIs not in committed WatchdogService; the working tree watchdog changes fix this but are a separate concern. (2) Task file fix written to dydo/agents/Charlie/workspace/fixed-task-file.md — guard blocks code-writer from editing dydo/project/tasks/ directly; reviewer or human should copy it over. All 249 tests pass, 0 build errors.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-13 14:50
- Result: PASSED
- Notes: PASS. Round 2 addresses both prior issues. Path fix correct (dydo/ prefix on all worktree paths, cd depth 5). Symlink/junction handling correct: Unix ln -s with rm -f cleanup, Windows NTFS junction with cmd /c rmdir cleanup (avoids Remove-Item following junction). Cleanup ordering verified by tests. 226 TerminalLauncher tests pass. Note: task file still has duplicate frontmatter — human should fix manually.

Awaiting human approval.