---
id: 64
area: project
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-26
---

# H19 indirect dydo invocation documented as hard-coded but is configurable nudge

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

H19 reclassified in dydo/reference/guardrails.md (commit 5ffcb54). New 'severity-pinned default nudge' label replaces 'hard-coded' for H19. Implementation reference: Services/ConfigFactory.cs:9-22 DefaultNudges + Commands/GuardCommand.cs:587-609 MergeSystemNudges (pattern/message user-editable in dydo.json; severity force-restored to block). Extensibility section split into 'hard-coded' vs 'severity-pinned default nudge'. Verified by Charlie.