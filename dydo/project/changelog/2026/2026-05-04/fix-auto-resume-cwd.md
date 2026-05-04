---
area: general
type: changelog
date: 2026-05-04
---

# Task: fix-auto-resume-cwd

Review commit 473af47 for fix-auto-resume-cwd (#0138). Brief: dydo/agents/Brian/brief-fix-auto-resume-cwd.md (also at dydo/agents/Wendy/inbox/archive/e94266b0-fix-auto-resume-cwd.md). One-line fix in Services/WatchdogService.cs: auto-resume now threads workingDirectory through to LaunchResumeTerminal so the resumed terminal lands in the project root (or worktree) instead of $HOME. Resolution choice: workingDirectory comes from .worktree-path if present and the path exists, otherwise projectRoot — see new ResolveResumeWorkingDirectory(). Verify (1) the threading actually reaches Process.Start across all 3 platform launchers (it does — every per-platform GetResumeArguments/LaunchResume already accepted workingDirectory pre-commit; only the watchdog call site was missing it); (2) the worktree handling matches dispatcher's marker convention (DispatchService writes .worktree-path on dispatch); (3) the 3 new tests in DynaDocs.Tests/Services/WatchdogServiceTests.cs (LaunchesWithProjectRootAsWorkingDirectory, WorktreeAgent_LaunchesInWorktreeDirectory, StaleWorktreeMarker_FallsBackToProjectRoot). LaunchResumeOverride signature changed to Func<string,string,string?,int> (the brief permitted this); existing 6 watchdog tests updated to match. README addendum (crash-recovery bullet) Brian asked for is OUT OF SCOPE for this commit per Brian's follow-up message — separate dispatch. Approve or reject.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit 473af47 for fix-auto-resume-cwd (#0138). Brief: dydo/agents/Brian/brief-fix-auto-resume-cwd.md (also at dydo/agents/Wendy/inbox/archive/e94266b0-fix-auto-resume-cwd.md). One-line fix in Services/WatchdogService.cs: auto-resume now threads workingDirectory through to LaunchResumeTerminal so the resumed terminal lands in the project root (or worktree) instead of $HOME. Resolution choice: workingDirectory comes from .worktree-path if present and the path exists, otherwise projectRoot — see new ResolveResumeWorkingDirectory(). Verify (1) the threading actually reaches Process.Start across all 3 platform launchers (it does — every per-platform GetResumeArguments/LaunchResume already accepted workingDirectory pre-commit; only the watchdog call site was missing it); (2) the worktree handling matches dispatcher's marker convention (DispatchService writes .worktree-path on dispatch); (3) the 3 new tests in DynaDocs.Tests/Services/WatchdogServiceTests.cs (LaunchesWithProjectRootAsWorkingDirectory, WorktreeAgent_LaunchesInWorktreeDirectory, StaleWorktreeMarker_FallsBackToProjectRoot). LaunchResumeOverride signature changed to Func<string,string,string?,int> (the brief permitted this); existing 6 watchdog tests updated to match. README addendum (crash-recovery bullet) Brian asked for is OUT OF SCOPE for this commit per Brian's follow-up message — separate dispatch. Approve or reject.

## Code Review

- Reviewed by: Adele
- Date: 2026-04-30 19:25
- Result: PASSED
- Notes: 473af47 is clean. WatchdogService threads workingDirectory via new ResolveResumeWorkingDirectory(.worktree-path -> projectRoot fallback, with stale-marker dir check). All 3 platform launchers (Win/Linux/Mac) thread it end-to-end pre-existing. 3 new tests + 6 existing override-signature updates verified. gap_check 137/137 PASS. Two unrelated test failures on master flagged separately to Brian (InboxServiceTests.PrintInboxItem_MessageItem_IncludesFilePath, StaleDispatchDoubleClaimTests.ReserveAgent_StaleAndNoLauncher_Succeeds) - neither touches code modified by 473af47.

Awaiting human approval.

## Approval

- Approved: 2026-05-04 21:51
