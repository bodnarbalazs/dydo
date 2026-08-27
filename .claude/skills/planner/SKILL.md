---
name: planner
description: Turns ripe designs into independently reviewable Linear Issue or repository Project-plan contracts. The methodology, standards, and checklist for working as a planner.
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

### Choose the contract grain

- **Atomic, autonomous-ready work** — sharpen one Linear Issue so its intent, scope, file boundary,
  acceptance criteria, gates, dependencies, and evidence requirements are independently reviewable.
- **Coordinated, cross-cutting, or architecture-sensitive work** — write one repository Project plan,
  link it to its Linear Project, and map implementation to disjoint Linear Issues.

Do not create a repository plan merely to mirror an atomic Issue.

### Write the Project plan

`dydo/project/plans/<name>.md`:

```markdown
---
title: <Name>
status: draft
area: project
type: context
linear-project: <stable Linear Project URL>
---

# <Name>

A 2–4 sentence summary of the Project outcome, why coordinated planning is required, and how the
repository contract relates to the linked Linear Project.

## 1. Specification
**Intent** — what this delivers and why, 2–4 sentences.
**In scope** / **Out of scope** — explicit lists. Out-of-scope is binding.
**Acceptance criteria** — observable, testable; the audit checks exactly these.
**Questions & answers** — every question raised during design, with its answer. None open.

## 2. Prior art
What was searched, what was found, why rejected/adopted. Evidence, not claims.

## 3. Design
Touchpoints, the existing patterns to follow (with paths), hazards, rollback.

## 4. Implementation Issue map
| Issue | outcome | files touched (disjoint) | blockers | gate |
|---|---|---|---|---|
| <TEAM-123> | <reviewable outcome> | path/A.cs | — | <exact command> |
| <TEAM-124> | <reviewable outcome> | path/B.cs | TEAM-123 | <exact command> |

## 5. Ordering & isolation
Which Issue lanes run in parallel worktrees versus serially; shared hot files; why the Issues cannot
collide. The orchestrator assigns worktrees and integrates passed Issue branches serially — this section
is its instruction sheet.

## 6. Watch-outs
The traps a reviewer or implementer must not walk into.
```

Each implementation Issue is **disjoint by file** and **atomic** — independently reviewable in one
round. A fresh implementer with only the Issue, its exact governing plan commit, and the coding
standards must be able to execute it without making architectural decisions. No model names in plan
text. Use the [dydo glossary](../../../dydo/reference/dydo-glossary.md)'s terms consistently.

When planning from a delivery Waypoint, plan only the currently visible Issue or bounded Project-plan
increment. Never turn Fog into speculative Linear work merely to make the plan look complete.

### Hand off to the gate

A **separate** reviewer subagent reviews the atomic Issue or Project plan — fresh eyes: it receives the
contract and evidence, never this conversation. A pass becomes review evidence linked from Linear; a
failure returns specific findings to you. Repository plan metadata may describe the artifact as draft or
reviewed, but never mirrors live Linear workflow state.

You planned it, so you can orchestrate it — but weigh your context: noisy from exploration → hand the
green-lit Issue or Project to a fresh orchestrator; high-signal → run it yourself.
