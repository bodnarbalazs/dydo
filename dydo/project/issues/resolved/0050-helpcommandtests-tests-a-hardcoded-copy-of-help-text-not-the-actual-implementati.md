---
id: 50
area: general
type: issue
severity: high
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-10
---

# HelpCommandTests tests a hardcoded copy of help text, not the actual implementation

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in commit 4a7da79: HelpCommandTests now calls HelpCommand.PrintHelp directly instead of testing hardcoded copy