---
title: Snapshot V2 and Write Receipts
sprint: notion-body-fidelity
seq: 2
status: done
area: backend
type: context
---

# Slice 2 — Snapshot V2 and Write Receipts

Persist distinct local/external body bases and crash-safe write intent/receipt state while keeping
legacy snapshots and identity adapters compatible.

## Spec fragment

Persist DR 043's two body bases and crash journal without breaking snapshot-v1 loading or existing
identity adapters. Acceptance: v1/v2 round-trip, atomic pending-intent durability, exact observed receipt
storage, and no default path that fabricates an external projection.

## Implementation detail

Add one-file types `BodyWriteIntent`, `BodyWriteReceipt`, `BodyWriteOperationKind`, `SyncBodyReadStatus`,
and `SyncApplyResult` under `Sync/`. `BodyWriteOperationKind` has exactly `Create`, `Update`, and
`Resolution`. `BodyWriteIntent` persists `OperationId` (UUID string), `Kind`, `LocalId`, nullable
`ExternalId`, `PriorLocalBody`, `PriorExternalBody`, and `IntendedLocalBody`; a create therefore has an
identity before a remote page id exists, and resolution is not inferred from surrounding state. Give each
`SyncSnapshot` an object-level `BodyVersion` (missing/0 means v1), nullable v1 `Body`, v2
`LocalBody`/`ExternalBody`, and optional pending intent. Do not add a file-level version: safe v2 objects
must coexist with unresolved v1 objects. Keep JSON property names explicit/stable and register every new
source-generated type in `Serialization/DydoJsonContext.cs`.

Extend `BaseSnapshotStore` with state-oriented getters/setters that expose a `DualBodyBase`, write/remove
pending intent, atomically save, and distinguish v1 from v2. Existing `Get`/`Set(SyncDoc)` remain as the
identity-adapter compatibility path and map local==external. A v1 load must not rewrite the file until a
real migration/intent operation occurs.

Add `SyncUpsert.WriteBody` (default `true`) while keeping `Body` non-null: `false` means properties only;
`true` plus `Body == ""` means clear the external body. Add `SyncChangeSet.BodyUpserts`/validation only if
it shortens call sites; do not create a parallel batch abstraction. A body upsert also carries the
journaled `OperationId` so the projected adapter can put it on the mutation.

Extend `SyncRecord` with `BodyReadStatus` defaulting to `Complete` for source/backward compatibility.
`Truncated` means `Body` is diagnostic/partial transport data and must never be reconciled, persisted, or
used as a base. No adapter may signal truncation by substituting an empty string or throwing away status.

Add `ISyncAdapter.ApplyWithReceipts` as a default method that calls existing `Apply` and returns receipts
only for identity bodies; projected adapters override it and must return an observed external body for
every successful body upsert. Do not force unrelated adapters/tests to implement speculative behavior.

Add exact test classes `DualProjectionSnapshotTests` and `BodyWriteReceiptTests`. Cover old JSON fixtures,
mixed v1/v2 JSON, missing/partial properties, pending intent surviving a fresh
store instance, JSON round-trips for all three operation kinds including a create with null external id,
receipt commit clearing intent only after save, complete/truncated record status, AOT source-gen
serialization, and identity adapter compatibility.

## Out of scope for this slice

Reconciliation, file patching, Notion overrides, and automatic legacy classification.

## Gate

```powershell
$listed = dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --list-tests --filter "FullyQualifiedName~DualProjectionSnapshot|FullyQualifiedName~BodyWriteReceipt"
if (($listed | Select-String 'DualProjectionSnapshot|BodyWriteReceipt').Count -lt 8) { throw 'Snapshot gate matched fewer than 8 tests.' }
dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "FullyQualifiedName~DualProjectionSnapshot|FullyQualifiedName~BodyWriteReceipt"
dotnet build DynaDocs.csproj --no-restore
```
