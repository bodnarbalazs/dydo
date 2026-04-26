---
id: 89
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-10
resolved-date: 2026-04-26
---

# Unescaped mainProjectRoot in inherited-worktree cleanup cd on Linux/Mac

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

BashSingleQuoteEscape helper extracted and applied at 5 sites in commit 3654ec6 (Frank). Linux/Mac launcher cleanup scripts now correctly escape apostrophes in mainProjectRoot via the standard '\"\''\" quoted bash idiom. Helper tests + Linux/Mac integration tests verify behavior. Reviewed by Charlie.