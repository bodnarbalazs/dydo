---
title: H19 indirect dydo invocation documented as hard-coded but is configurable nudge
area: project
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 64
type: issue
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-26
---

# H19 indirect dydo invocation documented as hard-coded but is configurable nudge
Resolved medium-severity docs finding: `guardrails.md` labelled H19 (indirect dydo invocation) as "hard-coded" when it's actually a default nudge whose pattern and message are user-editable in `dydo.json`, with only the severity force-restored to block. Fixed in commit `5ffcb54` by reclassifying H19 as a "severity-pinned default nudge" and splitting the Extensibility section into hard-coded versus severity-pinned categories.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
H19 reclassified in dydo/reference/guardrails.md (commit 5ffcb54). New 'severity-pinned default nudge' label replaces 'hard-coded' for H19. Implementation reference: Services/ConfigFactory.cs:9-22 DefaultNudges + Commands/GuardCommand.cs:587-609 MergeSystemNudges (pattern/message user-editable in dydo.json; severity force-restored to block). Extensibility section split into 'hard-coded' vs 'severity-pinned default nudge'. Verified by Charlie.