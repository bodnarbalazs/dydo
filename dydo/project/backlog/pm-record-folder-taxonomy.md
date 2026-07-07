---
area: project
type: backlog
status: done
date: 2026-07-06
---

# PM Record Folder Taxonomy + Status-Folder Cleanup — design brief

> **Design delivered → [DR 034 — PM Record Taxonomy](../decisions/034-pm-record-taxonomy.md)** (co-thinker
> round with Balazs, Dexter). Execution is the follow-on sprint
> [pm-record-taxonomy-migration](./pm-record-taxonomy-migration.md). Brian (sync-model) and Charlie
> (DR 033) coordinated.

Spun off during the Docs → Notion nested-page work ([[033-docs-notion-nested-page-mirror]]) when the
set of `dydo/project/` dirs that should be spine DATABASES (not browsable docs) kept growing
(decisions, future-features, pitfalls, …). Balazs wants the whole `dydo/project/` tree made **uniform
and clean across the stack** rather than patched dir-by-dir. Needs a **co-thinker round with Balazs →
a decision record → a migration sprint**. Coordinates with Brian (owns `sync-model`, the spine
object types) and Charlie (owns [[033-docs-notion-nested-page-mirror]], whose mirror auto-consumes the
result).

## The settled framing (from Balazs)

1. **`dydo/project/**` = PM records → spine DB object types.** `/project` means project management;
   almost everything under it is literally a *record*. Everything **outside** `project/`
   (`understand/`, `guides/`, `reference/`) = browsable reference docs → the DR 033 nested-page
   mirror. The mirror **derives its exclusion set from `sync-model`**, so once a dir is declared a
   spine DB it auto-excludes from the mirror — no double-maintenance.

2. **Folder-as-status is a semi-legacy wart to remove.** Some folders encode an item's *status /
   quality / horizon* rather than its record *type* — e.g. a top-level `backlog/` folder, and
   `future-features/`. "Backlog" is really a **marker/status property** on some record type, not its
   own category (many things may carry a `backlog` marker). These must collapse into a **property**
   on the appropriate record type so the model is uniform.

3. **Subfolder-as-property-partition is the good pattern — make it consistent.** Under a record type,
   a subfolder may partition items by a property *value* — e.g. `issues/resolved/`. This is handy and
   stays: the code **flattens and pools** all files under a record type for Notion and other logic, so
   the subfolder is pure physical organization. For consistency, **create sibling subfolders for the
   other values of that same property** (e.g. the non-resolved issue states) and apply the same
   convention to any other record type that uses (or should use) such partitions.

## What to produce

A decision record (the **PM record taxonomy** DR) that:

1. States the rule: `project/` = records (DBs), non-`project/` = docs; and the derive-from-sync-model
   consistency guarantee with the mirror.
2. **Classifies every current `dydo/project/` dir** — for each of `campaigns`, `sprints`,
   `sprint-tasks`, `issues`, `releases`, `decisions`, `future-features`, `pitfalls`, `tasks`,
   `backlog`, `changelog`, `inquisitions` — decide: its own record type (DB), a **property/partition
   of another record type**, or a browsable doc. (Balazs's lean: most are records; `backlog` /
   `future-features` are status/horizon *properties*, not types.)
3. Specifies the **folder → property migration** for each legacy status-folder: what property replaces
   it, on which record type, and the allowed values.
4. Specifies the **subfolder-partition convention** (status-value subfolders under a record type,
   flattened in code) and normalizes it across record types — starting from the existing
   `issues/resolved/` and adding siblings for the other values.
5. **Coordinates ownership:** Brian implements the `sync-model` object-type + field-schema entries;
   Charlie's mirror consumes the exclusion automatically. Call out any change to the flatten/pool
   loader or link integrity the migration implies.
6. Notes the **migration mechanics** as a follow-on sprint: moving files, backfilling `status`
   frontmatter, and keeping `[[wikilink]]` / relative-link integrity across the moves.

## Reuse / prereqs

Spine object model + `sync-model.json` (Brian's track), the flatten/pool doc loader
(`NotionSpineSync.LoadDocs` pools `*.md` recursively per type), DR 025 (canonical files + adapter) and
DR 033 (docs mirror). Confirm the include/exclude interplay with Charlie so the two surfaces stay
partitioned (every dir is exactly one of: spine DB, docs-mirror page, or excluded machinery).
