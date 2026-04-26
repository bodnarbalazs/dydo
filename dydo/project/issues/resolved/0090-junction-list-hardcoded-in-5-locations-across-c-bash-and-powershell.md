---
id: 90
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-10
resolved-date: 2026-04-26
---

# Junction list hardcoded in 5+ locations across C#, bash, and PowerShell

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Junction list centralized — JunctionSubpaths at Commands/WorktreeCommand.cs:634-640 is the single authoritative list, consumed by GenerateBashJunctionScript (:647), GeneratePsJunctionScript (:664), and TeardownWorktree (:705). Fix commit 99a9a33. Verified by Dexter in triage-worktree.