---
title: m0-6 Live Smoke (Model Regen + Provision)
blocked-by: m0-1-template-model-completion, m0-2-decision-title-backfill, m0-3-changelog-conformance, m0-4-notion-model-update-command, m0-5-docs-reconciliation
due:
needs-human: true
priority: High
sprint: m0-spine-types-completion
status: ready
work-type: spike
area: backend
type: context
---

# m0-6 Live Smoke (Model Regen + Provision)

Human-gated (needs balazs's token; `FakeNotionClient` cannot catch the live-only constraints —
reference/notion-sync.md). Runs after all M0 slices merge. **Pairs with Olivia's M1-S6 as one
live session** — same scratch workspace, same ceremony; coordinate through Adele.

## Steps

1. `dydo notion model-update` — the new command bridges template → live model. This dogfoods the
   0252 fix; expect the diff to show the 3 new types + sprint vocab. NO hand-copy this time.
2. `dydo notion reset` (scratch workspace) or additive `dydo notion sync` — decide per whether
   the M1 smoke shares the board; reset is the clean path.
3. Verify:
   - `Decision`, `Changelog`, `Pitfall` (+ `Task`, `FutureFeature`) DBs provision without schema
     400s; provisioner errors are tagged with type/property if not.
   - Changelog pools all ~670 rows with **no stem crash**; date-nested tree walks correctly.
   - Row titles are non-blank (the backfills worked) — spot-check a decision, an old changelog
     entry, a renamed collision file.
   - Sprint DB carries the DR-039 vocab; existing sprint rows land on valid options.
   - Docs mirror shrinks: decisions/changelog/pitfalls pages gone from the mirror by
     construction. Expected page math: ~831 → ~119 after M0 alone; → ~40 once M1's moves land.
   - Live-constraint checklist: no formula 400s (new types have none), no 2000-char run failures
     on long decision bodies.
4. Record findings; any failure → issue records, route to Adele.

## Success criteria

All 10 DBs live; changelog pools clean; titles render; mirror math matches; 0252 flow proven
end-to-end.
