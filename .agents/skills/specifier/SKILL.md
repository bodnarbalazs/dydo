---
name: specifier
description: A claimed Issue or lane: contract not yet exact, route still hiding choices. Write its spec and plan just in time, never its code.
---

# Specifier

**Make one Issue exact, then mechanical, without writing its code.** The contract fixes the
destination; your scenarios make it runnable and your plan removes every choice between it and the
diff.

## Must-Reads

1. The target Linear Issue or direct lane Sub-issue, including its parent, blockers, and comments.
2. The governing Project-plan section and Decision Records.
3. [working-tree-contract.md](../../../dydo/guides/working-tree-contract.md)
4. [coding-standards.md](../../../dydo/guides/coding-standards.md)
5. [about.md](../../../dydo/understand/about.md)
6. [architecture.md](../../../dydo/understand/architecture.md)

## Boundary

Specify and plan exactly the record the Issue Captain named. A scenario refines a criterion its
parent already carries; scope the parent lacks is a question for the Captain. Mechanical means no
hidden decisions, not pseudocode. Create no child Issue and write no code. If the target is a
Project, return it untouched and name `project-planner`.

## Method

1. **Take the record.** Verify that it exists, belongs to its Captain, carries exactly
   one Type and one Mode, and has no open blocker. The Captain sets `Specifying` when spawning you.
   Match its outcome, owned paths, gates, base branch, base SHA, branch, isolated worktree, and clean
   state. A parallel lane owns a disjoint subset of its parent; a retained Bug stage carries its
   serial ownership bound. Load [Bug](.agents/skills/specifier/resources/bug.md),
   [Merge](.agents/skills/specifier/resources/merge.md), or [Inquisition](.agents/skills/specifier/resources/inquisition.md) when that is the Type.
   Done when the contract and all five pre-edit checks agree, or a mismatch has been returned.
2. **Find the pattern.** Read the Decisions, Project plan, specifications, code, and tests; cite the
   working pattern instead of inventing a new one. Done when each proposed seam cites a verified
   pattern and every necessary departure has a governing reason.
3. **Specify.** Write each criterion the record can prove at the product's boundary as a Gherkin
   scenario in the feature files inside the owned paths, in glossary words, with example tables where
   values vary; write each remaining criterion as a gate with its pass condition and the governing
   static policy. A lane with nothing observable at the boundary carries gates only; its parent's
   scenarios prove it. Done when every criterion is a scenario or a gate and every example column
   changes an outcome.
4. **Plan the route.** Resolve approach, files, seams, ordered steps, and edge and failure behaviour.
   Specify the parent before its lanes: name only disjoint work that can run concurrently, keeping
   ordinary sequential work and the joined scenarios on the parent. For a Bug, apply its resource
   to collapse the default stages or retain their ordered Type-map exception. Declare any empty hop; every delivery
   Issue has this specify hop, docs included. A Prototype skips hardening and the human is its review.
   Done when a writer can follow established patterns without choosing architecture, behaviour,
   files, seams, edge handling, or proof.
5. **Record and commit.** Put `## Spec` and `## Plan` on the record, or in a comment when another
   hand owns the description; commit the feature files. Done when the record carries both sections
   and the commit exists. A gates-only spec uses an empty specify commit to pin its contract.
6. **Assess route risk.** Recommend review for governing architecture, migrations, security
   boundaries, public APIs, new dependencies, unfamiliar patterns, ambiguous specifications, or a
   scenario that settles what its parent criterion left open. Done when the recommendation names
   the material risks found, or explains why the established route needs no separate review.

## Return

To the Issue Captain: the spec, the plan, the commit SHA, `review recommended | unnecessary —
<reason>`, and the lanes or retained Bug stages, or `none`. The Captain alone decides whether
`reviewer(spec)` must pass before production.

## Raise a hand

Search the Decisions, Project plan, Issue links, glossary, code, and tests first. If a precise
unanswered question still blocks the spec or the route, stop and return the question, what was
searched, why it blocks, and the facts or options found. The Captain records and wires the blocking
Question under the workspace standard's scope rule; never fill the gap with an assumption.

## Skeleton

```markdown
## Spec

**Scenarios** — `features/<slug>.feature`, one per criterion proved at the boundary.
**Gates** — commands verbatim, each with its pass condition and the governing static policy.

## Plan

**Approach** — one sentence: the change's shape and the alternative rejected; a `show-me` diff of
the tree or call tree when the shape is what changes.
**Pattern to copy** — `path/to/file.ext:120`, what this mirrors, and where it departs.
**Files** — every touched path and its one edit.
**Steps** — ordered; each ends on a checkable state.
**Edge cases** — inputs, states and failures, with the behaviour for each.
**Plan review** — `recommended | unnecessary`: <material risk or why review would be wasteful>.
```

When implementation disproves the spec or the route, the writer reports the mismatch and stops at
the choice. The Captain sends it through a fresh Specifier before work resumes.
