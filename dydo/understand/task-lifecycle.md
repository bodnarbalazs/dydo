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

Creates a task file at `dydo/project/tasks/<name>.md` with YAML frontmatter. The `--area` flag is required (valid values: `backend`, `frontend`, `general`). A task created by an agent is assigned and starts `in-progress`; one created without an agent starts in `backlog`. Duplicate names are rejected.

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
| **backlog** | Created without an assigned agent; ready to be picked up |
| **in-progress** | Agent working on it |
| **in-review** | Work complete, awaiting code review |
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
assigned: Brian
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

## Tasks and Agent Roles

An agent binds to a task when setting a role:

```bash
dydo agent role code-writer --task feature-x
```

This sets the task status to `in-progress` and records the assignment. An agent can only have one in-progress task at a time. The role is recorded in `TaskRoleHistory` for constraint enforcement (e.g., preventing self-review). Multiple agents can work on the same task in different roles (e.g., code-writer implements, reviewer reviews), but double-dispatch protection prevents two agents from doing the same role simultaneously.

---

## Ready for Review

Two paths to mark a task for review:

**Manual transition:**
```bash
dydo task ready-for-review <task-name> --summary "Summary of changes"
```

**Auto-transition (preferred):**
```bash
dydo dispatch --role reviewer --task <task-name> --brief "Review summary"
```

The auto-transition combines marking the task `in-review` and dispatching the reviewer in a single command. The `--brief` content becomes the review summary.

---

## Review Flow

A reviewer agent examines the changes, runs tests, and records their verdict:

```bash
dydo review complete <task-name> --status pass [--notes "LGTM"]
dydo review complete <task-name> --status fail --notes "Missing input validation"
```

**If pass:** Task status becomes `done`. A "Code Review" section is added to the task file with reviewer name, date, result, and notes.

**If fail:** Task status returns to `in-progress`. The rejection notes are recorded. The reviewer can dispatch a code-writer to fix the issues.

## Completing Work Without Review

For work that does not require a review, a human terminal or a different agent can mark an `in-progress` task done:

```bash
dydo task done <task-name>
```

The assigned implementer cannot mark their own task done. A task can also be marked done from `in-review` when appropriate.

Done tasks remain on the board. The human archives them later; agents do not delete task files.

---

## Task vs Dispatch

Tasks and dispatches are related but independent:

- A **task** tracks the lifecycle of a unit of work from creation through completion
- A **dispatch** assigns an agent to work on a task

Multiple dispatches can happen within a single task's lifecycle (code-writer → reviewer → code-writer for rework → reviewer again). The task persists as the canonical record of progress.

---

## Task Listing

```bash
dydo task list                 # Active tasks
dydo task list --needs-review  # Only in-review tasks awaiting code review
dydo task list --all           # Include done tasks
```

---

## Related

- [Agent Lifecycle](./agent-lifecycle.md)
- [Dispatch and Messaging](./dispatch-and-messaging.md)
- [CLI Commands Reference](../reference/dydo-commands.md)
