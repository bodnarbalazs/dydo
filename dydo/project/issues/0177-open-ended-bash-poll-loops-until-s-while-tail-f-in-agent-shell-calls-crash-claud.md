---
title: Open-ended Bash poll-loops in agent shell calls crash claude — neither original nor resumed session reaches a tool call
id: 177
area: backend
type: issue
severity: medium
status: open
found-by: inquisition
date: 2026-05-07
---

# Open-ended Bash poll-loops in agent shell calls crash claude — neither original nor resumed session reaches a tool call

Two crashed sessions (Brian 4c2838f8 May 6, no sidecar at all; Charlie 4090052a May 7, sidecar tail is 'until [ -s /tmp/claude/...]') share the same shape: claude reaches the open-ended poll, the harness OOMs/CPU-watchdogs, and the resumed claude inherits the same large conversation and dies in the same place. Recovery rate (84-89%) holds for ordinary crashes but collapses to ~0% for this class because the rehydrated claude hits the same crash. dydo cannot recover this from the watchdog side — fix is preventative: add a coding-standards rule and a guard nudge against open-ended polls in agent shell calls. Also worth adding to dydo/agents/*/workflow.md defensive notes. Source: 'Remaining deeper issues' #1 in dydo/project/inquisitions/agent-crashes.md (Dexter, 2026-05-08).

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)