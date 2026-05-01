---
id: 60
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-26
---

# Off-limits bypass inconsistency between direct reads and bash reads

Resolved medium-severity correctness bug: direct file reads and bash-routed reads applied the off-limits-bypass logic differently, so a path that the Read tool blocked could leak through `cat` (or vice versa). Fixed in commit `4b162e2` by extracting a shared `ShouldBypassOffLimits` helper and routing both pipelines through it.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Shared ShouldBypassOffLimits helper at Commands/GuardCommand.cs:351 used by both direct reads (CheckDirectFileOffLimits :332) and bash reads (CheckBashFileOperation :827). Fix commit 4b162e2 ('Fix 8 audit and backend issues'). Verified by Adele in triage-guard-bash.