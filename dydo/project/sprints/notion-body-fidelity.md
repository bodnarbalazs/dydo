---
title: Notion Body Fidelity
seq: 16
status: active
gate-result: PASS 2026-08-17
campaign: dydo-2-0
area: backend
type: context
---

# Notion Body Fidelity

Deliver DR 043's dual-projection, format-preserving PM-spine body sync and prove the exact
existing-record watchdog path offline and against isolated real Notion.

## 1. Specification

**Intent** — Implement DR 043 so PM-spine bodies remain uniformly bidirectional without comparing
authored Markdown bytes to Notion's lossy dialect. Store distinct local/external projections, import
observable Notion edits through a format-preserving syntax-tree patch, move spine body transport to
the native Markdown API, and prove the exact watchdog failure path against real Notion.

**In scope**

- Snapshot-v2 local/external body bases, durable pending body-write receipts, and safe v1 migration.
- A source-spanned Markdig alignment/patch engine that preserves every untouched repo span.
- Representation-local body change detection and independent body/frontmatter file patching.
- Native Markdown create/read/update for spine bodies; properties stay on their current path.
- Full-sync and delta/watchdog integration, permanent conflict-shadow behavior, diagnostics, and safe
  recovery after interrupted writes.
- The sanitized slice-11 fixture, deterministic mutation/fidelity coverage, isolated scratch live
  tests, full regression/coverage gates, and issue/reference updates after proof.

**Out of scope**

- One-way bodies, ownership rules, blanket quarantine, or a temporary compatibility mode.
- Persisting normalized Markdown, extending normalization until the fixture happens to pass, or
  treating a current-corpus sweep as a general proof.
- A broad docs-mirror redesign. Shared generic improvements may be used by it, but this Sprint's live
  acceptance is the PM spine.
- Notion presentation/view metadata, unrelated schema evolution, and deleting legacy converter code
  still required solely to recognize snapshot-v1 echoes during migration.
- Recovering formatting distinctions that Notion erases before export; those are unobservable per
  DR 043's honest boundary.

**Acceptance criteria**

1. Snapshot v2 persists exact `localBody` and stable-cleaned `externalBody`; a no-op compares each
   current representation only with its own base. Old snapshot JSON remains loadable.
2. Every successful spine body create/update uses the native Markdown API, immediately reads its
   exact echo, and advances both projections only after the receipt is confirmed.
3. A create/update/resolution that lands before a read-back/process failure is recoverable from a
   durable typed pending record. Creates use a preassigned UUID persisted in engine-reserved `dydo-write-id`, not
   title matching; retry neither duplicates a row, invents an external author, nor loses a concurrent edit.
4. A genuine Notion edit changes only its uniquely mapped source span. Untouched body bytes and all
   frontmatter bytes are identical; field-only edits keep the body identical; disjoint two-sided edits
   compose once.
5. Overlap, repeated-section ambiguity, truncated export, and unprovable legacy migration go to the
   existing spine shadow with canonical file, Notion page, and base unadvanced. No uncertain or
   marker-bearing body reaches a canonical side.
6. Safe legacy snapshots adopt the current pair without writing either side. One-sided legacy changes
   import/push through the new engine; ambiguous state shadows. Migration is idempotent and crash-safe.
7. The sanitized slice-11 offline full/delta regression is RED against the pre-Sprint behavior and
   GREEN after: local edit → push → echo tick yields `None` and byte-identical file. The Notion-created
   control remains pristine and quiet on its next tick.
8. Real-Notion scratch acceptance repeats that exact existing-record watchdog sequence, then makes a
   genuine Notion body edit which imports exactly once without collateral body/frontmatter changes.
9. Seeded mutation/fidelity tests cover headings, H1 omission, blank lines, escapes, emphasis/links,
   nested lists, tables, quotes, code, repeated sections, and ambiguous alignments without masking
   semantic word/punctuation/structure edits.
10. Focused tests, the complete isolated backend suite, coverage gap check, `dydo check`, and the
    merged Sprint audit pass. Live tests must actually execute, not skip, before issue 0309 closes.

**Questions & answers**

- **Why not store one normalized body/hash?** Local and Notion are different representations, and
  native Markdown is documented lossy. Two bases remove the need to predict an echo.
