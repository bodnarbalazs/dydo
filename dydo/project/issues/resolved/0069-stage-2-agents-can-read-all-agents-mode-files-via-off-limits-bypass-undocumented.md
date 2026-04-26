---
id: 69
area: project
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-26
---

# Stage 2 agents can read all agents mode files via off-limits bypass — undocumented

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Disclosure half resolved in commit 5ffcb54. dydo/understand/guard-system.md now documents the Stage-2 cross-agent mode-file bypass (Commands/GuardCommand.cs:351-360 ShouldBypassOffLimits + IsAnyModeFile :1216-1223 matches all agents' mode files, not just self). Cross-linked from Stage 2 description. Resolved as 'disclosure' per Henry's recommended alternative. Code-tightening (scope to self-only) is a design call for the worktree-reliability cluster's sub-orchestrator (Adele) — they may file a new issue if the design opts for tightening.