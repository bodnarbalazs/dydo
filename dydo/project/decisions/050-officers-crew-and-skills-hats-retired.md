---
area: project
type: decision
status: accepted
date: 2026-09-22
accepted: 2026-09-22
participants: [balazs, Claude admiral]
---

# 050 — Officers, Crew and Skills: Hats Retired

A **role** is a skill that carries an identity, and there are two kinds of role. **Officers** hold
something and never switch. **Crew** are spawned for one bounded job, hold nothing, and return their
result. Every other skill is a plain skill that anyone loads, sorted by subject into `engineering`
and `productivity`. The word **hat** is retired. **Worker** becomes **crew** wherever it named this
concept. The canonical tree is sorted by these kinds. Settled by the human in the 2026-09-22 co-think
with the admiral.

---

## Context

[DR 045](./045-flow-map-hats-review-tiers-and-working-tree-contract.md) described a session as
wearing a *hat* and changing hats as the work moved: co-thinker, project-planner, issue-captain,
admiral, chief-of-staff. Spawned execution roles were *workers*. By 3.0 that no longer matched how
work ran:

- **The officer roles do not switch.** An admiral holds a Project, and an issue-captain holds an
  Issue from branch to merged PR. A captain never becomes an admiral, and an admiral never becomes a
  captain. A co-thinker that files ripe Issue intent does not become its captain either: the admiral
  commissions one, or the human opens a captain session. "Changing hats" described a move nobody
  makes.
- **co-thinker carries no identity.** It is a way of thinking with the human that any session can
  load, like `grilling` or `show-me`. Calling it a hat put it beside the admiral.
- **The line between hat and worker was a mood, not a criterion.** The project-planner was listed as
  both a hat and a spawnable worker. `research` and `scout` were workers filed under `engineering`,
  while `wayfinder` was a method filed under `orchestration`.
- **The skill tree mixed two axes.** [DR 049](./049-skills-are-the-source-retire-the-compiler.md)'s
  2026-09-21 amendment (DYD-219) grouped the skills into `orchestration/`, `engineering/` and
  `productivity/`. `orchestration/` sorted by kind (it held the roles). `engineering/` and
  `productivity/` sorted by subject, following Matt Pocock's skill categories, from which many of
  these skills are adapted. As a result, code-writer, research and scout sat under `engineering/`
  although they are roles, and wayfinder sat under `orchestration/` although it is not.

## Options Considered

### Option A: Keep hats and workers, fix the misfiled skills

- **Pros:** no vocabulary churn.
- **Cons:** keeps a word for a move that does not happen, and keeps co-thinker beside the officers.
  Where a skill belongs would still be decided by feel.

### Option B: One flat `roles/` folder

- **Pros:** one level of categories, as today.
- **Cons:** the difference that matters, holds versus returns, is lost from the tree and survives
  only in prose.

### Option C: Sort by a criterion; officers and crew under `roles/` (chosen)

- **Pros:** placement follows a testable question: does the skill carry an identity, and if so, does
  it hold something or return a result? The subject folders keep Pocock's meaning, so an adapted
  skill sits where its upstream would.
- **Cons:** one level of nesting under `roles/`, so any tool that walked a fixed depth has to walk by
  rule instead.

## Decision

1. **A role is a skill that carries an identity.** There are two kinds:
   - **Officers** hold something and never switch. The admiral holds a Project, the issue-captain
     holds an Issue, and the chief-of-staff holds the board. A session or agent has at most one
     officer role. A captain never becomes an admiral, and an admiral never becomes a captain.
   - **Crew** are spawned for one bounded job, hold nothing, and return their result to whoever sent
     them: project-planner, code-writer, docs-writer, reviewer, inquisitor, research, scout.
2. **Every other skill is a plain skill.** An officer, a crew member or a role-less session loads it
   when the work needs it. The plain skills sort by subject: `engineering` holds the craft, and
   `productivity` holds thinking, writing and moving work along with the human. `co-thinker` is a
   productivity skill, not a role.
3. **Hat is retired.** A session does not change hats: it keeps the role it was opened or spawned
   with, and it loads skills. **Worker** becomes **crew** wherever it named this concept. Other
   senses stay, such as the guard's Tier-2 worker lane and the nudge `audience: worker` value.
4. **The canonical tree:**
   - `skills/roles/officers/`: admiral, issue-captain, chief-of-staff
   - `skills/roles/crew/`: project-planner, code-writer, docs-writer, reviewer, inquisitor,
     research, scout
   - `skills/engineering/`: codebase-design, domain-modeling, diagnosing-bugs, prototype, wizard,
     improve-codebase-architecture
   - `skills/productivity/`: co-thinker, grilling, grill-me, bro, handoff, teach, show-me,
     walkthrough, writing-for-agents, writing-for-humans, self-improvement, wayfinder, to-project

   One level of nesting under `roles/` is accepted. Setup walks the tree by rule, not by depth: a
   folder holding `SKILL.md` is a skill, and any other folder under `skills/` is a category to walk
   into. Host projections stay flat. A link that setup made earlier, now pointing at a skill's old
   place, is re-pointed.

## Consequences

- **Gained:** each skill's placement answers a question anyone can check. Officers stay officers.
  The entry point sorts a session by the role it was given, not by a mood it is in.
- **Accepted:** the vocabulary sweep across the glossary, entry points, guides and skills. A walker
  that assumed two levels had to become rule-based: `setup-skills.mjs`, the isolated runner's
  `_canonical_skill_names`, and the canonical-skill test steps.
- **Unchanged:** the Issue lifecycle, the hop names, the review tiers and the working-tree
  contract. Only the words for who does the work change.

---

## Supersedes and amends

Amends [DR 045](./045-flow-map-hats-review-tiers-and-working-tree-contract.md): its taxonomy of hats
and workers is replaced by officers, crew and plain skills, and sessions no longer change hats.
Amends [DR 049](./049-skills-are-the-source-retire-the-compiler.md): adds a second layout line to its
2026-09-21 amendment.

## Affects

- [dydo Glossary](../../reference/dydo-glossary.md)
- [Work Model](../../understand/work-model.md)
- [Control Flow](../../understand/control-flow.md)
- [Architecture Overview](../../understand/architecture.md)
- [Customizing Roles](../../guides/customizing-roles.md)
- [Working-Tree Contract](../../guides/working-tree-contract.md)