- **What is compared?** Current repo bytes to `localBody`; current stable-cleaned Notion Markdown to
  `externalBody`. Semantic normalization participates only in AST alignment/equivalence.
- **How are Notion edits imported without reformatting the file?** Diff external-base→external-current,
  align its base nodes to local-base nodes, and graft only uniquely mapped changed spans onto the
  current repo source. Copy every untouched source/interstitial span verbatim.
- **What if repeated sections make the mapping non-unique?** Treat only affected ambiguous regions as
  a genuine conflict and use the spine shadow. The shadow contains the full local/external candidates
  inside the existing endpoint merge sentinels, so marker-free still means human-resolved; do not choose
  an LCS path arbitrarily.
- **How is frontmatter protected?** File application is span-based. A body-only result replaces only
  the body span; field changes patch only changed scalar lines while preserving untouched lines/order/
  comments. Full `SyncDocFile.Render` is reserved for new files and conflict artifacts.
- **How is a successful remote write observed?** The adapter returns an immediate native-Markdown
  read-back receipt. The runner journals intent before mutation and commits the dual base only with a
  receipt; restart resolves a pending intent against the prior bases and current representations.
- **How is a create recovered before it has a page id?** Its journaled UUID is sent in the provisioned,
  engine-reserved `dydo-write-id` property in the initial request. Retry/restart adopts exactly one row with that
  UUID, never a title match; duplicates shadow and zero matches permit one create attempt.
- **How is a resolved structured conflict promoted?** Promotion writes the resolved file and a durable
  resolution intent whose prior external projection is the current page read. It does not advance either
  base. The ordinary runner pushes that intent, reads the receipt, then commits both bases and clears the
  shadow; failure leaves the intent recoverable.
- **How does v1 migrate?** Without writes. Semantic equivalence to the v1 base/known legacy echo adopts
  the current pair; a unique one-sided delta reconciles; uncertainty shadows. No silent winner.
- **Does this change field semantics?** No. Existing field normalization/merge remains, but the file
  writer applies only actual field deltas so a body event cannot inject board echoes. `SyncUpsert.WriteBody`
  explicitly separates property-only from body-carrying mutations; `Body` remains non-null so empty-body
  replacement is distinct from “do not write body.”
- **What about child pages under a spine row?** A page with none uses destructive replacement. A page
  with children uses `allow_deleting_content:false` and appends the exact `<page>` tags reconstructed
  from `GetChildPages`; stable-cleaning strips those tags from receipts/canonical content.
- **What is the performance bound?** Align recursively per sibling sequence with unique-anchor/patience
  partitioning and bounded Myers fallback; never allocate a whole-document quadratic matrix. An
  exceeded/ambiguous bound shadows with a diagnostic rather than degrading to a destructive merge.
- **What authorizes closure?** Criterion 8's live scratch run plus all offline gates and the merged
  audit. A skipped live test is not evidence.

## 2. Prior art

- DR 025 supplies the bidirectional/base-snapshot contract; DR 035 supplies native Markdown transport,
  stable cleaning, truncation/child safety, and the permanent shadow invariant. DR 043 replaces only
  their unsafe single-body comparison model.
- `Sync/Notion/DocsPageAdapter.cs` and `INotionClient.GetPageMarkdown`/`UpdatePageMarkdown` are the
  landed native API path to reuse. Its exact-echo fake is not sufficient evidence and is augmented,
  not copied as a fidelity model.
- `DocsMarkdownNormalizer.CleanForPersist` is adopted only for documented unstable export artifacts.
  Its `Normalize` output is rejected as snapshot/persisted content.
- `NotionBlockConverter`/`NotionLegacyEcho` are retained only as bounded v1-migration recognizers;
  they are rejected for current body transport, comparison, or merging.
- `ThreeWayTextMerge` remains valid for same-representation plain text consumers but is rejected for
  cross-dialect projected bodies.
- Markdig source spans and the converter's sibling-span clamping are adopted for syntax boundaries.
  Arbitrary first-LCS alignment is rejected because repeated sections caused the reported duplication.
- Existing `SyncRunner` per-item assigned/deleted/empty-body bookkeeping and `SyncDocFile` atomic sibling
  rename are adopted for receipt durability and crash-safe canonical writes.

