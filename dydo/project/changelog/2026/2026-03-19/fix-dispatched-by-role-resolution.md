---
area: general
type: changelog
date: 2026-03-19
---

# Task: fix-dispatched-by-role-resolution

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Services\InboxMetadataReader.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\InboxMetadataReaderTests.cs — Modified


## Review Summary

Fixed non-deterministic inbox file selection in GetDispatchedFrom, GetDispatchedFromRole, and GetDispatchedRole. When multiple inbox items exist for the same task, the methods now pick the one with the newest received timestamp instead of relying on Directory.GetFiles ordering. Consolidated the fix in InboxMetadataReader (which already existed as an extracted class but was unused), added GetDispatchedFromRole to it, and wired AgentRegistry to delegate to it. Added 4 tests covering multi-file scenarios where the newest item must win.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-19 18:23
- Result: PASSED
- Notes: LGTM. Core fix is clean — iterates all matching inbox files and picks newest by received timestamp. GetDispatchedFromRole added. AgentRegistry properly delegates to InboxMetadataReader, eliminating ~100 lines of duplicated code. 4 new tests cover multi-file scenarios for all three methods. All 11 tests pass. No regressions.

Awaiting human approval.

## Approval

- Approved: 2026-03-19 18:47
