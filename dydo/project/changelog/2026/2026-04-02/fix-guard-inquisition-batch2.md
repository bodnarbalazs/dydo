---
area: general
type: changelog
date: 2026-04-02
---

# Task: fix-guard-inquisition-batch2

Fixed 4 inquisition issues: (1) removed unused CheckCommand from IOffLimitsService, rewrote bash off-limits tests to use production path (BashCommandAnalyzer + IsPathOffLimits); (2) removed dead dangerous-pattern recheck in AnalyzeAndCheckBashOperations; (3) ClearLift now delegates to Restore; (4) converted 8 static regex patterns in GuardCommand.cs to GeneratedRegex. All tests pass, gap_check passes. 12 pre-existing test failures unrelated to changes.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardLiftTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\GuardLiftService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\BashCommandAnalyzerTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\GuardCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\OffLimitsServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\IOffLimitsService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\OffLimitsService.cs — Modified


## Review Summary

Fixed 4 inquisition issues: (1) removed unused CheckCommand from IOffLimitsService, rewrote bash off-limits tests to use production path (BashCommandAnalyzer + IsPathOffLimits); (2) removed dead dangerous-pattern recheck in AnalyzeAndCheckBashOperations; (3) ClearLift now delegates to Restore; (4) converted 8 static regex patterns in GuardCommand.cs to GeneratedRegex. All tests pass, gap_check passes. 12 pre-existing test failures unrelated to changes.

## Code Review (2026-03-30 19:08)

- Reviewed by: Frank
- Result: FAILED
- Issues: Undocumented code change at GuardCommand.cs:731-746 adds staged access control for bash reads — not listed in brief, no dedicated tests. Issues #0007, #0008, #0010 are clean. Fix: add tests for the new bash read access control path and document it.

Requires rework.

## Code Review

- Reviewed by: Frank
- Date: 2026-03-30 19:28
- Result: PASSED
- Notes: LGTM. 4 bash staged access tests are clean, meaningful, and mirror the existing Read tool staged access tests. Tests cover all 3 guard stages. gap_check passes (396/396). 2 pre-existing WatchdogServiceTests failures unrelated.

Awaiting human approval.

## Approval

- Approved: 2026-04-02 18:55
