---
title: Git merge worktree block and human-only command restriction lack guardrail IDs
area: project
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 66
type: issue
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-26
---

# Git merge worktree block and human-only command restriction lack guardrail IDs
Resolved low-severity docs finding: two existing guard checks (direct `git merge` in a worktree; human-only dydo subcommands) lacked `H##` IDs in `guardrails.md`. Fixed in commit `5ffcb54` by assigning H28 and H29 and adding them to the Bash Command Safety section.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Two new H## IDs assigned in commit 5ffcb54: H28 (direct git merge in worktree, Commands/GuardCommand.cs:758-779) and H29 (human-only dydo subcommands, Commands/GuardCommand.cs:620-633). Both added to Bash Command Safety section in dydo/reference/guardrails.md. Verified by Charlie.