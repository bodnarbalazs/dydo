---
area: general
type: changelog
date: 2026-03-19
---

# Task: gap-check-method-flag

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MessageService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\MessageIntegrationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MacTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WatchdogServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified


## Review Summary

Implemented --methods flag in gap_check.py per plan. Added MethodCoverage dataclass, extended XML parser to retain per-method coverage data, added --methods CLI flag, wired into both print_report and print_inspect_report. Methods are deduplicated by (name, signature) during merge. Only methods with CC>0 are collected, and only those exceeding the tier CRAP threshold are displayed. No plan deviations.

## Code Review (2026-03-19 15:08)

- Reviewed by: Grace
- Result: FAILED
- Issues: Dead code: GENERATED_PATTERNS contains backslash pattern that can never match (all paths normalized to forward slashes before is_generated is called). Remove the \obj\ entry. The --methods implementation itself is correct.

Requires rework.

## Approval

- Approved: 2026-03-19 18:47
