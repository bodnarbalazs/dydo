---
area: general
name: fix-issues-small
status: review-pending
created: 2026-04-08T18:06:19.0715080Z
assigned: Frank
updated: 2026-04-08T18:38:21.9982072Z
---

# Task: fix-issues-small

Review code changes for issues #26, #36, #37. Changes: (1) WindowsTerminalLauncher.cs: escaped agentName and windowName in PowerShell env var assignments for consistency with existing prompt escaping. (2) TemplateGeneratorTests.cs: added inquisitor, judge, orchestrator to AllModes test theories (ReadBuiltInTemplate, HaveValidFrontmatter, HaveSetRoleSection). (3) TemplateOverrideTests.cs: added 3 missing template assertions in Init_CopiesTemplatesToSystemFolder, strengthened Init_StoresFrameworkHashes to verify hash count and SHA256 format. (4) TerminalLauncherTests.cs: added 2 new env var escaping tests, fixed pre-existing compile error (missing detector variable). Doc issues #33 and #34 dispatched separately to docs-writer.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review code changes for issues #26, #36, #37. Changes: (1) WindowsTerminalLauncher.cs: escaped agentName and windowName in PowerShell env var assignments for consistency with existing prompt escaping. (2) TemplateGeneratorTests.cs: added inquisitor, judge, orchestrator to AllModes test theories (ReadBuiltInTemplate, HaveValidFrontmatter, HaveSetRoleSection). (3) TemplateOverrideTests.cs: added 3 missing template assertions in Init_CopiesTemplatesToSystemFolder, strengthened Init_StoresFrameworkHashes to verify hash count and SHA256 format. (4) TerminalLauncherTests.cs: added 2 new env var escaping tests, fixed pre-existing compile error (missing detector variable). Doc issues #33 and #34 dispatched separately to docs-writer.