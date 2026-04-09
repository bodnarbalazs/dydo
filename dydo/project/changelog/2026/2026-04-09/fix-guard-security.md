---
area: general
type: changelog
date: 2026-04-09
---

# Task: fix-guard-security

Fixed 4 guard security findings from inquisition: (1) CRITICAL guard lift self-escalation via hardcoded off-limits in OffLimitsService, (2) HIGH interpreter bypass via DangerousPatterns, (3) HIGH command substitution evasion via HasBypassAttempt flag + tainted write blocking, (4) HIGH nudge false positives via HandleBashCommand reordering. 25 new tests, all 3640 tests pass, gap_check green.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardSecurityTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardWorktreeAllowBashWriteTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\OffLimitsService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\BashCommandAnalyzer.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\IBashCommandAnalyzer.cs — Modified


## Review Summary

Fixed 4 guard security findings from inquisition: (1) CRITICAL guard lift self-escalation via hardcoded off-limits in OffLimitsService, (2) HIGH interpreter bypass via DangerousPatterns, (3) HIGH command substitution evasion via HasBypassAttempt flag + tainted write blocking, (4) HIGH nudge false positives via HandleBashCommand reordering. 25 new tests, all 3640 tests pass, gap_check green.

## Code Review (2026-04-09 19:06)

- Reviewed by: Frank
- Result: FAILED
- Issues: SystemOffLimits check in OffLimitsService.IsPathOffLimits (line 69) runs AFTER the whitelist check (line 66), contradicting the 'not whitelistable' comment. A whitelist pattern matching .guard-lift.json (e.g. dydo/agents/** or **/*.json) bypasses the hardcoded protection entirely, defeating Finding #55. Fix: move SystemOffLimits check before the whitelist check. Add a test verifying whitelist cannot override system patterns.

Requires rework.

## Code Review

- Reviewed by: Frank
- Date: 2026-04-09 19:37
- Result: PASSED
- Notes: LGTM. Reordering SystemOffLimits before whitelist is the correct fix for the self-escalation bypass. Test is well-targeted. All 3641 tests pass, gap_check green (136/136 modules). Clean, minimal change.

Awaiting human approval.

## Approval

- Approved: 2026-04-09 22:49
