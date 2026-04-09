---
area: general
type: changelog
date: 2026-04-09
---

# Task: fix-issues-small

Review code changes for issues #26, #36, #37. Changes: (1) WindowsTerminalLauncher.cs: escaped agentName and windowName in PowerShell env var assignments for consistency with existing prompt escaping. (2) TemplateGeneratorTests.cs: added inquisitor, judge, orchestrator to AllModes test theories (ReadBuiltInTemplate, HaveValidFrontmatter, HaveSetRoleSection). (3) TemplateOverrideTests.cs: added 3 missing template assertions in Init_CopiesTemplatesToSystemFolder, strengthened Init_StoresFrameworkHashes to verify hash count and SHA256 format. (4) TerminalLauncherTests.cs: added 2 new env var escaping tests, fixed pre-existing compile error (missing detector variable). Doc issues #33 and #34 dispatched separately to docs-writer.

## Progress

- [ ] (Not started)

## Files Changed

C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Services/CompletionProviderTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TemplateGeneratorTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\TemplateOverrideTests.cs — Modified


## Review Summary

Review code changes for issues #26, #36, #37. Changes: (1) WindowsTerminalLauncher.cs: escaped agentName and windowName in PowerShell env var assignments for consistency with existing prompt escaping. (2) TemplateGeneratorTests.cs: added inquisitor, judge, orchestrator to AllModes test theories (ReadBuiltInTemplate, HaveValidFrontmatter, HaveSetRoleSection). (3) TemplateOverrideTests.cs: added 3 missing template assertions in Init_CopiesTemplatesToSystemFolder, strengthened Init_StoresFrameworkHashes to verify hash count and SHA256 format. (4) TerminalLauncherTests.cs: added 2 new env var escaping tests, fixed pre-existing compile error (missing detector variable). Doc issues #33 and #34 dispatched separately to docs-writer.

## Code Review

- Reviewed by: Jack
- Date: 2026-04-08 18:50
- Result: PASSED
- Notes: LGTM. All 3538 tests pass, gap_check green (135/135 modules). Code is clean, surgical changes. Fixes #18 (RunProcessWithExitCode fallthrough), #19 (double-dash git separators), #20 (consistent git -C). Template hash/update logic corrected. Include reanchor order-preservation fixed. Dead code removed. No security issues.

Awaiting human approval.

## Approval

- Approved: 2026-04-09 22:49
