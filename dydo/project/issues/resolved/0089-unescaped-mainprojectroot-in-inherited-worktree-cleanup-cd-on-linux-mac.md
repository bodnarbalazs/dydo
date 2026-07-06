---
title: Unescaped mainProjectRoot in inherited-worktree cleanup cd on Linux/Mac
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 89
type: issue
found-by: inquisition
date: 2026-04-10
resolved-date: 2026-04-26
---

# Unescaped mainProjectRoot in inherited-worktree cleanup cd on Linux/Mac
Resolved low-severity injection finding: `mainProjectRoot` was substituted into Linux/Mac inherited-worktree cleanup `cd` commands without escaping, breaking when the path contained apostrophes. Fixed in commit `3654ec6` by extracting a `BashSingleQuoteEscape` helper and applying it at five sites; covered by helper unit tests and Linux/Mac integration tests.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
BashSingleQuoteEscape helper extracted and applied at 5 sites in commit 3654ec6 (Frank). Linux/Mac launcher cleanup scripts now correctly escape apostrophes in mainProjectRoot via the standard '\"\''\" quoted bash idiom. Helper tests + Linux/Mac integration tests verify behavior. Reviewed by Charlie.