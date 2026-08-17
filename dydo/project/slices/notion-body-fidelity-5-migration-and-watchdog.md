---
title: Legacy Migration and Watchdog Integration
sprint: notion-body-fidelity
seq: 5
status: ready
area: backend
type: context
---

# Slice 5 — Legacy Migration and Watchdog Integration

Migrate legacy bases without silent winners and route the same projected reconciliation through
both manual full sync and daemon delta ticks.

## Spec fragment

Wire safe snapshot-v1 migration and the shared projected engine through manual full sync and daemon
delta ticks. Acceptance: migration never chooses a winner silently, full/delta behavior is identical,
diagnostics are actionable, and the slice-11 offline sequence is byte-safe and quiet.

## Implementation detail

In `NotionSpineSync` and `NotionSpineDelta`, construct the adapter/runner with projected-body mode and
the same per-type shadow resolver. Before ordinary reconcile, classify each v1 object without writes:
compare current repo/current native Markdown to the v1 local base and the known legacy block echo using
semantic equivalence. Adopt equivalent pairs; route unique one-sided deltas through projected reconcile;
route two-sided/ambiguous/truncated cases to a migration shadow and retain v1 state.

Replace `PromoteResolvedShadows`' current pre-push base seeding. For a marker-free resolved shadow,
read the current stable external projection, atomically write the resolved canonical file, persist a
`BodyWriteIntent` marked `Resolution` with the old local base/current external base/resolved intent, and
leave both dual bases unadvanced. Do not delete the shadow until the normal runner pushes the resolution,
obtains its read-back receipt, commits both bases, and clears the intent. A throw leaves the canonical
resolution plus shadow/intent recoverable; retry must not reclassify it as an ordinary two-sided edit.

Ensure delta changed-union includes pending intents and migration-needed local ids even when mtimes/page
stamps are quiet. Do not advance the delta cursor past an unhandled pending/migration conflict. Full and
delta output names local id, reason, canonical path, and shadow path; never log body text/token data.

On restart with a pending `Create` whose `ExternalId` is null, query by its exact `dydo-write-id` before
ordinary external/local pairing. Exactly one result binds the intent and continues receipt recovery;
zero leaves it eligible for one idempotent create attempt; multiple results shadow and perform no write.
Strip the reserved property after pairing so neither external-originated creation nor recovery can put it
in canonical frontmatter. A truncated read during migration or pending recovery is always unhandled and
leaves cursor/base/intent unchanged.

Add exact test classes `NotionSnapshotMigrationTests`, `NotionDualProjectionFullSyncTests`, and
`NotionDualProjectionDeltaTests`. Add `DynaDocs.Tests/Sync/Notion/Fixtures/slice-11-sanitized.md` as this
Slice's fixture. Cover v1 safe adoption, repo-only/external-only migration, ambiguity,
truncation, interrupted migration, pending write recovery, full/delta equivalence, boundary stamp echo,
crash-after-create recovery with duplicate titles/UUID ambiguity, and request cost. Add the sanitized slice-11 fixture now and prove local edit → daemon push → transformed
fake echo → next delta action `None`, file byte-identical. Also prove a real fake-modeled external edit
imports once and leaves frontmatter/untouched spans exact.

## Out of scope for this slice

Live Notion execution and final documentation/issue closure.

## Gate

```powershell
$listed = dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --list-tests --filter "FullyQualifiedName~NotionSnapshotMigration|FullyQualifiedName~NotionDualProjectionFullSync|FullyQualifiedName~NotionDualProjectionDelta"
if (($listed | Select-String 'NotionSnapshotMigration|NotionDualProjection').Count -lt 10) { throw 'Integration gate matched fewer than 10 new tests.' }
dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "FullyQualifiedName~NotionSnapshotMigration|FullyQualifiedName~NotionDualProjectionFullSync|FullyQualifiedName~NotionDualProjectionDelta|FullyQualifiedName~NotionSpineDeltaTests|FullyQualifiedName~NotionSpineSyncTests"
dotnet build DynaDocs.csproj --no-restore
```
