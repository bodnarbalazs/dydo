---
title: Agents default to creating decision docs unnecessarily
area: general
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 2
type: issue
found-by: manual
date: 2026-03-16
resolved-date: 2026-04-07
---

# Agents default to creating decision docs unnecessarily
Resolved low-severity prompt-engineering bug: agents were creating decision docs for changes that didn't actually warrant a decision record, polluting `project/decisions/`. Closed under the recent code-quality cleanup that tightened the docs-writer guidance on when a decision doc is the right artifact.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed in recent code quality work