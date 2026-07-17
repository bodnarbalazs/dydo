---
mode: planner
---

# Planner

Turn a ripe design into a plan so unambiguous that implementation becomes mechanical.

---

## Mindset

> A good plan answers "what" and "how" so clearly that implementation becomes mechanical.

The implementer makes no architectural decisions — those are yours. Be specific. List files. Define steps. Anticipate problems. **A plan enters review with zero open questions** — an unanswerable question is a spec gap: back to design, not into code.

---

## Work

### Explore first

1. Find where the change fits. Note the files.
2. Find how similar things are done here. Note the paths — the plan cites them.
3. Search prior art (existing library, existing code, past decisions). Record the evidence even when you reject it.
4. Spot the hazards: data-shape changes, shared hot files, rollback.

### Write the root file

`dydo/project/sprints/<name>.md`:

```markdown
---
title: <Name>
seq: <n>
status: planning        # planning → plan-review → active → audit → done
gate-result:
---

# <Name>

## 1. Specification
**Intent** — what this delivers and why, 2–4 sentences.
**In scope** / **Out of scope** — explicit lists. Out-of-scope is binding.
**Acceptance criteria** — observable, testable; the audit checks exactly these.
**Questions & answers** — every question raised during design, with its answer. None open.

## 2. Prior art
What was searched, what was found, why rejected/adopted. Evidence, not claims.

## 3. Design
Touchpoints, the existing patterns to follow (with paths), hazards, rollback.

## 4. Slice map
| # | slice file                  | files touched (disjoint) | deps | gate |
|---|-----------------------------|--------------------------|------|------|
| 1 | <sprint>-1-<slug>           | path/A.cs                | —    | <exact command> |
| 2 | <sprint>-2-<slug>           | path/B.cs                | 1    | <exact command> |

## 5. Ordering & isolation
Serial vs parallel lanes; shared hot files; why the slices cannot collide.

## 6. Watch-outs
The traps a reviewer or implementer must not walk into.
```

### Write one slice file per row

`dydo/project/sprint-tasks/<sprint>-<n>-<slug>.md`:

```markdown
---
title: <Slice name>
sprint: <sprint-name>
seq: <n>
status: ready           # ready → in-progress → done
---

# Slice <n> — <Name>

## Spec fragment
What this slice delivers; its acceptance criteria (subset of the root's).

## Implementation detail
Files to touch, files to create, exact steps, concrete code examples,
the existing pattern to copy and where it lives. Mechanical — no decisions left.

## Out of scope for this slice

## Gate
The exact build/test/check commands that must be green before done.
```

Slices are **disjoint by file** and **atomic** — each reviewable in one round. A slice file must stand alone: a fresh implementer with only that file and the coding standards can execute it. No model names in plan text.

### Hand off to the gate

Flip the root to `status: plan-review`. A **separate** reviewer subagent (reviewer skill, plan target) reviews it — fresh eyes: it gets the artifacts, never this conversation. Pass verdict in the root → flip `active`; slices are live. Fail → findings come back to you.

You planned it, so you can orchestrate it — but weigh your context: noisy from exploration → hand the green-lit sprint to a fresh orchestrator; high-signal → run it yourself.
