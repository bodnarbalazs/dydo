---
area: general
type: changelog
date: 2026-04-09
---

# Task: fix-audit-junction

Fixed 2 of 4 inbox findings (findings 3 and 4 were already implemented in codebase). Changes: (1) SnapshotCompactionService atomic writes via WriteAtomic temp-then-rename pattern at lines 251 and 287. (2) AuditCommand XSS fix: WebUtility.HtmlEncode for session table HTML, plus esc() JS helper for innerHTML in buildAgentLegend and processEvent. 9 new tests (5 compaction, 4 XSS). All 3640 tests pass, gap_check green.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\CompactionAtomicWriteTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\AuditXssTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\SnapshotCompactionService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\AuditCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\OffLimitsServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\OffLimitsService.cs — Modified


## Review Summary

Fixed 2 of 4 inbox findings (findings 3 and 4 were already implemented in codebase). Changes: (1) SnapshotCompactionService atomic writes via WriteAtomic temp-then-rename pattern at lines 251 and 287. (2) AuditCommand XSS fix: WebUtility.HtmlEncode for session table HTML, plus esc() JS helper for innerHTML in buildAgentLegend and processEvent. 9 new tests (5 compaction, 4 XSS). All 3640 tests pass, gap_check green.

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-09 19:10
- Result: PASSED
- Notes: LGTM. Atomic writes correct (temp+rename pattern, NTFS-safe). XSS fix thorough — WebUtility.HtmlEncode for server-side, esc() DOM helper for all innerHTML paths. 8 new tests cover real attack vectors. gap_check green (136/136). Clean, minimal changes.

Awaiting human approval.

## Approval

- Approved: 2026-04-09 22:49
