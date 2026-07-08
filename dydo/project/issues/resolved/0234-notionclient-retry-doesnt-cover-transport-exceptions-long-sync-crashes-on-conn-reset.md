---
title: NotionClient retry covers HTTP status codes but not transport exceptions — long syncs crash on a connection reset
area: backend
fix-release: 
needs-human: false
resolution: 
severity: high
status: resolved
work-type: 
id: 234
type: issue
found-by: review
date: 2026-07-08
resolved-date: 2026-07-08
---

# NotionClient retry covers HTTP status codes but not transport exceptions — long syncs crash on a connection reset

Found by the DR 033 docs-mirror live re-smoke (the retry-robust build). The 0226 retry
([0226-notionclient-no-retry-on-transient-5xx-429-makes-long-syncs-fragile](./0226-notionclient-no-retry-on-transient-5xx-429-makes-long-syncs-fragile.md)) only inspects the HTTP
**status code** (429/500/502/503/504). A transport-level failure — a forcibly-closed socket / connection
reset / timeout — throws `HttpRequestException` **before any response exists**, so the retry never sees
it; the exception propagates **unhandled** and crashes the whole sync (exit 1, raw stack trace), because
`NotionSyncService.Execute` only catches `NotionApiException`. Over a ~1600-call docs sync, a transient
connection reset is effectively inevitable. Low real-world exposure today (mirror is off-by-default), but
this must land before `--docs` is enabled.

## Description

**Observed** (live re-smoke, isolated build 2.0.5+6a8b112):
```
System.Net.Http.HttpRequestException: An error occurred while sending the request.
 ---> System.IO.IOException: Unable to read data from the transport connection...
 ---> System.Net.Sockets.SocketException (10054): An existing connection was forcibly closed by the remote host.
   at DynaDocs.Sync.Notion.NotionClient.SendWithRetry(...) NotionClient.cs:203
   at ... DeleteBlock ... DocsPageAdapter.ReplaceBody ... SyncRunner.Run ... DocsTreeSync.Run ...
```

**Mechanism.** `SendWithRetry` (`Sync/Notion/NotionClient.cs`) awaits `_http.SendAsync(...)` and only
branches on `resp.StatusCode` via `IsRetryable`. When `SendAsync` **throws** (`HttpRequestException`,
inner `SocketException`/`IOException`, or `TaskCanceledException` on a timeout), there is no `resp` — the
throw escapes the retry loop entirely. `Execute`'s `catch (NotionApiException)` doesn't match it, so it's
an unhandled crash rather than a clean `ExitCodes.ToolError`.

**Impact.** Any long sync (esp. the docs mirror) aborts hard on the first transient connection reset,
with an ugly stack trace and a non-`ToolError` exit. It's resumable, but unreliable and user-hostile.

**Fix.**
1. In `SendWithRetry`, wrap the `SendAsync` call in `try/catch` for **transient transport exceptions**
   — `HttpRequestException` (and `TaskCanceledException`/`OperationCanceledException` from a timeout when
   no external cancellation is in play). Treat them like a retryable status: backoff + retry up to
   `MaxAttempts`. On exhaustion, **wrap in `NotionApiException`** (e.g. a synthetic status) so the
   existing `Execute` catch surfaces a clean `ToolError` instead of crashing.
2. Keep genuine non-transient failures propagating as before.
3. Tests (fake transport): a handler that throws `HttpRequestException` once then returns 200 → succeeds
   after retry; a handler that always throws → surfaces `NotionApiException` (clean) after exactly
   `MaxAttempts`, never an unhandled `HttpRequestException`.

## Reproduction
1. Run a long `dydo notion sync --docs` (hundreds of sequential calls).
2. When Notion/an intermediary forcibly closes a connection mid-run, observe the whole sync crash with an
   unhandled `HttpRequestException`/`SocketException(10054)` (exit 1), not a clean retry or `ToolError`.

## Resolution

Fixed in d43c0e3: SendWithRetry catches transport exceptions (HttpRequestException/IOException/TaskCanceledException) scoped to SendAsync, retries with backoff, wraps exhaustion as NotionApiException so sync returns ToolError instead of crashing. Reviewed PASS + sprint-audited (run-sprint wmp0u5apd, Charlie); 4504/4504 green.
