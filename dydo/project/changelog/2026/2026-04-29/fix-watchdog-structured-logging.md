---
area: general
type: changelog
date: 2026-04-29
---

# Task: fix-watchdog-structured-logging

# Brief: recover-fix-watchdog-structured-logging (#0129)

You are a code-writer recovering an in-flight task. Today is 2026-04-29.

## Situation

Jack was dispatched on `fix-watchdog-structured-logging` at 15:31 UTC. His terminal died at ~15:35 UTC after ~4 minutes — almost certainly the watchdog-deaths issue (#0121/#0122) that this same long-tail batch is fixing. His WIP exists uncommitted in the working tree and is mostly complete; you're picking it up from where he left off.

## What Jack already did (read these in your working tree, don't redo)

- **`Services/WatchdogLogger.cs`** — NEW FILE, 152 lines. Untracked (`?? Services/WatchdogLogger.cs`). Contains: `WatchdogLogger` static class with `Write<T>` + `RotateIfNeeded` + 6 public `Log*` methods, source-gen JSON via `WatchdogLogJsonContext`, all 6 event records (`StartEvent`, `TickEvent`, `KillEvent`, `KillState`, `ParseFailureEvent`, `PollErrorEvent`, `ExitEvent`). Matches Grace's plan exactly.
- **`Services/WatchdogService.cs`** — MODIFIED, +44 lines (visible via `git diff Services/WatchdogService.cs`). Contains: `LogStart` after anchor resolution, `exitReason` tracking + `LogExit` in finally, `LogPollError` replacing the swallowed catch, agent-count + kills-attempted counter + `LogTick` in `PollAndCleanup`, `LogKill` per kill, `LogParseFailure` in both null-fields and exception branches of `ParseStateForWatchdog`, plus internal helpers `ReadStateContext` and `GetDydoRootForLog`. Matches Grace's plan.

**Read both before doing anything else.** Confirm they match Grace's plan at `dydo/agents/Grace/plan-watchdog-structured-logging.md` (Steps 1–4 of "Implementation Steps"). If anything diverges, surface it to Brian — don't quietly fix.

## What's left (your scope)

1. **Build the project**: `dotnet build`. It must pass with zero warnings (especially zero AOT trim warnings — the JSON source-gen pattern is the precedent at `RegistryLockJsonContext` in `Services/AgentRegistry.cs:1971`). If Jack's `WatchdogService.cs` has incomplete edits (e.g., a hanging method) the build will tell you.
2. **Write the 9 tests** per Grace's plan § "Tests to Add". Each test:
   - Sets `WatchdogLogger.LogPathOverride = Path.Combine(_testDir, "watchdog.log")` in setup.
   - Resets `LogPathOverride`, `MaxBytesOverride`, `MaxRotationsOverride` to null in `Dispose()`.
   - Test names: `Logger_StartEvent_RecordedWithAnchorPid`, `Logger_KillEvent_RecordsTargetPidAndPattern`, `Logger_ParseFailure_RecordedOnMalformedState`, `Logger_Rotation_TriggersAtThreshold_KeepsThreeBackups`, `Logger_ExitEvent_AnchorGoneReason`, `Logger_ExitEvent_CancelledReason`, `Logger_TickEvent_SkipsIdleNoAgents`, `Logger_NeverThrows_OnInvalidPath`, `Logger_PollError_RecordedOnInnerException`.
   - File: `DynaDocs.Tests/Services/WatchdogServiceTests.cs` (or a new sibling `WatchdogLoggerTests.cs` if Brian's pattern review prefers).
3. **Run the full test suite**: `dotnet test`. Must be 3888/3888 (or higher, with your additions).
4. **Commit** as a single commit: `feat(watchdog): structured JSONL event log with rotation (#0129)`. Body should explain event types, file path, rotation policy (2 MB × 3 backups), idle-tick suppression, and the never-throws contract. Stage `Services/WatchdogLogger.cs`, `Services/WatchdogService.cs`, and the test file.
5. **Dispatch the reviewer YOURSELF** before release (the guard requires it — Iris and Jack's protocol confusion already cost us cycles):

```bash
dydo dispatch --no-wait --auto-close --role reviewer --task fix-watchdog-structured-logging --brief "Review commit <hash> for fix-watchdog-structured-logging (#0129). Plan: dydo/agents/Grace/plan-watchdog-structured-logging.md. Brief (recovery): dydo/agents/Brian/brief-recover-fix-watchdog-structured-logging.md. New file Services/WatchdogLogger.cs + insertions in WatchdogService.cs + 9 tests. Verify event shapes, idle-tick suppression, rotation policy, never-throws contract, no AOT trim warnings. Approve or reject. NB: gap_check may flag pre-existing CRAP on WatchdogService.cs from 06512de — that's unrelated to this commit, waive with notes."
```

6. **Then** message Brian and release:

```bash
dydo msg --to Brian --subject fix-watchdog-structured-logging --body "Recovered from Jack's WIP. Done. Commit: <hash>. Tests: <pass/total>. <one-line note>."
dydo inbox clear --all
dydo agent release
```

## Q-resolutions still in force (decided by user; don't redebate)

- **Q7** — Real per-PID command line in kill events? **No, pattern only**. Don't add `ProcessUtils.GetCommandLine`.
- **Q8** — `version: 1` field in events? **No.**

## Hard constraints

- **No worktrees.** Plain working tree.
- **Don't re-create what Jack already did.** If `Services/WatchdogLogger.cs` exists, read it. If it deviates from plan, surface to Brian rather than silently rewriting.
- **Don't touch the anchor-hardening surface** (`EnsureRunning` env var, `FindAncestorProcess`) — that's Kate's task on `fix-watchdog-anchor-hardening`.
- **No `git --no-verify`, no `git push`.**
- If Jack's WIP is unsalvageable (e.g., builds break in an unfixable way), `git checkout Services/WatchdogService.cs && rm Services/WatchdogLogger.cs` and start clean from Grace's plan. Surface to Brian first.

## Reference plan

`dydo/agents/Grace/plan-watchdog-structured-logging.md` — the source of truth for the contract.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit 3532bd9 for fix-watchdog-structured-logging (#0129). Plan: dydo/agents/Grace/plan-watchdog-structured-logging.md. Brief (recovery): inbox d97b0545. New file Services/WatchdogLogger.cs (152 lines, source-gen JSON via WatchdogLogJsonContext) + insertions in WatchdogService.cs (LogStart/LogExit/LogPollError/LogTick/LogKill/LogParseFailure call sites + ReadStateContext + GetDydoRootForLog helpers) + 9 tests in DynaDocs.Tests/Services/WatchdogServiceTests.cs region 'Structured Logging (#0129)'. Verify event shapes match plan, idle-tick suppression (no tick when 0 agents and 0 kills), rotation policy (2 MB x 3 backups), never-throws contract, AOT trim warnings (zero — confirmed by clean build). Tests: 3897/3897 pass. Two deviations to flag: (1) Jack also wrote the 9 tests (Brian's brief listed only the two production files); names match Grace's plan exactly. (2) ReadStateContext at WatchdogService.cs:324 omits the inline try/catch Grace's plan specified — relies on the outer Run() poll_error catch. Functional but plan-divergent; surface judgment call. Approve or reject. NB: gap_check fails with pre-existing CRAP 30.3 on WatchdogService.cs from commit 06512de — Brian's brief authorized waiving with notes.

## Code Review (2026-04-29 14:56)

- Reviewed by: Charlie
- Result: FAILED
- Issues: FAIL. (1) gap_check red: WatchdogService.cs CRAP 30.3 (T1<=30); reviewer mode forbids waiving pre-existing failures. (2) Services/WatchdogLogger.cs declares 9 top-level types - violates One-Type-Per-File rule (cited precedent in AgentRegistry.cs nests its records, this file does not). (3) ReadStateContext at WatchdogService.cs:324 is missing the inline try/catch from Grace's plan Step 4 - an IO exception will abort the entire poll cycle, skip all later agents, and lose the kill log line; not a 'judgment call'. (4) Minor: outer exit-reason catch from plan Step 3 omitted (defensive, not breaking today). Tests 3897/3897 pass. Full notes: dydo/agents/Charlie/review-notes.md. Item 1 needs Brian/orchestrator guidance (cannot waive); 2-4 are code-writer fixes.

Requires rework.

## Approval

- Approved: 2026-04-29 16:51
