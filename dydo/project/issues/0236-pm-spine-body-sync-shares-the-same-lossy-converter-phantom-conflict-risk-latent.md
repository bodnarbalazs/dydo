---
title: PM-spine body sync shares the same lossy-converter phantom-conflict risk (latent, low-exposure)
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: open
work-type: 
id: 236
type: issue
found-by: review
date: 2026-07-08
---

# PM-spine body sync shares the same lossy-converter phantom-conflict risk (latent)

Surfaced while root-causing the docs-mirror corruption
([0235-docs-mirror-bidirectional-body-sync-corrupts-repo-with-phantom-conflicts-from-lossy-converter](./0235-docs-mirror-bidirectional-body-sync-corrupts-repo-with-phantom-conflicts-from-lossy-converter.md)).
The PM-spine sync (`NotionSpineSync` → `NotionSyncAdapter` → `SyncRunner`/`ReconcileEngine`) reconciles
each DB row's **page body** through the **same** lossy `NotionBlockConverter` round-trip and the **same**
raw-text 3-way merge that manufactured phantom conflicts in the docs mirror. So the spine carries the
**same latent bug**: a lossy Notion round-trip of a row body can be misread as an external edit and,
in the two-sided case, produce a conflict written back into the canonical repo file.

**Why it hasn't bitten yet (low exposure):** spine row bodies are typically short and structurally
simple (a brief, a few lines), so the round-trip drift is small or nil and the phantom conflict rarely
triggers — unlike the docs mirror's large, formatting-rich prose (guides, changelog) that drifts every
time. It's a latency-of-detection difference, not a difference in kind.

**Relation to 0235.** 0235's fix is scoped **adapter-specific** by default (normalize inside the docs
adapter) to avoid perturbing the live spine mid-flight. This issue exists so the spine's shared exposure
isn't lost when 0235 closes: whether the normalization fix should be lifted to the **engine level**
(`ReconcileEngine` compares/stores base in normalized space for all adapters) — which would protect the
spine too — is a **scheduled decision with Brian at the table** (he owns the spine), not mid-sprint
scope creep. The 0235 **part-A safety rail** (never write conflict markers into a canonical file), if
implemented at the engine chokepoint as a pure refuse-on-markers backstop, would already protect the
spine as a no-op-in-normal-operation guard.

## Reproduction (hypothetical, not yet observed)
1. A spine row whose body contains constructs the converter round-trips non-idempotently (a table, a
   nested list), edited so repo and the drifted Notion read differ from base on the same lines.
2. `dydo notion sync` → the raw 3-way merge writes conflict markers into the canonical row file.

## Resolution
Fix landed engine-level (the option 0235 flagged): `ReconcileEngine.Equal` compares spine bodies in
**normalized space on both sides** via `NotionSyncAdapter.NormalizeBody` (`FromBlocks∘ToBlocks`), so a
dialect-only round-trip difference (escapes, whitespace, list markers) can no longer be misread as an
external edit — killing the phantom-conflict class at the compare. Delivered across the sprint:
- **ns-6/ns-7** rebuilt `NotionBlockConverter` (Markdig AST; setext-off; tables/quotes/nested lists;
  deterministic `[!missing]` markers for unsupported blocks) so the round-trip is a **fixed point** on every
  real record body — pinned by `NotionBodyFixedPointTests` (idempotency sweep + the pre-ns-7 migration sweep).
- **ns-8** confirmed the phantom class is closed for spine bodies and locked it with adapter-level regression
  tests through the real converter + `FakeNotionClient`: `BodyDialectOnlyDifference_NoDriftNoConflict_AcrossTwoPasses`
  (dialect-only echo → no-op both passes, file byte-identical, no markers) and
  `DualBodyEdit_SameLine_Conflicts_ThroughLossyRoundTrip` (a genuine two-sided edit still conflicts — the
  normalization does not over-mask). No snapshot-canonical/hash refactor was needed: storing the base body
  canonical would perturb the raw-body `ThreeWayTextMerge` and the ns-7 migration shim for no reachable gain
  (non-idempotent bodies are ruled out by the fixed-point sweep).

**Status kept open** pending **ns-10**'s live pass: `sync → no edits → sync` must be verified a no-op against
real Notion (only fake-verified so far). Close there with live evidence.
