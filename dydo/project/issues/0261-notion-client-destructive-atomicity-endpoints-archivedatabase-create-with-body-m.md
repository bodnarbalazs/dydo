---
title: Notion client destructive/atomicity endpoints (ArchiveDatabase, create-with-body markdown) have no wire-shape test - fake/wire divergence invisible to the suite
id: 261
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

# Notion client destructive/atomicity endpoints (ArchiveDatabase, create-with-body markdown) have no wire-shape test - fake/wire divergence invisible to the suite

ArchiveDatabase (the reset wipe primitive) and the create-with-body markdown field are exercised only through FakeNotionClient's in-memory stub; a serialization regression to a shape Notion accepts as a no-op/degrade stays green in all 339 Notion tests and only surfaces on a live destructive/sync run.

## Description

Two destructive/atomicity-critical Notion endpoints added this campaign have no HTTP-layer (wire-shape) assertion — they are exercised only through `FakeNotionClient`'s in-memory stub, so a serialization regression stays invisible while the whole suite (339 Notion tests) stays green. The failure only surfaces on a live run.

**1. `ArchiveDatabase` — the destructive primitive of `dydo notion reset`** (`Sync/Notion/NotionClient.cs:64`, added in 6d985884)
Sends `PATCH databases/{id}` with `NotionDatabaseUpdateRequest{InTrash=true}`. No test asserts path/verb/body — only `FakeNotionClient.cs:234` (in-memory), `NotionSyncServiceTests.cs:349` (empty no-op), `NotionResetTests.cs:156` (fake failure flag). If the PATCH path or `in_trash` serialization (`NotionDatabaseUpdateRequest` / `NotionJsonContext` registration) regresses to a shape Notion accepts as a no-op 200, reset would "archive" nothing, then delete provision state (`NotionReset.cs:87-88`) and re-mint duplicates — the exact orphan-duplicate failure the code's own comment (`NotionReset.cs:83-86`) says the archive-before-clear ordering exists to prevent.

**2. Create-with-body `markdown` field** (`Sync/Notion/NotionClient.cs:103`, `Dtos/NotionPageCreateRequest.cs:28-30`, added in 85674a76)
Declares `[JsonPropertyName("markdown")] [JsonIgnore(WhenWritingNull)]`, posted by `CreatePage`. All three `CreatePage` wire tests (`NotionClientTests.cs:62, 115, 252`) construct requests WITHOUT `Markdown`, so no test proves the field serializes; the only byte-level `"markdown"` body assertion is the NEGATIVE one at `NotionClientTests.cs:189`. All create-with-body coverage runs through `FakeNotionClient.cs:288` (reads the property in memory). If name/JsonIgnore/AOT-context registration regressed, every fresh docs sync would silently degrade to the child-safe-PATCH path (`DocsTreeSync.cs:259-264`) and every resurrect-create would THROW via the read-back guard (`DocsPageAdapter.cs:163-165`, see issue #259) — while the suite stays green. Precedent: c2aeff8a added byte-level wire tests for the PATCH variant after a live 400 exposed exactly this fake/wire divergence class; the POST variant never got the same treatment.

Verify-only: reproduce by asserting the serialized request body for each endpoint against the live Notion wire shape. NOT a request to land fixes.

Found by the v2.0.6 campaign inquisition (coverage lens); adversarially verified. Note: several other real-client endpoints (UpdateDataSource, CreateView, ListViewIds, DeleteView, RetrieveDataSource) are also fake-only, but these two are the destructive/atomicity-critical ones whose silent regression is unrecoverable.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)