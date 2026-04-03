---
area: general
name: fix-inquisition-issues-dead-code
status: human-reviewed
created: 2026-04-03T14:31:30.7220252Z
assigned: Grace
---

# Task: fix-inquisition-issues-dead-code

Dead code cleanup and parameter refactoring for three inquisition issues. (1) Deleted MarkerStore.cs and MarkerStoreTests.cs — zero production usage, all marker logic already in AgentRegistry. (2) Removed QueueService.TryEnqueue — dead in production, superseded by TryAcquireOrEnqueue. Migrated 10+ test call sites to use TryAcquireOrEnqueue. (3) Extracted DispatchOptions record from 13-param Execute method. DispatchService.Execute and WriteAndLaunch now accept a single DispatchOptions object. Updated DispatchCommand call site. All 45 affected tests pass. Two pre-existing gap check failures noted (DispatchService CRAP 30.5 borderline, FileReadRetry line coverage).

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Dead code cleanup and parameter refactoring for three inquisition issues. (1) Deleted MarkerStore.cs and MarkerStoreTests.cs — zero production usage, all marker logic already in AgentRegistry. (2) Removed QueueService.TryEnqueue — dead in production, superseded by TryAcquireOrEnqueue. Migrated 10+ test call sites to use TryAcquireOrEnqueue. (3) Extracted DispatchOptions record from 13-param Execute method. DispatchService.Execute and WriteAndLaunch now accept a single DispatchOptions object. Updated DispatchCommand call site. All 45 affected tests pass. Two pre-existing gap check failures noted (DispatchService CRAP 30.5 borderline, FileReadRetry line coverage).

## Code Review

- Reviewed by: Emma
- Date: 2026-04-03 15:08
- Result: PASSED
- Notes: LGTM. All three inquisition issues addressed cleanly. FrontmatterParser, FileReadRetry, FileLock extractions are well-motivated Rule of Three consolidations with comprehensive tests. DispatchOptions record simplifies 13-param method. Bonus fixes (MessageFinder ordering, watchdog shell-process tracking, junction-safe rmdir, worktree read allow, process timeouts, judge inquisitions permission) are all correct and tested. 3455/3456 tests pass (1 pre-existing template sync failure). gap_check: 2 failures are not regressions (DispatchService CRAP improved 30.5->30.2; FileReadRetry is extracted code with pre-existing gap).

Awaiting human approval.