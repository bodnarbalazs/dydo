---
area: general
type: changelog
date: 2026-03-27
---

# Task: fix-reviewer-queue-hint

One-line change in Templates/mode-reviewer.template.md:106 — added --queue merge to the worktree merge dispatch example. Also fixed AgentRegistry.cs:484 where the release-block nudge was missing --queue merge and --auto-close. ReviewCommand.cs was already correct. All three hints now emit the same canonical command. dydo template update regenerated all agent mode files. 3239 tests pass, coverage gate clear.

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
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-reviewer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\InitCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\InitCommand.cs — Modified


## Review Summary

One-line change in Templates/mode-reviewer.template.md:106 — added --queue merge to the worktree merge dispatch example. Also fixed AgentRegistry.cs:484 where the release-block nudge was missing --queue merge and --auto-close. ReviewCommand.cs was already correct. All three hints now emit the same canonical command. dydo template update regenerated all agent mode files. 3239 tests pass, coverage gate clear.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-26 14:49
- Result: PASSED
- Notes: LGTM. All three merge dispatch hints (ReviewCommand.cs:136, AgentRegistry.cs:484, mode-reviewer.template.md:106) now emit the same canonical command with --queue merge --auto-close. All 25 agent mode files regenerated correctly. 3239 tests pass, coverage gate 131/131.

Awaiting human approval.

## Approval

- Approved: 2026-03-27 13:14
