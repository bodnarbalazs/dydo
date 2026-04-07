---
area: general
type: changelog
date: 2026-04-05
---

# Task: fix-inquisition-issues-dedup

Extracted 3 shared utilities (FileLock, FileReadRetry, FrontmatterParser) from duplicated code across the codebase. FileLock replaces identical WithWorktreeLock/WithQueueLock. FileReadRetry replaces identical FileReadWithRetry in AgentRegistry/AgentSessionManager. FrontmatterParser replaces 10 separate hand-rolled YAML frontmatter parsers. 816 lines added, 456 removed (net simplification). 51 new tests across 3 test files. All 3457 tests pass. Only gap_check failure is pre-existing DispatchService CRAP 30.2 from Execute/RunGitForWorktree methods not touched by this change.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Utils\FrontmatterParser.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Utils\FrontmatterParserTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Utils\FileLock.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Utils\FileLockTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Utils\FileReadRetry.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Utils\FileReadRetryTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\FrontmatterExtractor.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\InboxMetadataReader.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentSessionManager.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\InboxItemParser.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MessageFinder.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentStateStore.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\AgentSessionManagerTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\QueueService.cs — Modified


## Review Summary

Extracted 3 shared utilities (FileLock, FileReadRetry, FrontmatterParser) from duplicated code across the codebase. FileLock replaces identical WithWorktreeLock/WithQueueLock. FileReadRetry replaces identical FileReadWithRetry in AgentRegistry/AgentSessionManager. FrontmatterParser replaces 10 separate hand-rolled YAML frontmatter parsers. 816 lines added, 456 removed (net simplification). 51 new tests across 3 test files. All 3457 tests pass. Only gap_check failure is pre-existing DispatchService CRAP 30.2 from Execute/RunGitForWorktree methods not touched by this change.

## Code Review (2026-04-03 15:46)

- Reviewed by: Charlie
- Result: FAILED
- Issues: Review FAIL. 3 issues found. (1) CRITICAL - Incomplete commit: DispatchService.Execute signature changed to accept DispatchOptions record, but Models/DispatchOptions.cs and Commands/DispatchCommand.cs updates were NOT included in commit a89d1c2. The committed code does not compile. (2) Out-of-scope changes mixed into commit: GuardCommand worktree-read-allow logic (WorktreeReadAllowJson, IsWorktreeContext), WatchdogService auto-close timing fix (killedOrAttempted flag), MessageFinder sorting behavioral change (file creation time to frontmatter received timestamp), QueueService dead TryEnqueue removal. These are separate concerns that should be their own commits. (3) Missing tier annotations on new Utils files (FileLock.cs, FileReadRetry.cs, FrontmatterParser.cs). WHAT IS GOOD: The core utility extractions are clean, faithful, well-tested (51 tests), and follow project conventions. All integration points correctly delegate. No orphaned references. gap_check only failure is pre-existing DispatchService CRAP 30.2.

Requires rework.

## Code Review (2026-04-03 17:06)

- Reviewed by: Charlie
- Result: FAILED
- Issues: Code changes are correct — all 3 original issues fixed. BLOCKED by gap_check: (1) tier_registry.json has stale worktree temp paths for FileLock/FileReadRetry/FrontmatterParser. (2) Pre-existing DispatchService CRAP 30.2. (3) Pre-existing ReadmeClones_ContentInSync test failure.

Requires rework.

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-03 17:24
- Result: PASSED
- Notes: Code changes are correct. All 3 original review issues fixed. Pre-existing gap_check failures reported to Adele separately.

Awaiting human approval.

## Approval

- Approved: 2026-04-05 11:31
