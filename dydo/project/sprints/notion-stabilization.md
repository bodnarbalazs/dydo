---
title: Notion Stabilization
campaign: dydo-2-0
end:
gate-result: pass (fresh-eyes plan review, 2 rounds, 2026-07-20)
seq: 8
start:
status: active
area: project
type: context
---

# Notion Stabilization

Take the built-but-fragile Notion sync (engine + adapter + provisioning, Sprint 7) to **stable and working**: close the data-safety holes, stand up a live-API smoke harness, harden the converter/client with ecosystem-proven logic, support model evolution without resets, and repurpose the watchdog into the background sync daemon.

---

## Specification

Done means:

1. **No sync operation can destroy repo data.** A reset scoped to one parent page cannot poison another parent's state (issue 0257); a reconcile that would mass-delete local records aborts loudly; spine conflicts never write markers into canonical PM files.
2. **Live-only constraints are testable.** A token-gated live smoke suite runs against a scratch parent page and covers every class the fake can't see (titles, chunking, formulas, languages, option rendering). Open issues 0290/0291/0278 are verified live and closed (or reopened with live evidence).
3. **The converter respects every Notion API hard limit** (100 blocks, 2-level nesting, 2000-char runs, 100 rich_texts per block) and phantom body conflicts from lossy round-trips are eliminated by canonical-render comparison. *(DEVIATION, reviewer-ratified cc547317 / issue 0236, recorded 2026-07-22 per issue 0299 F21: the mechanism landed as a normalized-space comparison in `ReconcileEngine.Equal` over `NotionBlockConverter`'s fixed-point round-trip — NOT hashing. The snapshot-canonical/hash tasks were skipped as already-satisfied/net-negative; no hashing exists in the code.)*
4. **The model can evolve without a reset**: `dydo/_system/sync-model.json` regenerates from the template (issue 0252), an existing live board gains new columns/options/titles additively, and the Slice board displays as **"Slices"** (retiring the "Sprint Tasks" display title).

**Stretch (does not gate the sprint):** `dydo watchdog` runs the sync loop — a background daemon reconciling on an interval (ns-13). If the sprint runs long, ns-13 moves to the next sprint.

Every slice gates on the full ratchet, exactly:

```
dotnet build DynaDocs.csproj            # 0 errors
dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj   # 0 failed
python DynaDocs.Tests/coverage/gap_check.py --force-run   # all modules pass
```

Locked decisions (no open questions):

- **Spine-first.** The docs mirror stays opt-in and out of scope except where engine-level work (fuse, shadow, converter) benefits it for free. Issues 0220/0221 are not in this sprint.
- **CI stays fake-backed.** The live harness is opt-in local tooling gated on `DYDO_NOTION_TEST_TOKEN` + `DYDO_NOTION_TEST_PARENT` env vars; absent vars ⇒ skipped, suite stays green.
- **Live testing "in prod" is sanctioned** (balazs, 2026-07-20): the live suite may target the real workspace token/parent — `dydo notion reset` and git restore are the recovery path; a scratch parent is optional, not required. The safety slices (ns-1, ns-2) still land before any live run.
- **Deletion fuse threshold:** abort reconcile when it would locally delete more than 5 records AND more than 20% of the type's tracked records, unless `--allow-mass-delete` is passed. Dev-mode git recovery exists (balazs, 2026-07-20), so the fuse is a loud tripwire, not a vault.
- **State scoping:** per-type base snapshots and `provision.json` become parent-scoped using the docs mirror's existing `hash8(parentPageId)` convention. Existing project-scoped state migrates one-time on first run for the configured parent.
- **Retry policy:** add 529 to the retryable set; 500/503 retried only for idempotent requests; a failed create re-queries the data source (by title/local key) and adopts a found page before re-creating.
- **Option colors are Notion-owned.** Sync manages option *names* (normalized casing) and never fights colors; drift detection ignores color.
- **Daemon defaults:** 60s interval (`--interval <seconds>` override, floor 15s); `dydo watchdog start|stop|run`; logs via the existing `Services/WatchdogLogger.cs` path. **No pid plumbing exists today** — ns-13 builds it: pid file at `dydo/_system/.local/watchdog.pid`; stale-pid detection by process-liveness probe (dead pid ⇒ overwrite); `start` spawns `dydo watchdog run` detached (`UseShellExecute=false`, `CreateNoWindow=true`); `stop` kills the pid and deletes the file. The daemon is the stretch lane and cuttable without failing the sprint.
- **Nested-body support requires a converter restructure.** `NotionBlockConverter` is deliberately line-based and flat, and the block DTOs cannot express children; nested structure (and therefore the depth-2 append algorithm) lands as one coherent slice (ns-6) that rebuilds block conversion on the Markdig AST, adds children-capable DTOs, and makes `AppendBlockChildren` return created block ids. Inline rich-text stays plain runs (no annotation support this sprint).

## Prior art

- [notion-oss-survey.md](../../reference/notion-oss-survey.md) — ecosystem survey; the depth-limit append algorithm, canonical-hash stability recipe, retry taxonomy, and spine conventions come from there. MIT sources only for ported logic.
- [notion-sync.md](../../reference/notion-sync.md) — our recorded live-API constraints and past smoke runs.
- Issues 0257 (reset scoping + mass-wipe), 0290 (titles), 0291 (chunking), 0278 (FutureFeature), 0252 (model regen), 0236 (lossy spine bodies); backlog `notion-board-followups.md` (re-provisioning), task `notion-sync-daemon.md` (daemon scope).

## Design

Four seams, one theme each:

- **State scoping (safety):** `BaseSnapshotStore` adapter names and `NotionProvisioner` state gain a parent-page dimension, mirroring `DocsTreeSync.SnapshotAdapterName` (DocsTreeSync.cs:28). `NotionReset` then naturally only touches its parent's world. The fuse lives in the generic reconcile path so both spine and docs benefit.
- **Live harness (verifiability):** a new test category (separate project or `[Trait("live")]` collection) building on the real `NotionClient` against a scratch parent. Each live test provisions into a uniquely named child page and archives it in teardown. The harness is also the manual smoke tool (`dydo notion sync` against the scratch parent remains the fallback).
- **Converter/client (correctness):** rebuild block conversion on the Markdig AST with children-capable DTOs, then apply the survey's algorithms — depth-2 payload cutting with iterative child appends, per-block rich_text enforcement, table padding, `[!missing]` markers on read — and make body-drift detection hash the canonical re-render instead of comparing lossy round-trips.
- **Model evolution (operability):** sync-model.json becomes a hash-tracked template output (`dydo template update` refreshes un-customized copies); the provisioner grows an additive update pass (new columns, new options, data-source title changes) so `notionTitle: "Slices"` lands without a reset.

## Slice map

| Slice | Lane | Blocked by | Scope |
|---|---|---|---|
| [ns-1-parent-scoped-state](../slices/ns-1-parent-scoped-state.md) | A | — | Parent-scope snapshots + provision state; one-time migration; fixes 0257 |
| [ns-2-deletion-fuse](../slices/ns-2-deletion-fuse.md) | A | ns-1 | Mass-delete abort in reconcile; `--allow-mass-delete` |
| [ns-3-trashed-db-remint](../slices/ns-3-trashed-db-remint.md) | A | ns-1 | `StillValid` detects `in_trash` databases and forces re-mint |
| [ns-4-spine-conflict-shadow](../slices/ns-4-spine-conflict-shadow.md) | A | ns-2 | Spine conflicts divert to shadow, never into canonical files |
| [ns-5-client-retry-nuances](../slices/ns-5-client-retry-nuances.md) | B | — | 529 retry; idempotent-only 5xx; create re-query-before-recreate |
| [ns-6-depth-limit-append](../slices/ns-6-depth-limit-append.md) | B | — | Nested block converter (Markdig AST + children DTOs) + depth-2 cutting + iterative appends |
| [ns-7-converter-hardening](../slices/ns-7-converter-hardening.md) | B | ns-6 | Per-block rich_text cap, table padding, quote fix, heading clamp, `[!missing]` markers |
| [ns-8-canonical-hash](../slices/ns-8-canonical-hash.md) | B | ns-7 | Canonical-render hashing kills phantom body conflicts (0236) |
| [ns-9-live-smoke-harness](../slices/ns-9-live-smoke-harness.md) | C | ns-1, ns-2 | Token-gated live test rig against a scratch parent |
| [ns-10-live-verify-and-close](../slices/ns-10-live-verify-and-close.md) | C | ns-9, ns-5..8 | Run live, verify/close 0290 0291 0278, record smoke results |
| [ns-11-model-regen-and-additive-provision](../slices/ns-11-model-regen-and-additive-provision.md) | D | — | 0252 regeneration + additive columns/options/title updates |
| [ns-12-slice-display-rename](../slices/ns-12-slice-display-rename.md) | D | ns-11, ns-10 | notionTitle "Sprint Tasks" → "Slices", live-verified |
| [ns-13-sync-daemon](../slices/ns-13-sync-daemon.md) | E | ns-10 | Watchdog repurpose: interval reconcile loop (cuttable) |

## Ordering & isolation

**Execution is SERIAL in the main working tree — one slice at a time, no parallel worktrees.** The lanes above express logical grouping and dependency, not parallelism: the hot files below are shared across lanes, so parallel worktrees would collide (and this repo's worktree pitfalls are documented in [orchestration-pitfalls.md](../../guides/orchestration-pitfalls.md)).

**Hot files (shared across lanes — the reason for serial execution):**
- `Sync/Notion/NotionSyncAdapter.cs` — ns-4 (conflict path), ns-5 (create recovery), ns-6 (append path), ns-8 (read/hash path)
- `Sync/Notion/Provisioning/NotionProvisioner.cs` (+ its tests) — ns-1, ns-3, ns-11
- `Sync/SyncRunner.cs` — ns-2, ns-4
- `dydo/reference/notion-sync.md` — ns-1, ns-4, ns-9, ns-10, ns-12

**Execution order:** ns-1 → ns-2 → ns-3 → ns-5 → ns-6 → ns-7 → ns-8 → ns-4 → ns-9 → ns-11 → ns-10 → ns-12 → ns-13(stretch). ns-4 runs after Lane B because both touch the adapter; ns-10 needs the human env vars and everything before it; ns-12 needs ns-10's live access; ns-9 must never run before ns-1+ns-2 (no live tooling before scoping + fuse exist).

Workers never commit; every slice lands through review; one slice one commit. The full ratchet (commands in the Specification) gates every slice; live slices additionally gate on the harness run.

## Seam audit (2026-07-21, overnight run)

Ten slices (ns-1..9, ns-11) landed as individually reviewed commits; a merge-sprint seam audit over v2.1.0..HEAD returned DEFECTIVE (5 findings: live-title seed hazard, 2 unrecorded live-check flags, docs fuse-result discard, missing fuse-x-shadow composed test, stale path strings), all fixed and re-verified SOUND. ns-10 (live verification, needs human env vars), ns-12 (rename, blocked by ns-10), ns-13 (daemon, stretch) remain. ns-10 tasks 5-7 carry the complete accumulated live-check list.

## Watch-outs

- **`.claude/worktrees/` contains ~48 stale copies of old source** — any grep/edit hitting `worktrees/**` is working on a corpse. Workers must scope to the repo root excluding `.claude/`.
- The fake (`FakeNotionClient`) treats formulas and bodies as opaque — a green fake test proves wiring, not Notion behavior. Anything touching payload shapes needs a live check in ns-10.
- Notion-Version is pinned at `2026-03-11` with the data-source model; do not bump the API version in this sprint.
- GPL boundary: Notional/EasyChris lineage is pattern-reference only — no ported code.
- The 0257 fix was attempted once and reverted after review caught a worse latent wipe; ns-1's reviewer must specifically re-check the failure mode recorded in the issue.
- `dydo notion sync` exits success on missing token/config (silent no-op) — live slices must fail loudly instead when the env vars are explicitly set but invalid.
