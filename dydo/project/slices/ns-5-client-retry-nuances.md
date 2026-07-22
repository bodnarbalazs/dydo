---
title: ns-5 Client Retry Nuances
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

# ns-5 Client Retry Nuances

`NotionClient.SendWithRetry` (`Sync/Notion/NotionClient.cs:216-242`) retries 429/500/502/503/504 + transport throws for **all** requests. Two gaps versus the official SDKs' taxonomy (see [notion-oss-survey.md](../../reference/notion-oss-survey.md), API-limits section): 529 `service_overload` is not retried, and 5xx retry on **non-idempotent** requests (POST create) risks duplicate pages — a create that died on 500 may have succeeded server-side.

## Task

1. Add 529 to the retryable status set (treat like 429: honor `Retry-After`, else backoff+jitter).
2. Split retry policy by idempotency via a per-request flag on the send helper (default idempotent; non-idempotent senders opt out — no HTTP-verb sniffing). **Exactly three senders opt out** (each is a POST that creates an object): `CreatePage`, `CreateDatabase`, `CreateView`. Everything else (retrieves, queries, updates/PATCH full-replace, archive, delete-block, append — append is add-only but re-appending duplicates blocks: also opt it out, making it four) keeps 5xx retry. Final opt-out set: **CreatePage, CreateDatabase, CreateView, AppendBlockChildren.**
3. Recovery per opted-out sender on ambiguous failure (5xx/transport-throw):
   - **CreatePage** (`NotionSyncAdapter` create path): re-query the data source for a page with the record's title (via `QueryDataSource`; add a title-filter DTO if needed); found ⇒ adopt its id into the mapping; not found ⇒ re-create.
   - **CreateDatabase** (`NotionProvisioner`): re-search before re-creating — note `SearchDataSources` currently returns bare ids (`NotionClient.cs:168-176` discards title/parent): **extend the search response DTO to expose title + parent** so the recovery can match, then adopt a match into provision state.
   - **CreateView** (post-pass): list views and match by name before re-creating — `ListViewIds` returns ids only: **extend the view-list DTO to carry view names.**
   - **AppendBlockChildren**: no adoption possible — surface the failure as a body-sync error for that record (its snapshot must not advance; the next sync retries the body); never blind re-append.
4. 429/529 remain retryable for all senders including the opt-outs (rate responses are unambiguous — the request was rejected, nothing was created).

## Files

- `Sync/Notion/NotionClient.cs`, `INotionClient.cs`
- `Sync/Notion/NotionSyncAdapter.cs` (create recovery)
- `Sync/Notion/Dtos/` (title filter, if needed)
- Tests: `DynaDocs.Tests/Sync/Notion/NotionClientTests.cs` (FakeHttpMessageHandler), `NotionSyncAdapterTests.cs`

## Success criteria

- New tests: 529 → retried with backoff; create + 500 → NOT blind-retried, adapter re-queries and adopts existing page when found, creates fresh when not; create + 429 → retried.
- Existing retry tests still green; full ratchet green.
