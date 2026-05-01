---
id: 62
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-10
---

# Nudge evaluation runs before dangerous pattern detection, can shadow security checks

Resolved medium-severity correctness bug: nudge evaluation ran before dangerous-pattern detection in `HandleBashCommand`, so a soft nudge could short-circuit and shadow a hard security block. Fixed in commit `e97ebf1` by reordering the pipeline to check `DangerousPatterns` first.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in commit e97ebf1: HandleBashCommand reordered to check DangerousPatterns before Nudges, preventing nudge shadowing