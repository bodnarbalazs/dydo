---
area: platform
name: watchdog-lifecycle-review
status: human-reviewed
created: 2026-04-20T19:23:06.0796673Z
assigned: Brian
updated: 2026-04-20T19:23:18.8101699Z
---

# Task: watchdog-lifecycle-review

Review Adele's zombied watchdog-lifecycle-fixes work on master. Her Claude died mid-verification; code is on disk but unverified end-to-end.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review Adele's on-disk watchdog-lifecycle-fixes work on master (no worktree). Context: she zombied mid-verification — code is on disk but she never ran the full suite or dispatched you. The original task 'watchdog-lifecycle-fixes' is still slot-blocked by her stale session, so you're reviewing under a fresh task name; the work being reviewed is the original scope. YOUR REVIEW IS THE SOURCE OF TRUTH. Scope: issues #95 (WatchdogService.cs spawns with caller's CWD), #96 (FindDydoRoot resolves to worktree), #97 (WatchdogService.Run has no cancellation/parent-PID/signal handling). Original brief at dydo/agents/Brian/brief-watchdog-lifecycle-fixes.md — READ FIRST. Inquisition context at dydo/project/inquisitions/stale-dydo-processes.md. Files Adele touched (all uncommitted on master): Services/WatchdogService.cs, Utils/PathUtils.Discovery.cs, DynaDocs.Tests/Services/WatchdogServiceTests.cs, plus NEW DynaDocs.Tests/Services/PathUtilsWorktreeIsolationTests.cs and DynaDocs.Tests/Services/WatchdogRunLivenessContractTests.cs. Verify all three issues are addressed with passing tests. Run: python DynaDocs.Tests/coverage/run_tests.py. Expect 2 cross-lane test-infra flakes (PathUtilsDiscoveryTests/WatchdogServiceTests CWD contention) — confirm unrelated on isolation; flag but do not fail the review on them. PASS → Brian commits and resolves #95/#96/#97. FAIL → specify WHAT's missing (partial vs wrong direction) so Brian can dispatch a code-writer for the delta. Do NOT modify Adele's files — you're reviewing, not fixing. Report back to Brian. Note: the review-complete auto-CC feature (Frank's work) is NOT yet in the installed binary, so address Brian explicitly.

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-20 19:38
- Result: PASSED
- Notes: All 3 issues fixed. #95 WorkingDirectory pinned to main project root. #96 FindMainProjectRoot/FindMainDydoRoot added + callsites switched. #97 Run() has CancellationTokenSource + ProcessExit/CancelKeyPress + anchor-PID liveness + finally-block pid-file cleanup. 3776/3776 tests pass. gap_check 100% (136/136). Cross-lane PathUtilsDiscoveryTests CWD flake observed on coverage run only, as Brian predicted.

Awaiting human approval.