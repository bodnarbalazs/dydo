---
area: general
name: fix-inbox-discoverability
status: human-reviewed
created: 2026-03-26T20:50:36.1497601Z
assigned: Brian
updated: 2026-03-26T22:22:39.8447627Z
---

# Task: fix-inbox-discoverability

Fix for inbox file path discoverability regression. Root cause: commit 8242fc9 added unread-before-clear checks but inbox show never displayed file paths, trapping agents in a loop. Changes: (1) InboxItem.cs — added FilePath property. (2) InboxItemParser.cs — sets FilePath from parsed file. (3) InboxService.cs — PrintInboxItem (now internal) prints File: line with CWD-relative path. (4) GuardCommand.cs — FindMessageInfo returns file path, NotifyUnreadMessages displays it. (5) Tests: 3 new parser tests, 3 new InboxService output tests, 1 updated FindMessageInfo test. All 3258 tests pass, coverage gate clear.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fix for inbox file path discoverability regression. Root cause: commit 8242fc9 added unread-before-clear checks but inbox show never displayed file paths, trapping agents in a loop. Changes: (1) InboxItem.cs — added FilePath property. (2) InboxItemParser.cs — sets FilePath from parsed file. (3) InboxService.cs — PrintInboxItem (now internal) prints File: line with CWD-relative path. (4) GuardCommand.cs — FindMessageInfo returns file path, NotifyUnreadMessages displays it. (5) Tests: 3 new parser tests, 3 new InboxService output tests, 1 updated FindMessageInfo test. All 3258 tests pass, coverage gate clear.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-26 22:27
- Result: PASSED
- Notes: LGTM. Clean, minimal fix for the discoverability regression. FilePath flows correctly from parser through to both inbox show and guard notification. Tests are thorough. All 3258 tests pass, coverage gate clear.

Awaiting human approval.