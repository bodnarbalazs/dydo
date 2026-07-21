---
title: ns-12 Slice Board Display Rename
blocked-by: ns-11-model-regen-and-additive-provision, ns-10-live-verify-and-close
due:
needs-human: false
priority: Low
sprint: notion-stabilization
status: done
work-type: chore
area: backend
type: context
---

# ns-12 Slice Board Display Rename

The record type was renamed SprintTask → Slice on disk (`dydo/project/slices/`, sync-model `"type": "Slice"`), but the Notion board still displays **"Sprint Tasks"** (`notionTitle` in `Templates/sync-model.template.json` and `dydo/_system/sync-model.json`, ~:99-101) — deliberately deferred until a rename could land without a reset. ns-11's additive re-provisioning provides exactly that path.

## Task

1. Change `notionTitle` from `"Sprint Tasks"` to `"Slices"` in `Templates/sync-model.template.json`; propagate to `dydo/_system/sync-model.json` via the ns-11 regeneration path (verify the hash-tracked update actually flows — this is its first real consumer).
2. Sweep for other display-name references: view definitions in the model, `dydo/reference/notion-sync.md` prose, any test pinning "Sprint Tasks".
3. Live-verify with the ns-9 harness (env vars are available once ns-10 has run — this slice is blocked on ns-10 for exactly that reason): existing provisioned board's data source renames in place, no re-mint, relations intact.

## Files

- `Templates/sync-model.template.json`, `dydo/_system/sync-model.json`
- `dydo/reference/notion-sync.md`
- Tests pinning the display title (grep `"Sprint Tasks"` under `DynaDocs.Tests/` and `Sync/`)

## Success criteria

- Fake test: provisioned type with old title + new model → title-update call issued, no re-mint.
- Live check recorded (title renamed in place on the scratch board).
- No remaining `"Sprint Tasks"` display strings outside historical records; full ratchet green.
