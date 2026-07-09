---
title: No command regenerates the live _system/sync-model.json - template updates never reach a provisioned board
id: 252
area: backend
type: issue
severity: medium
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-09
---

# No command regenerates the live _system/sync-model.json - template updates never reach a provisioned board

Live 2.0.6 reset smoke (2026-07-09): dydo notion reset rebuilt the board from the STALE _system/sync-model.json - old dydo-prefixed titles, no Task/FutureFeature types - because Brian's DR-034 slice 1 updated Templates/sync-model.template.json but nothing regenerates the live model: dydo template update explicitly skips it and _system is guard-off-limits to agents, so only a manual human copy bridges template to live. Add a sanctioned regen path (e.g. dydo template update including sync-model with a diff-confirm, or a dydo notion model-update subcommand), so reset/sync consume model changes without hand-copying. Found live by balazs.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)