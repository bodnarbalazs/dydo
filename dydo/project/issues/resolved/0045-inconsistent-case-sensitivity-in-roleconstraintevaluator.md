---
id: 45
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-08
resolved-date: 2026-04-27
---

# Inconsistent case sensitivity in RoleConstraintEvaluator

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Unified case-insensitive comparison in RoleConstraintEvaluator. CanRelease/CanDispatch already used OrdinalIgnoreCase; CanTakeRole branches (role-transition, requires-prior, panel-limit) now use it too. Cherry-picked from Emma's recovery branch onto master as 2fba407 (originally 008e880). Verified by CI run 24998191977.