## 3. Design

Introduce a small generic projection model in `Sync/Projection/`: `DualBodyBase`,
`ProjectedBodyResult`, `ProjectedBodyConflict`, and `ProjectedMarkdownMerge`. Intent/receipt contracts
live under `Sync/` beside the runner/adapter seam. `ProjectedMarkdownMerge`
parses source-spanned Markdig trees, recursively aligns base representations, detects non-unique
alignments, and returns either a patched local body or a structured conflict reason. It does not know
about Notion or files.

Snapshot v2 is discriminated per object (not per file), so safe v2 objects and unresolved v1 objects may
coexist. It adds the dual base and optional pending intent while retaining v1 `body` deserialization.
`BaseSnapshotStore` exposes migration-aware state and saves intent before adapter mutation. Extend the
adapter application seam with a default receipt-capable method so non-projected adapters retain current
behavior; Notion overrides it and returns stable-cleaned native Markdown read-backs per body upsert.

`SyncRecord` carries an explicit `Complete`/`Truncated` body-read status; projected reconciliation never
interprets an incomplete export as content. `ReconcileEngine` receives a dual base for projected adapters and keeps the existing path for identity
adapters. It calculates field and body decisions independently. `SyncRunner` builds a span-aware repo
patch, routes structured projection conflicts through its existing shadow resolver, journals intended
external mutations, applies them, then commits receipts. Full and delta runners share this exact path.

`SyncDocFile` gains a patch operation over the original text: body-only replaces the located body span;
field-only replaces/adds/removes only changed frontmatter scalar lines; combined changes compose before
one atomic write. New files and shadows still use deterministic `Render`.

`NotionSyncAdapter` switches body reads/writes/creates to native Markdown and stops recursive block reads
for body conversion. `SyncUpsert.WriteBody` makes property-only updates skip the body endpoint. A
body update may perform one structural child-page enumeration; a child-bearing page reconstructs and
re-appends Notion's `<page>` tags before a child-safe write. A journaled operation UUID is written through
the hidden, provisioned `dydo-write-id` property and is the only create-recovery key. The fake gains explicit request/read/write/child-enumeration counters and configurable server echo transforms
so tests can model H1 removal, escapes, blank collapse, indentation, truncation, and write/read failure
without claiming those transformations are the live contract.

Legacy migration runs lazily per snapshot object before normal reconciliation. It never mutates either
side during classification. Diagnostics name the local id, migration/projection reason, canonical path,
and shadow path without logging body content or secrets.

Hazards: snapshot mutation is crash-sensitive; repeated Markdown nodes make alignment ambiguous; a page
PATCH can land before read-back fails; full and delta paths can diverge; fake echo tests can become
self-fulfilling; live tests mutate external state. Rollback is the Sprint commits in reverse order plus
restoring snapshot-v1 reading. Rollback deliberately leaves the inert `dydo-write-id` rich-text column in
remote data sources—removing/retyping remote schema is destructive and the adapter ignores the property
when this feature is absent. Live tests use uniquely named scratch children/databases and archive them in
teardown, never the configured production board.

## 4. Slice map

