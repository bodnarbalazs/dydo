---
area: guides
type: guide
---

# Writing Good Briefs

The self-containment bar for work handed to an agent. A brief is good when a fresh agent can execute it
and an independent reviewer can decide PASS or FAIL from the same text, without reconstructing the
conversation that produced it.

Two things get briefed: a **Linear Issue**, picked up and owned end to end, and a **worker prompt**, one
bounded job that returns to whoever spawned it. Everything else — a Project plan, a Decision Record, a
guide — is context the brief links, never a substitute for it.

---

## The implementation Issue

Every implementation Issue carries five fields. They are the contract; the rest of the body is context.

| Field | What it settles |
|---|---|
| **Outcome** | what becomes true, in observable terms — the result, not the route |
| **Owned paths** | the exact files this Issue may change; everything else belongs to another Issue |
| **Blockers** | what must land first, wired as Linear blocking relations rather than described in prose |
| **Exact gates** | the commands that decide done, verbatim, each with its pass condition |
| **Base branch** | the branch this one is cut from |

What the Issue deliberately does **not** carry is the route. Pre-writing it ages badly and buys
nothing: the implementing agent reads the code you were guessing about, and the high-resolution plan is
written just in time by whoever picks the Issue up — the two planning resolutions are in the
[Linear Issue Lifecycle](../understand/task-lifecycle.md). One act there is yours: an Issue the Project
plan flags as architecture-sensitive says so, because its plan is reviewed before any code exists.

Owned paths do double duty. They are the brief's scope and the isolation that lets Issues run in
parallel, so two Issues in flight together own disjoint files or say plainly that they are serial. The
[working-tree contract](./working-tree-contract.md) turns those two fields — owned paths and base
branch — into a branch, a worktree and a claim.

## The worker's brief

A spawned worker has no memory of the conversation that made it and cannot ask a question and wait.
Give it five things:

1. **One deliverable**, named by path.
2. **What to read first**, in order: the governing Decision Record, the section of the plan that binds
   this deliverable, the file as it stands, and the code or configuration its claims must match.
3. **The boundary** — what it owns, what it must leave alone, and what it must not run; in a tree
   several workers share, name the files that are not its own. State the positive target beside each
   prohibition.
4. **The return shape** the receiver parses. For a writer: the deliverable, plus a short note naming
   the choice made, what was cut and why, the links carried, and one open doubt.
5. **The constraints that decide the verdict** — budget, vocabulary, and the exact gates.

The same bar applies as to an Issue. If the worker has to infer which of two files you meant, or invent
a product decision to finish, the brief is not ready.

## What comes back

The verdict is a fresh reviewer's **review block** — its fields are locked in the
[dydo Glossary](../reference/dydo-glossary.md) — and PASS means no findings.

Write the brief so every field of that block can be filled from it. Gates that are not commands cannot
be rerun; an outcome with no observable form cannot be judged; a candidate with no owned paths has no
boundary to be judged against.

## The escape hatch

A brief does not have to answer every question — it has to leave the open ones askable. What an agent
does with a question the brief did not settle is the *fog → discovery → question Issue* rule in the
[Linear Issue Lifecycle](../understand/task-lifecycle.md); what it ends up as is a **question Issue**,
Linear label `question`, the question itself under a `## Question` heading.

Your part is upstream of that. Name the questions you already know are open, link the question Issues
that carry them, and let the brief say plainly what it does not settle. An assumption buried inside an
outcome reads as settled, and gets built.

## What does not belong

- **"As discussed."** The receiving agent was not there. Neither was the reviewer.
- **A model, a host, or a permission.** Runtime configuration owns those. State the capability,
  independence and evidence the work requires, and escalate a runtime limitation instead of freezing a
  workaround into a durable brief.
- **A copy of durable knowledge.** Link the Decision Record, plan or guide at its exact commit. Nothing
  in Git mirrors an Issue body, and no Linear workflow field belongs in frontmatter.
- **Success you cannot fail.** "Make it work" passes every review and proves nothing.

## Before you dispatch

Two questions. Could a fresh agent deliver this without making a product decision? Could an independent
reviewer decide PASS or FAIL from the same text? If either answer is no, the brief is not ready.

## Related

- [Working-Tree Contract](./working-tree-contract.md) — base branch, owned paths, and the claim
- [Orchestration Pitfalls](./orchestration-pitfalls.md) — how briefs fail once several are in flight
- [Linear Issue Lifecycle](../understand/task-lifecycle.md) — where an Issue's state lives
- [Work Model](../understand/work-model.md) — what Linear owns and what Git owns
- [dydo Glossary](../reference/dydo-glossary.md) — question Issue, review block, gate
- [DR 045 — Flow Map, Hats and Workers, Review Tiers, and the Working-Tree Contract](../project/decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md)
