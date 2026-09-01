# Planning One Issue

The `issue` resolution: high detail, just in time. One implementation Issue's route from its contract
to its diff, written into the Issue by a spawned `planner(issue)` at the Issue Captain's direction and
reviewed with the code it guides.

The Issue Captain coordinates; the Issue already carries outcome, owned paths, blockers, exact gates
and base branch. The plan begins where those end and refines them until the work is **mechanical**:
every remaining edit is one a delegated writer can make without deciding anything. That is the stopping
bound — keep resolving while a step still hides a choice, and stop the moment none does.

## Where it goes

Post it as a `## Plan` section on the Linear Issue, or as a comment when another hand owns the
description. The environment fields the
[working-tree contract](../../../../dydo/guides/working-tree-contract.md) requires — base SHA, branch,
worktree path — are on the Issue before the first edit, so the plan is written against a settled tree
and the diff can be traced back to it.

When the Project plan flags an Issue as architecture-sensitive, its plan is reviewed on its own,
before any code exists.

## The skeleton

Paste this, fill it, keep the headings:

```markdown
## Plan

**Approach** — one sentence: the shape of the change, and the alternative it was chosen over.

**Pattern to copy** — `path/to/file.ext:120`, the working thing this mirrors, and where it departs.

**Files** — every path this touches, each with the one edit it receives.

**Steps** — ordered; each ends on a state you can check: a red test, a green build, a written file.

**Edge cases** — the inputs, states and failures this change meets, and what it does with each.

**Gates** — the Issue's gate commands verbatim, plus any this change adds, each with its pass condition.
```

Each part earns its lines by the choices it removes: a one-file change still names its pattern, its
steps and its gates, and can do all three in three lines.

When the code disproves a step, edit the section and record what replaced it. Plan and diff arrive at
review together, so they agree by the time they do.
