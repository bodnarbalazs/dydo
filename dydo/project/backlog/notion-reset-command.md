---
area: general
type: context
status: open
created: 2026-07-06
created-by: Adele
origin: balazs asked whether a Notion "reset (wipe then recreate)" exists; it does not — `dydo notion sync` is a forward, create-only reconcile.
related-decisions: [025, 029, 034]
---

# `dydo notion reset` — wipe-then-recreate the board from the sync model

> **Scheduling (balazs, 2026-07-07):** slated for the **next sprint**, after the
> PM-record-taxonomy migration lands — *not* folded into the current migration. Commit the
> current work clean first, then build this on top.

## Problem

`dydo notion sync` is a **forward, create-only reconcile**, not a reset:

- Creates missing databases/views; reuses existing databases by **stored id**
  (`provision.json`), so it never duplicates — idempotent in the "don't double-create" sense.
- `--prune` deletes **schema drift** (properties / select options present in Notion but
  absent from the model). `--dry-run` previews the plan.
- **But it is create-only for views and titles.** A deleted view is **not** restored; an
  existing database is **not** renamed when `notionTitle` changes; reordered options /
  manual layout edits are not reverted (only `--prune`'d drift is). See
  `NotionProvisioner.AddViews` ("created on a fresh mint, not re-created on a reuse tick")
  and `Create` (title set only at creation).

So if a user (or operator) messes up the board — deletes a view, renames a column,
reshuffles things — re-running `dydo notion sync` will **not** put it back. There is no
clean "make Notion exactly match the model again" button.

## Why a reset is genuinely different (and useful)

A reset = **wipe then recreate**: archive/delete the tracked databases and re-provision
fresh from the sync model, guaranteeing the live board matches the model regardless of
manual mess. Distinct from reconcile because it discards live state instead of preserving it.

Footgun a real command must handle: you **cannot** just delete `provision.json` to reset —
that orphans the old databases (loses their ids) and the next provision creates duplicates.
A proper reset must delete/archive the **tracked** databases (ids from `provision.json`)
**and** clear state, then re-provision.

## Sketch

- `dydo notion reset [--dry-run] [--archive]` (or a `--reset` flag on `notion sync`).
- Confirm destructively (it deletes board data); default to **archive** (Notion trash) over
  hard-delete, mirroring the "never hard-delete, archive" pattern used elsewhere (DR 034 §8).
- After wipe: clear `provision.json` tracked types, then run the normal provision path.
- Increased value post-DR-034: the taxonomy adds `Task` + `FutureFeature` and reshapes
  vocab; a reset is the clean way to re-materialize the board after such schema churn.

## Notes

- Live-only behavior — `FakeNotionClient` can't fully exercise it; needs a live smoke against
  a scratch workspace (same constraint the migration's provisioning slice carries).
- Ties to the broader create-only-reconcile limitation also noted on issue #215 and the
  DR-034 migration (existing views/titles don't auto-update).
