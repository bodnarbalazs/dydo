---
area: general
name: quick-fixes-89-115-116
status: human-reviewed
created: 2026-04-26T19:48:44.2070307Z
assigned: Frank
updated: 2026-04-26T20:19:52.6027285Z
---

# Task: quick-fixes-89-115-116

Review quick fixes for #0089, #0115, #0116. See git log master..HEAD (3 commits, one per issue). #0089: extracted BashSingleQuoteEscape helper, applied across LinuxTerminalLauncher cleanup cd, MacTerminalLauncher cleanup cd, and the three pre-existing call sites in TerminalLauncher.cs. #0115: IssueResolveHandler now prefers open dir; warns when both open+resolved share an ID. #0116: WatchdogServiceTests no longer captures Environment.CurrentDirectory; Dispose parks CWD on Path.GetTempPath(). Tests: full suite green (3803/3803), gap_check 100% pass on rerun. Two flakes observed under coverage instrumentation (InboxServiceTests.PrintInboxItem_TaskItem_IncludesFilePath, StaleDispatchDoubleClaimTests.ReserveAgent_StaleAndNoLauncher_Succeeds) — both pass in isolation and on rerun, neither touches the changed code; flagging as pre-existing flakes worth filing.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review quick fixes for #0089, #0115, #0116. See git log master..HEAD (3 commits, one per issue). #0089: extracted BashSingleQuoteEscape helper, applied across LinuxTerminalLauncher cleanup cd, MacTerminalLauncher cleanup cd, and the three pre-existing call sites in TerminalLauncher.cs. #0115: IssueResolveHandler now prefers open dir; warns when both open+resolved share an ID. #0116: WatchdogServiceTests no longer captures Environment.CurrentDirectory; Dispose parks CWD on Path.GetTempPath(). Tests: full suite green (3803/3803), gap_check 100% pass on rerun. Two flakes observed under coverage instrumentation (InboxServiceTests.PrintInboxItem_TaskItem_IncludesFilePath, StaleDispatchDoubleClaimTests.ReserveAgent_StaleAndNoLauncher_Succeeds) — both pass in isolation and on rerun, neither touches the changed code; flagging as pre-existing flakes worth filing.

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-26 20:31
- Result: PASSED
- Notes: PASS. #0089: BashSingleQuoteEscape extracted cleanly and applied at all 5 sites; helper tests + Linux/Mac integration tests cover apostrophe escape correctly. #0115: open-dir-wins logic flip is sound, warning fires when both share an ID, 'already resolved' only when open is genuinely absent; integration test asserts exact behavior. #0116: CWD-capture anti-pattern removed, Dispose parks on Path.GetTempPath() with justified try/catch for test cleanup; rationale comments cite issue. Tests: 3803/3803 clean (forced rerun), gap_check 136/136 modules at 100% on commit 3654ec6. Brief-flagged flakes (InboxServiceTests.PrintInboxItem_TaskItem_IncludesFilePath, StaleDispatchDoubleClaimTests.ReserveAgent_StaleAndNoLauncher_Succeeds) did not reproduce.

Awaiting human approval.