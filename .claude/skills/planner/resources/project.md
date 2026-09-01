# The Project plan

Low-resolution map for one Linear Project: destination, scope, acceptance, architecture-level design,
an Issue map. Every Issue is sharpened to mechanical detail just in time by whoever picks it, so
file-by-file precision here is wasted work. Write it at `dydo/project/plans/<kebab-case>.md`, keep
`dydo check` clean, and keep the section numbers — briefs cite them.

```markdown
---
title: <the Linear Project's title>
status: draft
area: project
type: context
linear-project: <the Linear Project URL>
---

# <Title>

<Two or three sentences: the destination, and the tooling reality this Project runs under.>

## 1. Specification
### Intent — <what becomes true, and for whom; one paragraph, no file lists>
### In scope — <bullets by lane; every bullet is claimed by an Issue in §4>
### Out of scope — <what a reader would otherwise assume is included, and why it is not>
### Acceptance criteria — <numbered; each proved at the final merge by a command, diff or artifact>
### Questions and answers — <every question this plan settled, with its answer>
## 2. Prior art — <commits, upstream sources, docs and Decision Records read, and what each gave>
## 3. Design — <shape of the change, invariants, hazards, migration, rollback; name the paths and
patterns you verified at the governing commit instead of restating the code>
## 4. Implementation Issue map

| Issue | Outcome | Owned paths | Blockers | Gate | Base branch |
|---|---|---|---|---|---|
| <H-1> | <one independently reviewable outcome> | <its exclusive surface> | <none, or H-n> | <A> | <feature/slug> |

Tracer bullets: every Issue cuts end to end and lands something. Widen a refactor by expand–contract
rather than by one Issue that touches everything. Close the map with integration and with whatever
durable knowledge this Project owes dydo.
### Exact gates — <one named block per gate letter: copy-pasteable commands run from the repository
root in the Issue's worktree, and what its evidence must prove>
## 5. Ordering and isolation — <kickoff acts, merge order, which Issues run in parallel, and every hot
file owned by one Issue at a time>
## 6. Watch-outs — <the mistakes this Project's Issue Captains and reviewers would otherwise make>
## Not yet specified — <in-scope fog too vague to state as a question; omit the section when clear>
```

**Fog stays on the map.** Never pretend a complete route. Fog that has sharpened into a precise
question leaves `## Not yet specified` and becomes a question Issue — Linear label `question`, body
under `## Question` — wired as a blocker of whatever it holds up. The test is precision, not
answerability: a sharp question is an Issue even while nothing can answer it yet. Charting that fog and
working the frontier it leaves is wayfinder's method; the plan links the Project and stops there.

**Reviewed once, then amended.** A fresh reviewer with the `plan` rubric passes the plan before any
Issue is pickable; `status` becomes `reviewed` and that commit governs execution. From there the
manager amends in place as fog clears, as a dated `## Amendment — <YYYY-MM-DD>` section rather than a
rewrite of reviewed text. Re-review only when scope, acceptance criteria or the Issue map change.
