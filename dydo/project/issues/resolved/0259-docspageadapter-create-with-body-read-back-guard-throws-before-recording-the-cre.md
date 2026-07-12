---
title: DocsPageAdapter create-with-body read-back guard throws before recording the created page - each retry mints an orphan duplicate
id: 259
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
found-by-agent: Leo
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-09
resolved-date: 2026-07-12
---

# DocsPageAdapter create-with-body read-back guard throws before recording the created page - each retry mints an orphan duplicate

The adapter create path throws NotionApiException before assigned[LocalId]=page.Id, so CommitBase never records the page that DOES exist in Notion; every retry re-creates it. DocsTreeSync got the graceful fix in c2aeff8a (empty base + child-safe PATCH degrade); the adapter ingress from 85674a76 did not.

## Description

In the adapter's create path (`Sync/Notion/DocsPageAdapter.cs:163-166`), a non-empty body that reads back empty throws `NotionApiException` BEFORE `assigned[upsert.LocalId] = page.Id` — but `CreatePage` (line 147) already made the page in Notion. `SyncRunner.CommitBase` (`SyncRunner.cs:207-215`) records a create's base only from `assigned`, so the existing page gets no base entry, its id is absent from `_managedPageIds`, the walk skips it (`DocsPageAdapter.cs:124`, unmanaged pages are never adopted), and the next tick's `CreateToExternal` mints another page.

**Failure:** one orphan duplicate per retry, and the tick fails every time with no self-heal — directly breaking SyncRunner's stated invariant that a crashed tick "does not re-create (duplicate) pages on retry" (`SyncRunner.cs:113-116`).

**Triggers (production-reachable):** Notion silently ignoring markdown-on-create (the scenario the guard exists for), or a merely eventual-consistency-lagged read-back straight after create — a lag the codebase itself treats as expected (`ReconcileEngine.cs:110-116`). Reached via ExternalDeleted + RepoOwnedStructure → CreateToExternal. Under a transient lag the sync self-heals after stranding one full-body orphan; under a persistent silent ignore it mints an empty orphan every tick.

**Cross-rewrite incoherence (why per-slice review missed it):** the c2aeff8a review fixed this same failure mode gracefully in `DocsTreeSync.CreatePageWithBodyAndRecord` (`DocsTreeSync.cs:245-270`: record empty base first, warn, degrade to child-safe PATCH — duplicate-free, self-healing) but left the adapter path from 85674a76 with throw-before-record. The final composition handles one ingress safely and duplicate-mints on the other.

**Fix caution:** the adapter cannot trivially record `assigned` before throwing — CommitBase would then persist NewBase's FULL body against the empty page (the 0235 wipe). The fix needs plumbing for an adapter-controlled empty base on create, mirroring the DocsTreeSync approach.

Found by the v2.0.6 campaign inquisition; adversarially verified.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

RESOLVED 2026-07-12 (landed 01e128b3). DocsPageAdapter create path now records assigned[LocalId] + an empty-base marker IMMEDIATELY after CreatePage, BEFORE the read-back GET - so a throw from the read-back (429/5xx) can no longer orphan the created page and re-mint a duplicate next tick (the remaining window round-1 left open). Mirrors DocsTreeSync's crash-safe record-first ordering. The empty-base marker is routed through a new ISyncAdapter.Apply emptyBodied out-collection (removed the concrete DocsPageAdapter downcast that violated SyncRunner's Notion-agnostic contract). 0235 no-wipe preserved in every branch. Codex Emma (Terra, 2 rounds), Claude adversarially-reviewed PASS; read-back-throw regression RED-before/GREEN-after, exactly one page.