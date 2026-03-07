---
area: general
type: changelog
date: 2026-03-07
---

# Task: template-update-system

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented template update system per decision 002. Changes: (1) DydoConfig.cs: added frameworkHashes property. (2) TemplateGenerator.cs: added ResolveIncludes and GetTemplateAdditionsPath methods, integrated into GenerateModeFile and GenerateWorkflowFile. (3) IncludeReanchor.cs: new service for extracting user-added include tags and re-anchoring them into updated templates. (4) TemplateCommand.cs: new 'dydo template update' command with --diff and --force flags. (5) FolderScaffolder.cs: scaffolds _system/template-additions/ folder with README and example, stores initial framework hashes. (6) All 8 mode templates: added {{include:extra-must-reads}} tags; code-writer: {{include:extra-verify}}; reviewer: {{include:extra-review-steps}} and {{include:extra-review-checklist}}. (7) Rules: updated FrontmatterRule, BrokenLinksRule, NamingRule, FixCommand to exclude _system/template-additions/. (8) 2 new embedded resources: template-additions-readme.md and extra-verify.example.md. (9) Program.cs: registered TemplateCommand. (10) 46 new tests across 3 test files covering include resolution, re-anchoring, hash computation, and integration scenarios including .example file non-resolution. All 1469 tests pass.

## Approval

- Approved: 2026-03-07 22:42
