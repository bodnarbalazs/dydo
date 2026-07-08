---
title: Docs-mirror archive try/catch is too broad — swallows real Notion errors and drops page tracking
area: backend
fix-release: 
needs-human: false
resolution: 
severity: high
status: open
work-type: 
id: 221
type: issue
found-by: review
date: 2026-07-07
---

# Docs-mirror archive try/catch is too broad — swallows real Notion errors and drops page tracking

Found by the Sonnet review of the docs-mirror archive-fix ([033-docs-notion-nested-page-mirror](../decisions/033-docs-notion-nested-page-mirror.md),
shipped dormant in v2.0.2). The archived-ancestor ordering fix added a per-archive `try/catch` for
robustness, but it catches **every** `NotionApiException`, not just the archived-ancestor 400 — and a
swallowed failure still advances the base snapshot, orphaning a live Notion page. **Low real-world
exposure today** because the docs mirror is gated OFF by default (opt-in `--docs`); this must be fixed
before anyone enables the mirror. Target: the 2.0.3 cycle.

## Description

**Mechanism.** `Sync/Notion/DocsPageAdapter.cs` archive loop:
```csharp
foreach (var externalId in Enumerable.Reverse(changes.Deletes))
{
    try { _client.UpdatePage(externalId, new NotionPageUpdateRequest { Archived = true }); }
    catch (NotionApiException ex) { _log?.WriteLine($"...could not archive {externalId}, skipping — {ex.Message}"); }
}
```
catches any `NotionApiException` (429 rate-limit, 401 expired/revoked token, 5xx, permissions) and
swallows it identically to the intended archived-ancestor 400. `Apply()` then returns normally, so in
`SyncRunner.Run` the batch-wide `applied` flag is `true` and `CommitBase` removes the local id's base
entry (`ReconcileAction.Delete → _base.Remove`) even though the archive never happened.

**Impact.** A repo doc is deleted and its archive fails transiently: the tick logs one easily-missed
stdout line, returns `ExitCodes.Success`, and drops the base entry. The Notion page stays **live but
untracked** (`ManagedPageIds` comes from `store.LocalIds`), so no future sync ever revisits it, CI/cron
sees a success exit, and a later re-create mints a duplicate page beside the orphan. This is **worse
than pre-fix**: before the try/catch, an archive failure threw, `applied` stayed `false`, the base was
retained for retry, and `NotionSyncService.Execute` surfaced a loud `ExitCodes.ToolError`.

**Fix.** Narrow the catch to the archived-ancestor condition only (inspect the exception signature, or
re-probe an archived-ancestor check before archiving) and let every other `NotionApiException`
propagate so `Execute` surfaces `ExitCodes.ToolError` and the base is left un-advanced for retry. If
partial-batch tolerance is genuinely wanted, have `Apply` report which deletes actually landed
(mirror the `assigned` dictionary used for creates) so `CommitBase` only drops base entries for
archives that succeeded. Add a test driving a **non-ordering** archive failure (the existing
`FakeNotionClient.FailUpdate` hook) to prove the error surfaces and the base is retained.

## Secondary (lower severity, fold into the same fix)

- **MEDIUM** — dry-run archive/retire prediction (`DocsTreeSync.WriteDryRunPlan`) checks Notion
  *presence* only, not content drift, so it prints `archive` for a doc the real run would actually
  **resurrect** (the delete-vs-external-edit `Conflict` case in `ReconcileEngine.DeleteOne`). Refine
  the prediction or label that case.
- **LOW** — the CLI mutual-exclusion guard rejects `--docs-only --spine-only` but silently accepts
  `--docs --spine-only` (spine-only wins, `--docs` ignored with no warning). Reject or warn for
  consistency.

## Reproduction
1. Enable the docs mirror (`dydo notion sync --docs`), delete a mirrored doc from the repo.
2. Make the archive `UpdatePage` fail with a non-ancestor error (e.g. rate-limit/auth).
3. Observe: exit success, base entry dropped, Notion page still live and now untracked.

## Resolution
(Filled when resolved)
