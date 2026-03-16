---
area: understand
type: concept
---

# Task Lifecycle

How tasks flow from creation through implementation, review, and approval.

---

## Task Creation

```bash
dydo task create <name> --area <area> [--description "<text>"]
```

Creates a task file at `dydo/project/tasks/<name>.md` with YAML frontmatter. The `--area` flag is required (valid values: `backend`, `frontend`, `general`). The task is automatically assigned to the current agent. Duplicate names are rejected.

---

## Task States

Tasks move through a state machine:

```
pending → active → review-pending → human-reviewed → closed
                        ↑                  ↑
                        └─── review-failed ─┘
```

| State | Meaning |
|-------|---------|
| **pending** | Created, not yet in work |
| **active** | Agent working on it (set by `dydo agent role <role> --task <name>`) |
| **review-pending** | Work complete, awaiting code review |
| **human-reviewed** | Agent review passed, awaiting human approval |
| **review-failed** | Review rejected, needs rework |
| **closed** | Approved and moved to changelog (terminal state) |

---

## The Task File

Each task lives at `dydo/project/tasks/<name>.md`:

```markdown
---
area: general
name: feature-x
status: active
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

(Populated during approval from audit logs)

## Review Summary

(Populated when marked ready for review)
```

---

## Tasks and Agent Roles

An agent binds to a task when setting a role:

```bash
dydo agent role code-writer --task feature-x
```

This sets the task status to `active` and records the assignment. An agent can only have one active task at a time. The role is recorded in `TaskRoleHistory` for constraint enforcement (e.g., preventing self-review). Multiple agents can work on the same task in different roles (e.g., code-writer implements, reviewer reviews), but double-dispatch protection prevents two agents from doing the same role simultaneously.

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

The auto-transition combines marking the task as `review-pending` and dispatching the reviewer in a single command. The `--brief` content becomes the review summary.

---

## Review Flow

A reviewer agent examines the changes, runs tests, and records their verdict:

```bash
dydo review complete <task-name> --status pass [--notes "LGTM"]
dydo review complete <task-name> --status fail --notes "Missing input validation"
```

**If pass:** Task status becomes `human-reviewed`. A "Code Review" section is added to the task file with reviewer name, date, result, and notes.

**If fail:** Task status becomes `review-failed`. The rejection notes are recorded. The reviewer can dispatch a code-writer to fix the issues.

---

## Human Approval Gate

After a reviewer passes a task, a human makes the final call:

```bash
dydo task approve <task-name> [--notes "Ship it"]
dydo task approve --all
```

Approval deletes the task file and creates a changelog entry at `dydo/project/changelog/<year>/<date>/<task-name>.md`. The "Files Changed" section is populated from audit logs.

```bash
dydo task reject <task-name> --notes "Needs more test coverage"
```

Rejection sets the task back to `review-failed` with feedback recorded. Use `dydo task list --needs-review` to see tasks awaiting human approval.

---

## Task vs Dispatch

Tasks and dispatches are related but independent:

- A **task** tracks the lifecycle of a unit of work from creation through approval
- A **dispatch** assigns an agent to work on a task

Multiple dispatches can happen within a single task's lifecycle (code-writer → reviewer → code-writer for rework → reviewer again). The task persists as the canonical record of progress.

---

## Task Listing

```bash
dydo task list                 # Active tasks
dydo task list --needs-review  # Only human-reviewed tasks ready for approval
dydo task list --all           # Include closed tasks
```

---

## Related

- [Agent Lifecycle](./agent-lifecycle.md)
- [Dispatch and Messaging](./dispatch-and-messaging.md)
- [CLI Commands Reference](../reference/dydo-commands.md)
