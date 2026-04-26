---
id: 66
area: project
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-26
---

# Git merge worktree block and human-only command restriction lack guardrail IDs

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Two new H## IDs assigned in commit 5ffcb54: H28 (direct git merge in worktree, Commands/GuardCommand.cs:758-779) and H29 (human-only dydo subcommands, Commands/GuardCommand.cs:620-633). Both added to Bash Command Safety section in dydo/reference/guardrails.md. Verified by Charlie.