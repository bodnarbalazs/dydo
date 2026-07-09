---
title: notion reset --parent-page archives the real board's databases - provision state is project-scoped, not parent-scoped
id: 257
area: backend
type: issue
severity: medium
status: open
found-by: inquisition
found-by-agent: Leo
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-09
---

# notion reset --parent-page archives the real board's databases - provision state is project-scoped, not parent-scoped

NotionReset loads tracked databases from the project-scoped provision.json and archives ALL of them regardless of the --parent-page override, so the documented scratch-workspace flow trashes the live board and leaves cross-contaminated provision state.

## Description

`NotionProvisioner.PathFor` (`Sync/Notion/Provisioning/NotionProvisioner.cs:34-35`) keys provision state by project only (`_system/.local/notion/provision.json`) — no parent-page scoping. `NotionReset.Execute` (`Sync/Notion/NotionReset.cs:53-55, 77-80`) resolves the parent from the `--parent-page` override but loads tracked databases from that project-scoped file and archives ALL of them, then re-mints the spine under the override target.

**Failure:** on a project whose real board is provisioned, `dydo notion reset --parent-page <scratch>` — the exact flow the option's help text recommends ("Point it at a scratch page to reset a throwaway workspace", `Commands/NotionCommand.cs:154`) — archives the REAL board's databases and recreates the spine under the scratch page. The confirmation prompt (`NotionCommand.cs:171`) never says which board dies.

**Secondary contamination:** post-reset, provision.json records the scratch databases, so a subsequent plain `dydo notion sync` against the configured real parent reuses the scratch databases via Lookup/StillValid — contamination persists in both directions.

**Cross-campaign seam:** DR-035 fixed this exact cross-contamination class for the docs mirror by parent-scoping its snapshot (`DocsTreeSync.SnapshotAdapterName`, `DocsTreeSync.cs:23-29`), but the reset command (6d98588) shipped the same day without applying the lesson to spine provision state.

Data is recoverable from Notion trash, hence medium rather than high.

Found by the v2.0.6 campaign inquisition; adversarially verified.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)