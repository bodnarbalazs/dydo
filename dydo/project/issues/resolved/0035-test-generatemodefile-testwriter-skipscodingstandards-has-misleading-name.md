---
id: 35
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-08
resolved-date: 2026-04-26
---

# Test GenerateModeFile_TestWriter_SkipsCodingStandards has misleading name

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Test renamed in commit e4cd980 (Dexter) to match assertion. Production behaviour correct: test-writer mode files include coding-standards (mode-test-writer.template.md:18, with template comment 'tests are code too'). Reviewed by Frank.