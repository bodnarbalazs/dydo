---
title: Spine Native Markdown Transport
sprint: notion-body-fidelity
seq: 4
status: done
area: backend
type: context
---

# Slice 4 — Spine Native Markdown Transport

Replace PM-spine block conversion with native Markdown body transport and confirmed read-back
receipts while leaving the property channel unchanged.

## Spec fragment

Move every current PM-spine body create/read/update onto Notion's native Markdown API and return exact
read-back receipts. Acceptance: no spine body transport calls the block converter or recursively reads
body blocks, properties behave unchanged, truncation fails safe, and fake request accounting proves
which bodies and structural child lists were read and written.

## Implementation detail

In `NotionSyncAdapter`, read each selected page with `GetPageMarkdown`, stable-clean through the same
documented volatile-artifact cleaner used by DR 035, and return `SyncBodyReadStatus.Truncated` when the
API marks the response truncated; never pass partial text as a candidate body. Create pages with the `Markdown` field; update bodies
with `UpdatePageMarkdown`, selecting the child-safe flag from actual child-page presence. Keep property
mapping/update/clear behavior unchanged.

Honor `SyncUpsert.WriteBody`: a property-only upsert updates properties/clears and performs zero body
read/write or structural-child calls. For an existing-page body write, enumerate child pages exactly once
through `GetChildPages` (which may use one real block-children request, but never invokes the body block
converter). If none, send the stable local body with
`allow_deleting_content:true`. If children exist, XML-escape each title, reconstruct
`<page url="https://app.notion.com/p/{id}">title</page>` lines in enumeration order, append them after the
body, and send `allow_deleting_content:false` as DR 035's live contract requires. Receipts pass through
`CleanForPersist`, so those structural tags never enter the external base or canonical file.

Override `ApplyWithReceipts`: after each body create/update, immediately `GetPageMarkdown` and return the
stable-cleaned observed projection keyed by local id/external id. A missing/truncated/throwing read-back
does not return a receipt. Property-only upserts do not rewrite or re-read the body.

Provision an engine-reserved rich-text `dydo-write-id` property on every object type in both the template
and tracked sync model. Mark it `hidden` for newly configured views, but do not update existing view
metadata in this Sprint and do not rely on UI visibility for correctness. Filter it from
`SyncRecord.Fields` and canonical frontmatter,
and never accept a repo value for it. Every body mutation writes the pending `OperationId`; a create puts
it in the initial property payload. Replace `CreatePageWithRecovery` title matching with exact lookup of
that UUID. One live unarchived match is adopted, zero is retried once, and multiple matches throw a
structured ambiguity that the runner shadows. The same lookup is exposed to pending-intent restart
recovery, so a process death after page creation but before id assignment is recoverable.

Extend `NotionProvisioner.ApplyModelAdditions` (or a directly adjacent preflight it owns) so projected
sync first adds an absent property, reads the live data-source schema back, and verifies
`dydo-write-id` is exactly `rich_text` before any runner/reconcile mutation. A pre-existing same-name
property of the wrong type fails closed with a diagnostic naming the data source/property/expected and
actual types; never retype or delete it automatically. On rollback, leave a correctly added inert column
in place. Test fresh provisioning, additive existing-database provisioning, correct pre-existing type,
and wrong-type collision proving zero page/file mutations. The engine-reserved column is injected and
verified even when a custom/legacy sync model omits it; schema drift always treats it as known, and
`--prune` must never delete it. This Slice therefore owns the narrow `NotionSchemaDrift` protocol-key
change plus reused-custom-model and fresh-custom-model-with-prune regressions.

Extend the neutral record/runner seam narrowly for restart recovery: `SyncRecord` carries the reserved
operation UUID separately from canonical `Fields`; `SyncRunner.MapExternalToLocalId` first uses the
snapshot's trusted external id, then pairs exactly one record UUID to a pending `Create` UUID. Zero matches
leaves the same create intent retryable; one binds the Notion page id; duplicate matching UUIDs produce an
explicit unhandled identity-ambiguity result and complete marker-bearing shadow, with canonical/pages/base/
intent untouched. Do not issue an extra per-intent query on restart and never expose `dydo-write-id` in
frontmatter. This Slice therefore owns the narrow `SyncRecord`/`SyncRunner`/`ReconcileResult` extension and
must rerun all Slice 2/3 projected and identity regressions.

Wire every PM-spine `SyncRunner` construction (manual run, dry-run, and delta) with projected bodies
enabled. The runner's per-object v1 guard retains legacy behavior until Slice 5 classifies/migrates it;
v2/new objects must never run native Markdown through converter-era comparison. Freshly minted as well as
reused data sources must perform the reserved-property live readback/type preflight before any page/file
reconcile. This Slice owns these narrow production wiring/preflight changes in `NotionSpineSync` and
`NotionSpineDelta`; Slice 5 revisits the same files serially for migration and watchdog integration.

Remove `NormalizeBody`/`IsStaleConverterEcho` from current spine comparison use; keep legacy converter
helpers reachable only by Slice 5 migration. In `FakeNotionClient`, make native Markdown reads/writes and
`GetChildPages` increment `RequestCount`, add separate Markdown read/write/structural-child counters, and add an opt-in echo transform/throw/truncation
hook. Default exact echo remains explicit and must not be described as modeling live fidelity.

Add exact test class `NativeMarkdownSpineAdapterTests`. Pin create/update/read endpoints, body-vs-property request profiles, receipts, truncation,
failure, child safety, zero body-block conversion calls, the one permitted structural enumeration and
its request cost, operation-id create recovery (ambiguous response, process restart, duplicate UUID,
duplicate titles), additive reserved-property provisioning and wrong-type fail-closed behavior, existing relation/property behavior, and the control that a
Notion-created page imports its native body intact.

## Out of scope for this slice

Snapshot-v1 migration, full/delta orchestration, and live API calls.

## Gate

```powershell
$listed = dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --list-tests --filter "FullyQualifiedName~NativeMarkdownSpine"
if (($listed | Select-String 'NativeMarkdownSpine').Count -lt 14) { throw 'Adapter gate matched fewer than 14 new tests.' }
dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "FullyQualifiedName~NativeMarkdownSpine|FullyQualifiedName~NotionSyncAdapterTests|FullyQualifiedName~NotionClientTests|FullyQualifiedName~NotionProvisionerTests|FullyQualifiedName~SyncModelLoaderTests|FullyQualifiedName~ProjectedReconcileTests|FullyQualifiedName~SyncRunnerTests|FullyQualifiedName~BodyWriteReceiptTests|FullyQualifiedName~NotionSpineSyncTests|FullyQualifiedName~NotionSpineDeltaTests"
dotnet build DynaDocs.csproj --no-restore
```
