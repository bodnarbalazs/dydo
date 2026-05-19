---
area: project
type: folder-meta
---

# Backlog

Identified, scoped, ready-ish work that isn't in flight and isn't yet an active task. Distinct from `issues/` (broken things) and `future-features/` (far-out ideas without a concrete next step). Convention-only — backlog items use `type: context`; the folder location is the discriminator.

## Category Boundary

> If the thing is broken → `issues/`. If the thing is identified work not yet in flight → `backlog/`. If the thing is a far-out idea without a concrete next step → `future-features/`.

Borderline items stay where they were filed unless they obviously cross the boundary. Don't migrate well-described entries just because this category exists.

## File Format

One slug-named `.md` file per item (e.g. `querykey-hygiene-factory-and-lint.md`). Numbering is reserved for `issues/`.

### Required Frontmatter

```yaml
---
area: <one of the existing area enum values>
type: context                              # existing enum value; mirrors future-features
status: open | in-flight | done | cancelled
created: <YYYY-MM-DD>
created-by: <agent-name-or-balazs>
origin: <one-line — where the item came from>
---
```

`type: context` is the value. There is no `type: backlog` — the folder location is what marks the file as a backlog item.

### Optional Frontmatter

```yaml
related-issues: [<issue-ids>]
related-decisions: [<decision-numbers>]
related-tasks: [<task-names>]              # populated on pickup
```

`priority` and `estimated-scope` are intentionally not included. What's next is decided at pickup, not stamped on the item.

## Lifecycle

| State | Frontmatter | File location |
|---|---|---|
| Created | `status: open` | `dydo/project/backlog/<slug>.md` |
| Picked up | `status: in-flight`, populate `related-tasks: [<slug>]` | (same) |
| Completed | `status: done` | move to `dydo/project/backlog/done/<slug>.md` |
| Cancelled | `status: cancelled` | move to `dydo/project/backlog/done/<slug>.md` |

`done/` is the no-longer-live archive; the `status` field disambiguates completed from cancelled. The `done/` subfolder is created the first time an item is archived — not before.

See Decision 023 — `dydo/project/decisions/023-backlog-doc-category.md` — for the full rationale and alternatives considered.
