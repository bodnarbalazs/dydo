---
title: Swarm 0259
area: general
name: swarm-0259
status: stale
created: 2026-07-12T18:49:06.0011538Z
assigned: Emma
needs-human: false
---

# Task: swarm-0259

CODEX swarm fix ROUND 2 — issue 0259. Your round-1 fix is CLOSE: a Claude reviewer adversarially VERIFIED that the 0235 no-wipe invariant holds in EVERY branch (you did NOT trade duplicates for body-wipes) and the self-heal converges. KEEP those. But the review FAILED on two points — one is a real remaining duplicate window (the blocker), one is an architecture-contract violation. Self-contained; report then RELEASE YOURSELF. Under the dydo guard + auto mode.

BLOCKER — the duplicate-on-retry window is NARROWED, not CLOSED. You set `assigned[upsert.LocalId] = page.Id` AFTER the read-back GET (`DocsPageAdapter.cs:161`, `_client.GetPageMarkdown(page.Id)`). If that read-back GET itself THROWS (a 429/5xx on the network call the fix fires immediately after every body-carrying create, under the ~3 req/s throttle the codebase cites at DocsTreeSync.cs:61), the exception unwinds BEFORE `assigned` gets the id → `SyncRunner.CommitBase` records nothing → next tick resurrects and mints a SECOND page while the first is orphaned unmanaged. That is the exact #0259 symptom via a different throw site. The reference `DocsTreeSync.CreatePageWithBodyAndRecord` records the EMPTY base at DocsTreeSync.cs:254 BEFORE the read-back at :259 — that record-FIRST ordering is the load-bearing crash-safety property (its comment DocsTreeSync.cs:236-237: "The base is recorded EMPTY the instant the page exists ... a mid-phase failure must never orphan an unrecorded page and re-mint it as a duplicate"). You mirrored the degrade but not the ordering.
FIX (mirror the ordering exactly): IMMEDIATELY after `CreatePage` returns (BEFORE the read-back GET), set `assigned[upsert.LocalId] = page.Id` AND mark it empty-base (add LocalId to the empty-base set). THEN do the read-back: on a FAITHFUL non-empty read-back, REMOVE the empty-base mark (full base commits). On silent-ignore + PATCH SUCCESS, REMOVE the mark (full base — page has the body). On PATCH FAILURE, KEEP the mark (empty base persisted). A read-back-GET throw now leaves the id recorded with an empty base → next tick converges to ONE page, no duplicate, no wipe (external empty == base empty → not an external clear).

SECONDARY (architecture contract) — `SyncRunner.CommitBase` (`SyncRunner.cs:223`) now does a concrete downcast: `_adapter is Notion.DocsPageAdapter { CreatedWithEmptyBase: var emptyBases }`. This violates SyncRunner's own class contract (SyncRunner.cs:9-10: "Notion-agnostic — it only ever touches ISyncAdapter and SyncDoc"). Route the empty-base marker through the `Apply` call the SAME way `assigned`/`deleted` already flow back — e.g. add an `ICollection<string> emptyBodied` (or similar) out-param to `ISyncAdapter.Apply`, populated by the adapter, read by CommitBase — instead of a mutable side-channel property + downcast. This removes the drift-prone temporal coupling (Clear-at-Apply-start, read-only-between-Apply-and-CommitBase). Since NO other swarm agent is running now, you MAY touch `ISyncAdapter` and its other implementers (`DocsTreeSync`, spine sync) to add the param — they can ignore/pass it through if they don't set empty bases. If this genuinely sprawls beyond a clean out-param addition, KEEP the current mechanism and say so in your report (Adele will file a follow-up) — but the BLOCKER fix above is required regardless.

ADD TEST (pins the closed window): a fake-client failure mode that THROWS on the first `GetPageMarkdown` after a body-carrying create → run two ticks → assert the base carries the created id with an EMPTY body AND exactly ONE page exists (`Assert.Single(client.GetChildPages(root))`), no duplicate. This must be RED against your round-1 code (which throws before recording) and GREEN after.
Also fix the stale comment in `FakeNotionClient.cs` (~line 113) that still says "the adapter's rare resurrect create THROWS" — this fix makes that false.

KEEP: the 0235 no-wipe branch logic (verified correct), the self-heal, and your 3 existing tests (silent-ignore no-dup, failed-PATCH empty-base retry, faithful-readback full-base).

VERIFY: `dotnet build DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore` (0 errors); `dotnet test` filtered to DocsPageAdapter + SyncRunner + DocsTreeSync tests — all green including the new read-back-throw test. Do NOT run the python coverage gate.

REPORT + RELEASE: `dydo msg --to Adele --subject swarm-0259-r2` with: the record-before-readback reordering (confirm the id is recorded immediately after CreatePage), how you routed the empty-base marker (Apply out-param, or kept-with-flag if it sprawled), the new read-back-throw test (RED-before/GREEN-after), build/test results, ~time. THEN release yourself.

CONSTRAINTS: touch `Sync/Notion/DocsPageAdapter.cs`, `Sync/SyncRunner.cs`, `Sync/ISyncAdapter.cs` (+ `Sync/Notion/DocsTreeSync.cs` / spine adapter ONLY for the Apply-param signature if you route via Apply), `DynaDocs.Tests/Sync/Notion/DocsPageAdapterTests.cs`, `DynaDocs.Tests/Sync/Notion/FakeNotionClient.cs` (comment + throw-mode). Do NOT change the sync data model or the 0235-safe branch logic. Do NOT run git checkout/reset/stash/clean.

--- STANDING INSTRUCTIONS ---
1. TEST VECTOR: the read-back-GET-throws case must be a real test, RED before this fix.
2. COMPLEXITY: keep any method <= CC 30.
3. NO DESTRUCTIVE GIT.
4. REPORT+RELEASE as above; do NOT run the python gate.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

(Pending)

> Mass-closed 2026-07-16 (DR-041 campaign wrap-up): pre-campaign roster-era task; the work either landed before the pivot or was abandoned with the roster. See git history.
