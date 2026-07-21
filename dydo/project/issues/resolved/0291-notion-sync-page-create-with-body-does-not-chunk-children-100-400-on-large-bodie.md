---
title: Notion sync: page create-with-body does not chunk children >100 (400 on large bodies)
id: 291
area: backend
type: issue
severity: high
status: resolved
resolved-date: 2026-07-21
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-13
---

# Notion sync: page create-with-body does not chunk children >100 (400 on large bodies)

AppendBlockChildren chunks at 100 (DR-033) but CreatePage inlines the whole body; a >100-block doc/object 400s on creation.

## Description

## Observed

`dydo notion sync` against a real workspace fails with:

```
Notion API returned 400: body failed validation: body.children.length should be <= `100`, instead was `189`.
```

...after reconciling some objects (seen right after "sync Campaign reconciled 1 object").

## CORRECTION (2026-07-13, verified by planner + adversarial plan-review)

**The root-cause section below is WRONG.** It blames `DocsTreeSync.CreatePageWithBodyAndRecord`, which sends the `Markdown` DTO field and therefore *cannot* emit a `body.children.length` error at all — and the docs mirror is dormant (`--docs` is off by default).

**The actual culprit is the SPINE create path:** `Sync/Notion/NotionSyncAdapter.cs` `Apply` (~:126-132) inlines the entire block body as `Children = blocks` in the create POST, unchunked. `children` and `markdown` are mutually-exclusive fields on `NotionPageCreateRequest`; the spine uses `Children`. Confirmed by the failing log sequence (`sync {type} reconciled N object(s)` from `NotionSpineSync.cs:247` immediately preceding the 400). `NotionClient.AppendBlockChildren` already chunks at 100 (DR-033); `CreatePage` does not.

**Fix direction:** create with at most 100 children, record `assigned[localId] = page.Id` immediately, then append the remainder via the already-chunking `AppendBlockChildren`, guarding the remainder-append with the existing `emptyBodied` / `SyncRunner.CommitBase` mechanism so a mid-append failure records an EMPTY base body (averting the #0235 silent-wipe class) rather than a full-body base against a partial page.

**Known degraded outcome to accept + test:** on a mid-append failure, the retry tick three-way-merges (base `""` vs full repo body vs partial external body) and, because the spine has NO conflict-shadow, writes conflict markers into the canonical spine file. Visible, recoverable, converging — but it must be acknowledged and covered by a test, not advertised away.

## Root-cause (SUPERSEDED — see correction above)

`Sync/Notion/NotionClient.cs:147 AppendBlockChildren` ALREADY chunks appends at 100 per request (DR-033). But the page-**CREATE** path — `DocsTreeSync.CreatePageWithBodyAndRecord` → `client.CreatePage(new NotionPageCreateRequest { Markdown = body })` — inlines the ENTIRE markdown body as `children` in the initial create request, and Notion caps create-children at 100 too. Any doc or spine object whose body renders to >100 blocks 400s on creation. The append-chunking never gets a chance to help because the failure is at create time.

## Fix direction

In the CreatePage-with-body path, create the page with at most 100 children (or zero), then use the already-chunking `AppendBlockChildren` for the remainder. Mirror the DR-033 chunking constant. Critically, keep the create+append idempotent/recorded: the existing read-back-guard / base-record logic (DR-035) must still hold so a mid-operation failure or retry does not duplicate or leave an unrecorded page.

## Acceptance

- Syncing a doc/object with a >100-block body succeeds.
- Add a test with a >100-block body asserting no single request exceeds 100 children AND the full body round-trips into the page.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed by ns-6: `NotionBlockAppender` chunks block payloads at ≤100 top-level AND ≤1000 total elements, with depth-2 cutting + iterative appends for nested structure; `CreatePage` carries a capped head and appends the remainder. LIVE-VERIFIED 2026-07-21 (ns-10, Opus 4.8 continuation): `NotionLiveLargeBodyTests` (a >100-block body — including a >2000-char run — creates then appends without a 400) passes against real Notion, and a full real-board sync reconciled 397 records including large bodies with zero 400s. Closed.