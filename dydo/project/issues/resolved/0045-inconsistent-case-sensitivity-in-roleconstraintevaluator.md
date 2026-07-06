---
title: Inconsistent case sensitivity in RoleConstraintEvaluator
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 45
type: issue
found-by: inquisition
date: 2026-04-08
resolved-date: 2026-04-27
---

# Inconsistent case sensitivity in RoleConstraintEvaluator
Resolved low-severity correctness bug: `RoleConstraintEvaluator.CanRelease` and `CanDispatch` already used `OrdinalIgnoreCase`, but the `CanTakeRole` branches (role-transition, requires-prior, panel-limit) were case-sensitive. Fixed by unifying on case-insensitive comparison everywhere; cherry-picked from Emma's recovery branch as `2fba407`.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Unified case-insensitive comparison in RoleConstraintEvaluator. CanRelease/CanDispatch already used OrdinalIgnoreCase; CanTakeRole branches (role-transition, requires-prior, panel-limit) now use it too. Cherry-picked from Emma's recovery branch onto master as 2fba407 (originally 008e880). Verified by CI run 24998191977.