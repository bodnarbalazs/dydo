---
title: Page archival sends 'archived', rejected by Notion-Version 2026-03-11 — deletions never propagate
id: 305
area: backend
type: issue
severity: high
status: resolved
found-by: manual
date: 2026-07-24
resolved-date: 2026-07-26
---

# Page archival sends 'archived', rejected by Notion-Version 2026-03-11 — deletions never propagate

NotionPageUpdateRequestConverter serializes Archived as 'archived', but the pinned API version requires 'in_trash' for pages (400 validation_error), so repo-side deletions, docs-mirror removals, and live-test teardown all fail; the tests' best-effort catch swallowed it, leaking 42 smoke-* pages onto the real board.

## Description

Verified live: PATCH /v1/pages/<id> with {"archived": true} under Notion-Version 2026-03-11 returns 400 'body.archived should be not present'; {"in_trash": true} succeeds. Affected callers: NotionSyncAdapter.cs:223 (local deletion -> remote archive), DocsPageAdapter.cs:203 (docs mirror removal), NotionLiveTestBase.Dispose (teardown). Databases were already migrated to in_trash (NotionDatabaseUpdateRequest); pages were missed. Fix: serialize in_trash in NotionPageUpdateRequestConverter and update NotionClientTests wire-shape expectation. The 42 leaked pages were manually trashed on 2026-07-24.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Resolved in v2.2.3. Page soft-delete now serializes `in_trash` for the pinned Notion API
version, the wire-shape test rejects the old field, and the live harness sweeps stale smoke
pages without touching recent concurrent runs.
