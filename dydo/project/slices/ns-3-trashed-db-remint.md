---
title: ns-3 Trashed-Database Detection in StillValid
blocked-by: ns-1-parent-scoped-state
due:
needs-human: false
priority: Medium
sprint: notion-stabilization
status: done
work-type: bug
area: backend
type: context
---

# ns-3 Trashed-Database Detection in StillValid

A Notion database moved to trash still 200s on retrieval, so `NotionProvisioner.StillValid()` happily reuses it and the spine syncs into a trashed board — silent, confusing, recorded in `dydo/reference/notion-sync.md` ("workspace-level trashed pages can't be un-trashed via API; must clear provision.json to force re-mint"). The provisioner should detect the trashed state and re-mint instead.

## Task

1. In `Sync/Notion/Provisioning/NotionProvisioner.cs` `StillValid` (and/or its `Lookup` path): after retrieving the database/data source, treat `in_trash: true` (and `archived: true`) as invalid — same handling as a 404: drop the stale provision entry and re-mint the type.
2. Ensure the retrieve DTO actually carries `in_trash`/`archived` (`Sync/Notion/Dtos/` database/data-source response records) — add the fields if absent.
3. Log one clear line when a trashed database is detected and re-minted.
4. Stale base snapshot for that type must be deleted on re-mint (existing mint-delete path — verify it fires on this branch too, with ns-1's parent-scoped names).

## Files

- `Sync/Notion/Provisioning/NotionProvisioner.cs`
- `Sync/Notion/Dtos/` (database/data-source response DTOs)
- `Sync/Notion/FakeNotionClient`-backed tests: `DynaDocs.Tests/Sync/Notion/NotionProvisionerTests.cs`

## Success criteria

- New tests: retrieve returns `in_trash: true` → type re-mints (new create issued, provision entry replaced, snapshot cleared); `in_trash: false` → reused as today.
- Full ratchet green.
