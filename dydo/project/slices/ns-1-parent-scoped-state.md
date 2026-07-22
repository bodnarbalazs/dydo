---
title: ns-1 Parent-Scoped Spine State
blocked-by:
due:
needs-human: false
priority: Urgent
sprint: notion-stabilization
status: done
work-type: bug
area: backend
type: context
---

# ns-1 Parent-Scoped Spine State

Fixes issue 0257. Spine sync state is project-scoped today: per-type base snapshots (`BaseSnapshotStore.PathFor(dydoRoot, "notion-<type>")`, used at `Sync/Notion/NotionSpineSync.cs` ~:206 and mint-delete ~:101) and `dydo/_system/.local/notion/provision.json` know nothing about which Notion parent page they belong to. Consequence: `dydo notion reset --parent-page <scratch>` archives the scratch board but deletes the REAL board's provision state and poisons its base snapshots — after which a normal sync can read external=null and mass-delete canonical repo records. A previous fix attempt was reverted after review found it left the snapshots project-scoped (see issue 0257's history — the reviewer must re-check exactly that residue). The docs mirror already solved this: `DocsTreeSync.SnapshotAdapterName` (`Sync/Notion/DocsTreeSync.cs:28`) embeds `hash8(parentPageId)`.

## Task

1. Scope all spine state by parent page id, reusing the docs mirror's `hash8` convention:
   - Base snapshot adapter names: `notion-<hash8(parent)>-<type>`.
   - `provision.json` → either per-parent file (`provision-<hash8>.json`) or a keyed map inside one file — pick per-parent files to match the snapshot convention.
2. **One decision point.** The legacy-vs-scoped/override-vs-configured resolution lives in ONE shared function used by BOTH sync and reset (issue 0257's CRITICAL 2: the reverted attempt let `sync --parent-page <scratch>` adopt real-board state because sync and reset resolved state independently). Nothing else computes state paths.
3. One-time migration: on first spine run for the **configured** parent, if legacy project-scoped files exist (`provision.json`, `notion-<type>.json`) and no parent-scoped ones do, rename them into that parent's scoped names and log one line per migrated file. A `--parent-page` override **equal to the configured parent counts as non-override** (issue 0257 MEDIUM 3 — otherwise resetting the configured board by explicit id would orphan legacy state and re-mint a duplicate); an override to any *other* parent starts clean and never migrates.
4. `NotionReset` operates only on the state for the parent it resolved via the shared decision point; resetting a scratch parent leaves every other parent's provision state and snapshots untouched.
5. Update `dydo/reference/notion-sync.md` state-files section to the new names.

## Files

- `Sync/Notion/NotionSpineSync.cs` (snapshot naming, mint-delete)
- `Sync/Notion/Provisioning/NotionProvisioner.cs` (+ state records) — provision persistence
- `Sync/Notion/NotionReset.cs` — reset scoping
- `Sync/BaseSnapshotStore.cs` — only if the naming needs a hook; prefer changing callers
- Tests: `DynaDocs.Tests/Sync/Notion/` — `NotionSpineSyncTests`, `NotionProvisionerTests`, `NotionResetTests`

## Success criteria

- New test: reset against parent B leaves parent A's provision state and snapshots byte-identical.
- New test: legacy state migrates once for the configured parent; a second run does not re-migrate; an override to a different parent never migrates; an override **equal to** the configured parent behaves exactly like no override.
- **Repo-survival assertion (the 0257-mandated blind-spot test):** the full scratch-reset-then-sync sequence ends with the original repo records intact — e.g. a seeded `c1.md` still exists and no `<page-id>.md` files appear anywhere under `dydo/project/`.
- New test: `sync --parent-page <scratch>` resolves scratch-scoped state (never the configured board's) via the same shared decision point reset uses.
- Existing suite green; full ratchet (build 0 errors → suite 0 failed → `python DynaDocs.Tests/coverage/gap_check.py --force-run` all pass).
- Issue 0257 updated with the resolution (leave open until ns-10's live verification).