| # | slice file | files touched / owned seam | deps | gate |
|---|---|---|---|---|
| 1 | `notion-body-fidelity-1-projection-core` | new `Sync/Projection/*.cs`; new `DynaDocs.Tests/Sync/Projection/*.cs` | — | projection focused tests |
| 2 | `notion-body-fidelity-2-snapshot-receipts` | `Sync/SyncSnapshot.cs`; `Sync/SyncSnapshotFile.cs`; `Sync/BaseSnapshotStore.cs`; `Sync/ISyncAdapter.cs`; `Sync/SyncRecord.cs`; `Sync/SyncUpsert.cs`; `Sync/SyncChangeSet.cs`; `Serialization/DydoJsonContext.cs`; new read-status/receipt/typed-intent types; new snapshot tests | 1 | snapshot/receipt focused tests |
| 3 | `notion-body-fidelity-3-engine-and-file-patch` | `Sync/ReconcileEngine.cs`; `Sync/ReconcileResult.cs`; `Sync/SyncRunner.cs`; `Sync/SyncDocFile.cs`; `Sync/Notion/NotionSpineDelta.cs`; `DynaDocs.Tests/Sync/Notion/NotionSpineDeltaTests.cs`; projected-reconcile/file-patch tests | 1,2 | engine/file/delta safety tests + existing sync tests |
| 4 | `notion-body-fidelity-4-spine-native-markdown` | `Sync/Notion/NotionSyncAdapter.cs`; `Sync/Notion/NotionPropertyMapper.cs`; `Sync/Notion/Provisioning/NotionProvisioner.cs`; `Sync/Notion/Provisioning/NotionSchemaDrift.cs`; `Sync/Notion/NotionSpineSync.cs`; `Sync/Notion/NotionSpineDelta.cs`; `Sync/SyncRecord.cs`; `Sync/SyncRunner.cs`; `Sync/ReconcileResult.cs`; `Templates/sync-model.template.json`; `dydo/_system/sync-model.json`; fake + adapter/runner/full/delta/model/provisioner/schema-drift tests | 2,3 | adapter + production wiring + recovery + schema + client gates |
| 5 | `notion-body-fidelity-5-migration-and-watchdog` | `Sync/Notion/NotionSpineSync.cs`; `Sync/Notion/NotionSpineDelta.cs`; new sanitized fixture; new full/delta/migration tests | 3,4 | full/delta/migration focused tests |
| 6 | `notion-body-fidelity-6-fidelity-and-live-proof` | new fidelity/mutation/live tests; `dydo/reference/notion-sync.md`; issue 0309 resolution | 5 | live test executes + full suite/coverage/check |

## 5. Ordering & isolation

Run all six Slices serially. Each consumes contracts from the previous Slice and every gate compiles the
same solution. Slices 3 and 5 intentionally overlap `NotionSpineDelta.cs`: Slice 3 makes the unhandled
projection/cursor-retention contract end-to-end safe; Slice 5 later enables projected mode and adds
migration/watchdog integration on that reviewed base. Slice 4 intentionally extends Slice 2/3's neutral
`SyncRecord`/runner seam so the adapter's reserved operation UUID can bind a pending Create after restart;
it also owns the first safe production opt-in and reserved-schema preflight in `NotionSpineSync`/Delta.
Slice 5 intentionally revisits those files for legacy migration/watchdog integration. The Slices are
serial, and Slice 4 must preserve all prior projected/identity tests. Do not use parallel worktrees: a red shared build
would strand later lanes, and the current tree already contains unrelated dirty work that must remain
untouched.

Each Slice receives one code-writer pass, then a fresh reviewer using the code target; findings return to
the same Slice until PASS. Commit only that Slice's declared paths with explicit path literals. After
Slice 6, review the composed committed diff with the merge-sprint target, record its PASS/FAIL in this
root, and only then mark the Sprint done and advance Campaign waypoint W3.

## 6. Watch-outs

- Never use normalization output as `localBody`, `externalBody`, a repo write, or a merge base.
- Never infer “no external edit” by comparing Notion to current repo; compare it to its own receipt.
- Do not let a default adapter receipt silently claim an external projection; projected adapters must
  return an observed body or leave the intent pending.
- Do not clear pending state before the snapshot containing both confirmed bases is durably saved.
- Do not recover creates by title, timestamp, or best-effort content matching. Only one exact pending
  `dydo-write-id` match is adoptable; duplicate matches are a conflict.
- Do not treat `Truncated` as an empty/short body or permit it to advance either projection.
- Do not use a single arbitrary LCS result in repeated sections. Detect non-uniqueness at affected
  boundaries and shadow.
- Do not rewrite full frontmatter for body-only changes or full body for field-only changes.
- Do not weaken semantic comparison so word, punctuation, order, link-target, checkbox, or code changes
  disappear as “dialect.” Mutation tests must prove each remains visible.
- Do not treat a skipped live test as green. Verify both required environment variables are present
  without printing them, and fail the Slice gate if the test did not execute.
- Never point live tests at the configured production parent. Use `NotionLiveTestBase.ChildPageId`,
  unique child databases/pages, and best-effort archive teardown.
- Preserve unrelated dirty files; no blanket staging, formatter, reset, checkout, or stash.
