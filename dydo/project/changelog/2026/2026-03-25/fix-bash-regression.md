---
area: general
type: changelog
date: 2026-03-25
---

# Task: fix-bash-regression

Fixed two issues: (1) Restored empty-token and whitespace safety guards in BashCommandAnalyzer removed by 018e879 — the third guard (dash-colon) was genuinely unreachable and left removed. (2) Changed dydo\b to dydo(?=[\s"']|$) in IndirectPythonDydoRegex and IndirectShellDydoRegex to prevent matching dydo as a path component. Added 20 new tests. All 3153 tests pass, coverage gate passes.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Models\NudgeConfig.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\ConfigFactory.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigFactoryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TemplateCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\GuardCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\DydoConfig.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Serialization\DydoJsonContext.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\ValidationService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCompatTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ValidationServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\BashCommandAnalyzer.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\BashCommandAnalyzerTests.cs — Modified


## Review Summary

Fixed two issues: (1) Restored empty-token and whitespace safety guards in BashCommandAnalyzer removed by 018e879 — the third guard (dash-colon) was genuinely unreachable and left removed. (2) Changed dydo\b to dydo(?=[\s"']|$) in IndirectPythonDydoRegex and IndirectShellDydoRegex to prevent matching dydo as a path component. Added 20 new tests. All 3153 tests pass, coverage gate passes.

## Code Review

- Reviewed by: Dexter
- Date: 2026-03-23 20:36
- Result: PASSED
- Notes: LGTM. Both safety guard restorations are correct (empty-token and whitespace). Dash-colon guard correctly left removed (unreachable due to existing StartsWith dash check). Regex change from dydo\b to dydo lookahead is sound — prevents path-component false positives while matching all legitimate invocations. 20 new tests are meaningful. All 3153 tests pass. Coverage gate has 3 pre-existing failures (Program.cs, FrontmatterRule.cs, OffLimitsRule.cs) — none touched by this commit.

Awaiting human approval.

## Approval

- Approved: 2026-03-25 17:24
