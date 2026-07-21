---
title: No command regenerates the live _system/sync-model.json - template updates never reach a provisioned board
id: 252
area: backend
type: issue
severity: medium
status: resolved
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-09
resolved-by: ns-11
---

# No command regenerates the live _system/sync-model.json - template updates never reach a provisioned board

Live 2.0.6 reset smoke (2026-07-09): dydo notion reset rebuilt the board from the STALE _system/sync-model.json - old dydo-prefixed titles, no Task/FutureFeature types - because Brian's DR-034 slice 1 updated Templates/sync-model.template.json but nothing regenerates the live model: dydo template update explicitly skips it and _system is guard-off-limits to agents, so only a manual human copy bridges template to live. Add a sanctioned regen path (e.g. dydo template update including sync-model with a diff-confirm, or a dydo notion model-update subcommand), so reset/sync consume model changes without hand-copying. Found live by balazs.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Resolved by ns-11 (half A). The live `dydo/_system/sync-model.json` now joins the hash-tracked
template-update flow: `TemplateCommand.FrameworkGeneratedFiles` registers it, handled by a dedicated
`UpdateGeneratedFile` (embedded content via `GetEmbeddedDocContent` → `SyncModelLoader.DefaultTemplateName`).
`dydo template update` refreshes an un-customized copy from `Templates/sync-model.template.json` and leaves a
project-customized one untouched. Because the model is materialized lazily (never seeded a baseline hash at
init), the handler is stricter than the doc-file path: a MISSING stored hash is treated as a user edit, so an
existing install that hand-edited its model — the pre-ns-11 state, where no hash exists — is skipped + warned,
never silently reverted; only an on-disk copy proven identical to the template (re)records the baseline so the
next bump is trusted. Reset/sync now consume model changes without a hand copy under the agent-off-limits
`_system` tree. Covered by `TemplateUpdateTests.UpdateFile_SyncModel_UnCustomized_Refreshed` /
`_Customized_LeftUntouched` / `_CustomizedWithNoStoredHash_LeftUntouched`.