---
area: reference
type: reference
---

# dydo Glossary

The dydo system's terms, locked. One meaning per word, used consistently across docs,
templates, skills, and records. The project's own domain terms live in the separate
[glossary.md](../glossary.md).

## The work hierarchy

- **Release** — a shipped, tagged version.
- **Campaign** — a large goal spanning multiple sprints.
- **Sprint** — one plan's execution unit: a root record plus its slices.
  `planning → plan-review → active → audit → done`.
- **Slice** — the atom of implementation: one disjoint piece of a sprint, small enough to
  review in one round. Lives as one record file under `project/slices/`
  (`ready → in-progress → done`); may carry a checklist of subtasks in its body.
- **Task** — a day-to-day tracked work item (`project/tasks/`,
  `backlog → in-progress → in-review → done`). Not sprint work.
- **Lane** — a set of slices executing serially in one worktree; lanes run in parallel
  with each other. The plan's Ordering & isolation section declares the lanes.

## Planning

- **Plan** — the whole artifact a planner produces: sprint root + slice files. The plan
  *is* the records; there is no separate plan document.
- **Specification (spec)** — section 1 of a sprint root: intent, binding scope,
  acceptance criteria, and every question answered. Not a standalone artifact.
- **Gate** — any pass/fail checkpoint: the plan-review gate, a slice's gate commands,
  the audit.
- **Audit** — the merged-sprint review stage (reviewer × merge-sprint). At sprint
  altitude the word `in-review` is never used; `in-review` belongs to tasks only.

## The compile chain

- **Role** — an authored methodology identity: a mode template plus its role definition.
  A compile-time concept.
- **Skill** — the compiled folder (`SKILL.md` + `resources/`) a session invokes: the
  runtime packaging of a role's methodology.
- **Agent** — a spawned worker instance running a compiled agent definition (tool
  profile + model tier). In dydo prose, never "any AI".
- **Resource** — a skill's per-domain reference file. Protected word:
  `<role>-resource-<name>.template.md` compiles to the skill's `resources/`.
- **Workflow** — a deterministic orchestration harness script. Protected prefix:
  `workflow-<name>.js` compiles to the platform's workflow folder (run-sprint,
  inquisition).

## Everything else

- **Record** — any markdown+frontmatter file under `dydo/project/`.
- **Inquisition** — the multi-lens QA sweep workflow, run at milestones.
  Orchestrator-shaped, not review-shaped.
- **Worktree** — native git isolation for a lane. The platform owns it; dydo plans
  for it.
- **Guard** — the hook on every tool call: off-limits paths, dangerous commands, nudges.
- **Nudge** — a configurable regex rule the guard enforces with guidance (warn or block).

## Retired terms

Do not use these; each has a canonical replacement.

- **Marathon** → *campaign* (scope) or a *sprint sequence* (duration).
- **SprintTask** → *slice*. (The old name existed to avoid colliding with *task*; slice
  solves that naturally.)
