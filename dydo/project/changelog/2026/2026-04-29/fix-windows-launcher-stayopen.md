---
area: general
type: changelog
date: 2026-04-29
---

# Task: fix-windows-launcher-stayopen

Review commit e1eac2e for fix-windows-launcher-stayopen (#0124). Implements Dexter's plan: WindowsTerminalLauncher.GetArguments now unconditionally passes -NoExit; postClaudeCheck unchanged so the free-path 'exit 0' still closes the terminal, while non-free exits (claude crash, /exit, watchdog kill, context limit) keep the terminal open with output visible. Tests: 2 inverted (_OmitsNoExit -> _RetainsNoExit_StaysOpenOnNonFreeExit; worktree+autoClose flipped) + 2 added (_FreePathExitsZero, _NotFreePathStaysOpenViaNoExit). Full dotnet test 3885/3885 green; build clean 0 warnings; sanity check on PS -NoExit + explicit exit 0 verified locally. Files touched: Services/WindowsTerminalLauncher.cs, DynaDocs.Tests/Services/TerminalLauncherTests.cs. Pre-existing dirty AgentRegistry/AgentLifecycle changes were intentionally NOT staged (out of scope per Brian's brief). Plan: dydo/agents/Dexter/plan-windows-launcher-stayopen.md.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit e1eac2e for fix-windows-launcher-stayopen (#0124). Implements Dexter's plan: WindowsTerminalLauncher.GetArguments now unconditionally passes -NoExit; postClaudeCheck unchanged so the free-path 'exit 0' still closes the terminal, while non-free exits (claude crash, /exit, watchdog kill, context limit) keep the terminal open with output visible. Tests: 2 inverted (_OmitsNoExit -> _RetainsNoExit_StaysOpenOnNonFreeExit; worktree+autoClose flipped) + 2 added (_FreePathExitsZero, _NotFreePathStaysOpenViaNoExit). Full dotnet test 3885/3885 green; build clean 0 warnings; sanity check on PS -NoExit + explicit exit 0 verified locally. Files touched: Services/WindowsTerminalLauncher.cs, DynaDocs.Tests/Services/TerminalLauncherTests.cs. Pre-existing dirty AgentRegistry/AgentLifecycle changes were intentionally NOT staged (out of scope per Brian's brief). Plan: dydo/agents/Dexter/plan-windows-launcher-stayopen.md.

## Code Review

- Reviewed by: Dexter
- Date: 2026-04-29 13:55
- Result: PASSED
- Notes: PASS. Commit e1eac2e cleanly implements Dexter's plan: WindowsTerminalLauncher.GetArguments now unconditionally sets -NoExit (Services/WindowsTerminalLauncher.cs:18-23), with the existing postClaudeCheck ternary unchanged so the free-path 'exit 0' still terminates the host while non-free exits (claude crash, /exit, watchdog kill, context limit) keep the terminal open with output visible. All four call sites pick up the new noExitFlag by interpolation - no other edits needed. Tests match the plan exactly: 1 inverted (_AutoClose_OmitsNoExit -> _RetainsNoExit_StaysOpenOnNonFreeExit), 1 inverted assertion (_Worktree_CombinesWithAutoClose), 2 added (_FreePathExitsZero, _NotFreePathStaysOpenViaNoExit). Diff scope: only Services/WindowsTerminalLauncher.cs (+7) and DynaDocs.Tests/Services/TerminalLauncherTests.cs (+34/-4); pre-existing dirty AgentRegistry/AgentLifecycle correctly NOT staged. Worktree-isolated test run: 3888/3888 passed in 3m42s, build clean 0 warnings.

WAIVED gap_check failures (per orchestrator Brian, both confirmed unrelated to e1eac2e): (1) Services/WatchdogLogger.cs (line 76.6%, branch 50.0%) - untracked working-tree from Jack's in-flight fix-watchdog-structured-logging (#0129), not part of any commit including this one. (2) Services/WatchdogService.cs CRAP 30.1 - pre-existing from 06512de (per-agent lock fix); Frank's upcoming fix-watchdog-anchor-hardening will rework that surface. Brian explicitly authorised pass-with-notes; review held first, guidance received via msg ab0ab709 before completion.

Awaiting human approval.

## Approval

- Approved: 2026-04-29 16:51
