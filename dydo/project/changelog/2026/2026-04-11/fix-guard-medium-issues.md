---
area: general
type: changelog
date: 2026-04-11
---

# Task: fix-guard-medium-issues

Fixed all 7 guard/security issues (#58, #85, #86, #60, #61, #63, #64). Key changes: (1) DangerousPatterns expanded for rm -rf ./, PowerShell, NVMe devices. (2) bash -c inner command analysis replaces blanket block. (3) Variable-as-command flags uncertain writes. (4) Off-limits bypass consistent for bash reads. (5) Dead code removed from OffLimitsService. (6) Agent state cached to reduce filesystem reads. (7) Block-severity nudges non-removable. All 3685 tests pass, gap_check green. No plan deviations.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Services\BashCommandAnalyzer.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\OffLimitsService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardSecurityTests.cs — Modified


## Review Summary

Fixed all 7 guard/security issues (#58, #85, #86, #60, #61, #63, #64). Key changes: (1) DangerousPatterns expanded for rm -rf ./, PowerShell, NVMe devices. (2) bash -c inner command analysis replaces blanket block. (3) Variable-as-command flags uncertain writes. (4) Off-limits bypass consistent for bash reads. (5) Dead code removed from OffLimitsService. (6) Agent state cached to reduce filesystem reads. (7) Block-severity nudges non-removable. All 3685 tests pass, gap_check green. No plan deviations.

## Code Review

- Reviewed by: Dexter
- Date: 2026-04-11 19:10
- Result: PASSED
- Notes: LGTM. MergeSystemNudges correctly enforces block severity on downgraded config nudges. Logic covers all edge cases. Test is focused and meaningful. 3686 tests pass, gap_check green (135/135).

Awaiting human approval.

## Approval

- Approved: 2026-04-11 19:34
