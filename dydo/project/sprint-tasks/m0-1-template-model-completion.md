---
title: m0-1 Template Model Completion
blocked-by:
due:
needs-human: false
priority: High
sprint: m0-spine-types-completion
status: ready
work-type: feature
area: backend
type: context
---

# m0-1 Template Model Completion

Add the three missing object types to `Templates/sync-model.template.json` and encode the DR-039
sprint status vocabulary — one model edit covering DR 040 §§2–5. Follow the existing JSON shapes
in the file exactly (Issue is the pattern for a partitionless record type with selects).

## Changes — `Templates/sync-model.template.json`

**New type `Decision`** — `dir: project/decisions`, `notionTitle: Decisions`, icon ⚖️:
- `title` (title); `status` select `[proposed, accepted, deprecated, superseded]` (align with
  `Frontmatter.ValidStatuses` — includes `deprecated` even though the corpus has none today);
  colors: proposed gray, accepted green, deprecated brown, superseded purple.
- `area` select `[backend, general, platform, project, reference, understand]` — note `platform`,
  present in 3 decision records, is NOT in the Issue area enum; this type gets the wider enum.
- `date` (date); `participants` (rich_text).
- Views: `All` table sorted by date descending; `Accepted` table filtered status=accepted;
  `Board` grouped by status.

**New type `Changelog`** — `dir: project/changelog`, `notionTitle: Changelog`, icon 📦:
- `title` (title); `date` (date); `area` select (same 6-value enum as Decision).
- **No status property** (DR 040 §2 — an entry is inherently shipped) and no `folders` map.
- Views: `All` table sorted by date descending; `By Area` board grouped by area.
- The date-nested `YYYY/YYYY-MM-DD/` tree pools recursively for free; `_`-prefixed day hubs are
  skipped by the loader — no code change.

**New type `Pitfall`** — `dir: project/pitfalls`, `notionTitle: Pitfalls`, icon ⚠️:
- `title` (title); `area` select (6-value enum); `date` (date).
- Views: `All` table. (Dir is empty in this repo; the type ships by default — DR 040 §4.)

**Sprint status vocab** (DR 040 §5): options become
`[planning, plan-review, active, audit, done, escalated]`; colors: planning gray, plan-review
purple, active blue, audit yellow, done green, escalated red. `gate-result` and all other Sprint
properties unchanged. Update the Sprint views whose filters name old values (`Active` filter is
still valid; check `Board` groupBy is unaffected).

**Corpus conformance for the vocab change (same slice, tiny):** map old status words on the two
existing sprint records + this sprint's own record if needed: `planned → planning`,
`in-review → audit`; `active`/`done`/`escalated` unchanged. (`project/sprints/notion-sync.md` is
`active` — untouched; check `runtime-slim.md`.)

## Constraints

- New types carry NO formula/rollup properties — keeps them clear of the live
  formula-references-formula constraint (reference/notion-sync.md #1).
- Do not touch the live `dydo/_system/sync-model.json` (guard-off-limits; bridged later by m0-4's
  command at smoke time).

## Tests / gates

- Update whatever asserts on the template's type set: `DynaDocs.Tests/Sync/Model/SyncModelLoaderTests.cs`,
  `DynaDocs.Tests/Sync/Notion/NotionSchemaDriftTests.cs`, `NotionProvisionerTests.cs` (grep for
  hardcoded type lists/counts before assuming).
- Full test suite green; `gap_check --force-run` (Sync/ touched).

## Success criteria

Template parses via `SyncModelLoader`; 10 object types declared; sprint vocab matches DR 039 §3;
no live-model edit; suite green.
