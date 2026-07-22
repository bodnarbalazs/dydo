---
title: ns-11 Model Regeneration and Additive Re-Provisioning
blocked-by:
due:
needs-human: false
priority: Normal
sprint: notion-stabilization
status: done
work-type: feature
area: backend
type: context
---

# ns-11 Model Regeneration and Additive Re-Provisioning

Two halves of "the model can evolve without a reset":

**A — issue 0252:** nothing regenerates `dydo/_system/sync-model.json` from `Templates/sync-model.template.json`; they match today by luck. Fold the live file into the hash-tracked template-update flow: `dydo template update` refreshes it when un-customized, leaves it alone when customized (identical semantics to markdown templates — check `TemplateUpdateTests` / the hash-tracking service for the existing mechanism and register the JSON file there).

**B — backlog `notion-board-followups.md`:** provisioning is mint-only. A model change (new property, new select option, changed `notionTitle`) never reaches an already-provisioned live board; today's only path is a full reset. Add an **additive-only** update pass to `NotionProvisioner`: when a type is already provisioned and `StillValid`, diff the model against the live schema (machinery exists in `Sync/Notion/Provisioning/NotionSchemaDrift.cs` — extend, don't duplicate) and apply via `UpdateDataSource`:
- create missing properties;
- add missing select/multi-select **options** (normalized names; never set colors — Notion owns colors, survey spine-lessons);
- update the data source **title** when `notionTitle` changed;
- **never delete or retype** anything — destructive drift stays a warning (existing `--prune` behavior unchanged).

## Files

- `Commands/TemplateCommand.cs` / template-update service + hash tracking (locate via `TemplateUpdateTests`)
- `Sync/Notion/Provisioning/NotionProvisioner.cs`, `NotionSchemaDrift.cs`
- `Sync/Notion/Dtos/` (data-source title update payload, if missing)
- Tests: `DynaDocs.Tests/Services/TemplateUpdateTests.cs`, `DynaDocs.Tests/Sync/Notion/NotionProvisionerTests.cs`, `NotionSchemaDriftTests.cs`

## Success criteria

- New tests: template update refreshes an un-customized sync-model.json and skips a customized one; provisioner adds a missing property, adds a missing option without touching existing options' colors, renames the data-source title on `notionTitle` change; retype/delete drift still only warns.
- Full ratchet green; issue 0252 resolved; `notion-board-followups.md` updated (retro-provisioning items ticked, rest left).
