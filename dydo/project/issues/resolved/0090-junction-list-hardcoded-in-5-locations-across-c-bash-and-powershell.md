---
title: Junction list hardcoded in 5+ locations across C#, bash, and PowerShell
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 90
type: issue
found-by: inquisition
date: 2026-04-10
resolved-date: 2026-04-26
---

# Junction list hardcoded in 5+ locations across C#, bash, and PowerShell
Resolved medium-severity duplication finding: the junction subpath list was hardcoded across five-plus places in C#, bash, and PowerShell, so adding a junction meant editing all of them. Fixed in commit `99a9a33` by promoting `JunctionSubpaths` in `WorktreeCommand` to the single authoritative list consumed by both shell-script generators and `TeardownWorktree`.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Junction list centralized — JunctionSubpaths at Commands/WorktreeCommand.cs:634-640 is the single authoritative list, consumed by GenerateBashJunctionScript (:647), GeneratePsJunctionScript (:664), and TeardownWorktree (:705). Fix commit 99a9a33. Verified by Dexter in triage-worktree.