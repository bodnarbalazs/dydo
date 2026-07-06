---
title: Test GenerateModeFile_TestWriter_SkipsCodingStandards has misleading name
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 35
type: issue
found-by: inquisition
date: 2026-04-08
resolved-date: 2026-04-26
---

# Test GenerateModeFile_TestWriter_SkipsCodingStandards has misleading name
Resolved low-severity test-quality finding: the test name claimed test-writer mode skips coding-standards, but the actual production behavior (and the test's own assertion) was that it includes them. Test was renamed in commit `e4cd980` to match the assertion; production behavior was already correct.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Test renamed in commit e4cd980 (Dexter) to match assertion. Production behaviour correct: test-writer mode files include coding-standards (mode-test-writer.template.md:18, with template comment 'tests are code too'). Reviewed by Frank.