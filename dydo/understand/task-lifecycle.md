---
area: understand
type: concept
---

# Task Lifecycle

How tasks flow from creation through implementation, review, and completion.

---

## Task Creation

```bash
dydo task create <name> --area <area> [--description "<text>"]
```

Creates a task file at `dydo/project/tasks/<name>.md` with YAML frontmatter, starting in `backlog`. The `--area` flag is required (valid values: `backend`, `frontend`, `general`). Duplicate names are rejected.

---

## Task States

Tasks move through a state machine:

```
backlog → in-progress → in-review → done
                 ↑            │
                 └────────────┘
```

| State | Meaning |
|-------|---------|
| **backlog** | Created; ready to be picked up |
| **in-progress** | Being worked on |
| **in-review** | Work complete, awaiting review |
| **done** | Review passed or a qualified person completed non-review work; terminal state |

---

## The Task File

Each task lives at `dydo/project/tasks/<name>.md`:

```markdown
---
area: general
name: feature-x
status: in-progress
created: 2026-03-16T14:18:13Z
assigned: unassigned
updated: 2026-03-16T16:19:47Z
---

# Task: feature-x

Description of the work.

## Progress

- [x] Step one
- [ ] Step two

## Files Changed

(Record relevant changes here.)

## Review Summary

(Populated when marked ready for review)
```

---

## Ready for Review

```bash
dydo task ready-for-review <task-name> --summary "Summary of changes"
```

Marks the task `in-review`; the summary lands in the task file's Review Summary section.

---

## Review Flow

A reviewer (fresh eyes — never the implementer) examines the changes, runs the gates, and records the verdict:

```bash
dydo review complete <task-name> --status pass [--notes "LGTM"]
dydo review complete <task-name> --status fail --notes "Missing input validation"
```

**If pass:** task status becomes `done`. A "Code Review" section is added to the task file with date, result, and notes.

**If fail:** task status returns to `in-progress` with the rejection notes recorded, and the work goes back for rework.

## Completing Work Without Review

For work that does not require a review, mark an `in-progress` task done directly:

```bash
dydo task done <task-name>
```

Done tasks remain on the board. The human archives them later; agents do not delete task files.

---

## Task Listing

```bash
dydo task list                 # Active tasks
dydo task list --needs-review  # Only in-review tasks awaiting code review
dydo task list --all           # Include done tasks
```

---

## Related

- [Work Model](./work-model.md) — where tasks sit among slices, sprints, and campaigns
- [CLI Commands Reference](../reference/dydo-commands.md)
