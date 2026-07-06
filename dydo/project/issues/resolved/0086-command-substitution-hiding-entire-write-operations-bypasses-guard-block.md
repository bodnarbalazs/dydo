---
title: Command substitution hiding entire write operations bypasses guard block
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 86
type: issue
found-by: inquisition
date: 2026-04-10
resolved-date: 2026-04-26
---

# Command substitution hiding entire write operations bypasses guard block
Resolved medium-severity security bug: command substitution and backticks could hide an entire write operation from the guard. Fixed with a three-layer defense in commits `ea35282` and `4b162e2`: a bypass-attempt flag, recursive analysis of substituted content, and an outright block when bypass + any write op coincide; `AnalyzeUncertainCommand` covers variable-as-command-name.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Three-layer defense: (1) CheckBypassAttempts flags command-substitution/backticks (BashCommandAnalyzer.cs:198,386) and sets HasBypassAttempt; (2) AnalyzeCommandSubstitutions (:355) recursively analyzes inner content so hidden write ops are emitted; (3) GuardCommand.cs:787-801 blocks outright when HasBypassAttempt + any write op. AnalyzeUncertainCommand (:587) covers variable-as-command-name. Fix commits ea35282 and 4b162e2. Verified by Adele.