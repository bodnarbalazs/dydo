---
area: general
type: context
status: open
date: 2026-07-06
---

# Notion Board (DR 029/030) — Follow-ups Backlog

Deferred items surfaced during the DR 029/030 board sprint and its five-round inquisition QA loop
(waves 4a–8, commits fea7915…12c45cc + the wave-8 finalization). The board schema, attention layer,
and relation-sync correctness landed and converged; these are the deliberately-out-of-scope tails.

## A. Runtime → board bridge (its own sprint — design round first)

The largest item, already scoped in `dydo/agents/Charlie/brief-runtime-board-bridge.md` and recorded in
DR 030's implementation notes. The four `needs-human` detectors write the runtime task tracker
(`dydo/project/tasks/`), but the board syncs `sprint-tasks/` + `issues/` — no bridge. Also: Sprint
`gate-result` has no machine writer; `triage` is not the agent-filed default; escalation-path
flag-writes are deferred. **Needs a co-thinker design round with Balazs, then a run-sprint.**

## B. Re-provision / model-evolution robustness (retro-provisioning story — needs live Notion)

The rare recovery path where a Notion database is deleted, unshared, or the sync model's shape changes.
Wave 7/8 closed the *data-safety* layer (no mass-delete of repo files; a parent re-mint preserves and
re-pushes child relation values instead of silently clearing them). The **deep layer remains**, and it
cannot be verified with fixtures alone — it requires a live-Notion smoke:

- **Child relation-schema re-point on parent re-mint.** A child's relation property schema is pinned to
  the parent's `data_source_id` at child-create time. A parent-only re-mint does not re-point it, so the
  wave-8 re-push writes new-parent page ids into a relation whose schema still targets the dead data
  source. Worst case today is a *loud, non-destructive* wedged child sync (repo data preserved) — but full
  convergence needs a PATCH of every child relation property to the new `data_source_id` (the
  `UpdateDataSource` machinery already exists), plus **reverse-relation / rollup re-synthesis** on the new
  data source (a re-minted parent carrying rollups re-runs `AddRollups` against reverse properties that only
  a child dual-property relation against the NEW data source creates).
- **Existing-board schema evolution.** ✅ **Landed (ns-11, half B).** An already-provisioned, still-valid
  type now runs an additive-only pass (`NotionProvisioner.ApplyModelAdditions`, driven from
  `NotionSpineSync` alongside the drift check, sharing one live-schema read): it creates missing model
  properties, appends missing select options by name (existing options and their Notion-owned colors left
  untouched; name match is case-insensitive), and renames
  the data source title when `notionTitle` changed — all via `UpdateDataSource`, never a re-mint. Strictly
  additive: retype/delete drift is still only warned (a `--prune` decision). NOT covered: ordering-aware
  addition of an interdependent rollup/formula (a single PATCH, no post-pass sequencing) and the
  relation-repoint case below — both still need the live-Notion smoke.
- **Prerequisite:** one manual live-Notion provision smoke to verify (a) whether Notion accepts a relation
  write whose pages belong to a data source the property does not target, and (b) `dual_property.synced_property_name`
  honored-at-create, before relying on any of the above in production.

## C. Smaller banked items (from the QA loop, non-blocking)

- `dydo notion sync --prune` has no `--dry-run` preview (CheckDrift is skipped under dry-run) and no
  command-level test.
- `SyncChangeSet.IsEmpty` is misleading now that engine-computed refreshes make every tick non-empty
  (zero consumers today — delete or rename to exclude refreshes).
- `EngineComputedInSync` compares every engine-computed property to one value — fine while `last-activity`
  is the only one; needs rework before a second engine-computed property.
- Live formula-syntax smoke: the health/stale/attention formula expressions and date-returning rollups are
  opaque strings to the fake — verify against real Notion once before relying on the model.
- Remaining `FakeNotionClient` vs real-Notion fidelity gaps beyond the `EchoEmptyRelations` knob added in
  wave 6 (e.g. relation-target validation, which would have caught item B's schema-repoint gap in fixtures).
- `CreateToRepo` for a board-originated new doc referencing an unmapped page can still write RenderRelation's
  raw page-id fallback into a fresh repo file — benign (base records it, no churn, deletes propagate), the
  last path where a raw Notion id can reach frontmatter.
- Watchdog task-mirror TOCTOU microsecond window (documented in code) and the `IsRelationKey` empty-probe
  adapter coupling (documented in its summary) — report only if a production configuration misfires.

## D. Harness anomalies (report to Brian — run-sprint machinery, not board code)

Three distinct failures observed in the run-sprint/workflow harness during this sprint, all worth folding
into Brian's "Workflow args re-stringification" hardening: (1) args-collapse — a multi-slice `args` array
delivered stringified, collapsing slices into one in-tree `slice-1` (recurred even after the defensive
`normalizeSlices` JSON.parse); (2) `raiseHand:false` dropped from a worker's structured payload, forcing a
fake "serialization probe" escalation; (3) StructuredOutput retry-cap (×5) killing a workflow after the
worker had already completed and applied its in-tree work. Mitigation that worked: single-slice waves with
plain-prose briefs, and salvaging completed in-tree work from the journal when the workflow died.

## Migration shim removal (issue 0299 F13, deferred 2026-07-22)

`NotionLegacyEcho` (frozen ns-6-era converter), `NotionSyncAdapter.IsStaleConverterEcho`, the `ISyncAdapter`
default member, and `ReconcileEngine.ApplyMigrationShim` are dead-on-arrival for THIS repo — ns-10's live run
confirmed this board is fully re-rendered under the new converter (dry-run zero across 397 records). REMOVAL is
deferred, not done: remove the whole shim once every downstream install has synced at least once post-2.2. Open
question to resolve first: the frozen echo reproduces the ns-6 Markdig converter (never shipped — ns-6/ns-7 are
both inside v2.1.0..HEAD), so it targets only dev-window boards; a v2.1.0-era board was pushed by the older
LINE-based converter the shim cannot recognise anyway, so it may already be safe to delete. Confirm the downstream
board lineage, then delete per the pins.
