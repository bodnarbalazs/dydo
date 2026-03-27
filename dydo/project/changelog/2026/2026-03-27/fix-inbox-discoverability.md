---
area: general
type: changelog
date: 2026-03-27
---

# Task: fix-inbox-discoverability

Fix for inbox file path discoverability regression. Root cause: commit 8242fc9 added unread-before-clear checks but inbox show never displayed file paths, trapping agents in a loop. Changes: (1) InboxItem.cs — added FilePath property. (2) InboxItemParser.cs — sets FilePath from parsed file. (3) InboxService.cs — PrintInboxItem (now internal) prints File: line with CWD-relative path. (4) GuardCommand.cs — FindMessageInfo returns file path, NotifyUnreadMessages displays it. (5) Tests: 3 new parser tests, 3 new InboxService output tests, 1 updated FindMessageInfo test. All 3258 tests pass, coverage gate clear.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\InboxServiceTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Models\InboxItem.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\InboxItemParser.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\InboxService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\InboxItemParserTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCompatTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\InitCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\InitCommand.cs — Modified


## Review Summary

Fix for inbox file path discoverability regression. Root cause: commit 8242fc9 added unread-before-clear checks but inbox show never displayed file paths, trapping agents in a loop. Changes: (1) InboxItem.cs — added FilePath property. (2) InboxItemParser.cs — sets FilePath from parsed file. (3) InboxService.cs — PrintInboxItem (now internal) prints File: line with CWD-relative path. (4) GuardCommand.cs — FindMessageInfo returns file path, NotifyUnreadMessages displays it. (5) Tests: 3 new parser tests, 3 new InboxService output tests, 1 updated FindMessageInfo test. All 3258 tests pass, coverage gate clear.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-26 22:27
- Result: PASSED
- Notes: LGTM. Clean, minimal fix for the discoverability regression. FilePath flows correctly from parser through to both inbox show and guard notification. Tests are thorough. All 3258 tests pass, coverage gate clear.

Awaiting human approval.

## Approval

- Approved: 2026-03-27 13:14
