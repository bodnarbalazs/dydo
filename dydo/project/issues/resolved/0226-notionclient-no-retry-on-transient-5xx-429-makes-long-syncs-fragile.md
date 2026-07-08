---
title: NotionClient has no retry on transient 5xx/429 — long syncs (esp. the docs mirror) are fragile
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 226
type: issue
found-by: review
date: 2026-07-07
resolved-date: 2026-07-08
---

# NotionClient has no retry on transient 5xx/429 — long syncs (esp. the docs mirror) are fragile

Surfaced during the DR 033 docs-mirror live re-smoke: a `dydo notion sync --docs-only` run against
an empty scratch page **failed on a transient Notion 504** (`Notion API returned 504`, exit 2) partway
through, leaving the structure created but bodies unwritten (the run aborts mid phase-2). Not a mirror
logic bug — but it exposes that `NotionClient` does **no retry on transient failures**, which makes any
long-running sync unreliable.

## Description

**Mechanism.** `Sync/Notion/NotionClient.cs` throws `NotionApiException` on **any** non-2xx response
immediately, with no retry/backoff (`~L69-72`, `L136-142`, `L188-191`). The only resiliency is the
`Throttle()` rate-limiter (~3 req/s). Transient, retryable statuses — **429** (rate limit, with
`Retry-After`), **500/502/503/504** (gateway/server) — are treated as hard failures.

**Impact.** The docs mirror walks and writes a large tree: ~798 pages ⇒ ~1600+ sequential HTTP calls
at 3 req/s over ~20 minutes. Over that window a transient 5xx is *likely*, and a single one aborts the
whole tick with exit 2. The sync is resumable (phase-1 pages persist to the snapshot, so a re-run
no-ops structure and continues), so it *converges over repeated manual re-runs* — but that is fragile
and a poor operator experience, and it's the main thing standing between the docs mirror and *reliable*
enable-readiness. The spine sync has the same exposure but a smaller surface.

**Fix.** Add bounded retry-with-backoff to `NotionClient` for retryable statuses: honor `Retry-After`
on 429, exponential backoff on 500/502/503/504, a small max-attempts cap, and leave 4xx (except 429)
as immediate hard failures. Mirror how Claude Code itself treats 529 overload (retry) vs hard limits
(surface). Keep it inside the client so every caller (spine + docs) benefits. Add a test using a fake
transport that returns 503 then 200 and asserts the call succeeds.

## Reproduction
1. Run a large `dydo notion sync` (e.g. the docs mirror `--docs --parent-page <fresh page>` over a big
   docs tree) so it issues hundreds of sequential calls.
2. When Notion returns a transient 5xx/429 for any single call, observe the whole run abort with exit 2.

## Resolution

Status-code retry (429/500/502/503/504 with backoff + Retry-After) landed committed as part of the DR 033 track (Charlie); confirmed in-tree and superseded by the 0234 transport-exception extension in d43c0e3.
