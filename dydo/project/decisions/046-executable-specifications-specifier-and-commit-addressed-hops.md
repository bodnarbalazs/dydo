---
area: project
type: decision
status: proposed
date: 2026-09-02
participants: [balazs, Claude (Fable)]
---

# 046 — Executable Specifications, the Specifier, and Commit-Addressed Hops

Makes the Issue contract runnable and the delivery chain traceable: acceptance criteria become Gherkin
scenarios where they can, a **specifier** writes the just-in-time spec and route that the Issue Planner
used to write half of, every worker hop ends on a commit the next hop and the reviewer read, and the
review block pins the contract it judged against. Settled in the 2026-09-02 co-think after reading
Uncle Bob's swarm-forge and its Acceptance Pipeline Specification.

---

## Context

- DR 045 fixes acceptance criteria as prose "proved at the final merge by a command, diff or
  artifact", and the code rubric's "matches the requested outcome" is judged by reading. The Issue
  Planner writes the route just in time, but no worker owns the exact *what*: an Issue's contract is
  authored by the project-planner, the admiral, or the captain, and sharpened by nobody.
- On 2026-09-01 the writer split into [implementer] → [hardener] (DYD-64), reversing DYD-69's "no
  separate hardener". The hardener's boundary reads "the contract fixes the behaviour, not the
  implementer's version of it" with nothing runnable to point at. Neither worker is told to commit
  before returning, while the reviewer must pin an immutable reference no rule guarantees exists.
- Swarm-forge (unclebob/swarm-forge, six-pack: specifier → coder → cleaner → architect → hardender →
  QA) owns the runtime DR 041 ceded: tmux, a worktree per role, a handoff daemon, a dashboard. Two of
  its mechanisms survive the translation across that boundary: Gherkin as the spec-level claim with
  example-value mutation, and handoffs that name a commit rather than a working tree. Its own
  two-pack ships without Gherkin, and its coder is told not to substitute acceptance tests for unit
  tests: the line between the two levels is drawn there too.

## Decision

### 1. Three claims prove a change

- A **scenario** claims behaviour at the product's boundary, in glossary words, with example tables
  where values vary. It is written in Gherkin in the Issue's feature files, which live with the tests
  inside the Issue's owned paths, and it is run by the project's acceptance runner.
- A **test** claims one seam inside, in the code's words. It is the implementer's and comes and goes
  with refactors.
- A **gate** is a command whose exit code proves what neither can state: the coverage bar, the
  mutation run, the docs check.

An acceptance criterion is a scenario when it can be one, else a gate. A scenario is contract: only
the specifier writes or changes one; implementation wires it through step definitions and never edits
it; a change to a scenario is a spec amendment recorded on the Issue and, when it changes acceptance,
re-reviewed under DR 045 §5. A scenario refines a criterion its parent already carries and never
extends scope. A lane with nothing observable at the boundary carries gates only; its parent's
scenarios prove it. The Project plan's acceptance criteria are proved at the final merge by running
the feature files its Issues wrote.

**Acceptance mutation** joins code mutation as a hardener measure: one example value changed at a
time, and a scenario still green marks a step that asserts nothing.

*Rejected:* Gherkin for every test, because step-definition glue on internal seams costs more than
it proves and produces scenarios nobody reads; and Gherkin nowhere, because acceptance then stays
hand-checked prose.

### 2. The specifier

`issue-planner` retires; **specifier** replaces it as the worker an Issue Captain sends ahead of
production for each parent Issue or lane. It writes the just-in-time *what* and *how* in one pass,
because the same reading of the code and the same questions feed both, and a route cannot be fixed
until the destination is exact. Its return is `## Spec` (scenarios and gates, with the tier the
module must meet) and `## Plan` (approach, pattern, files, steps, edge cases) on the record, plus the
commit that holds the feature files. The rubric `issue-plan` becomes `spec`; the review stays the
captain's call, and the specifier's recommendation triggers gain one item: a scenario that settles
what its parent criterion left open. The chain reads:

[specifier] → [implementer] → [hardener] → [reviewer]

*Rejected:* a separate specifier beside the issue-planner, because it adds a hop and reads the same
ground twice.

### 3. Commit-addressed hops

**Uncommitted work is not a return.** Every worker hop ends on a commit on the Issue branch, in owned
paths, with the message `<KEY> <hop>: <what>` where the hop is `specify`, `implement`, `harden` or
`fix`. The return names the SHA; the Issue Captain posts it on the record. The hardener starts at the
implementer's commit; a correction after a FAIL is its own commit and the re-review pins the new SHA.
The Issue branch keeps its hops unsquashed and unrewritten, and the contract's `--no-ff` merges carry
them into the feature branch. The code rubric gains a step: read the hops, and a behaviour or test one
hop had and a later hop dropped is a finding when the contract needed it.

### 4. The review block pins the contract

The block gains one line, `Contract: <Issue key or plan path> @ <governing SHA>`: the plan's governing
commit for a plan review, the specifier's commit for an Issue. A PASS binds one candidate under one
contract; a change to either calls for a fresh review.

*Not adopted:* swarm-forge's forced double submission with a self-audit between; a fresh independent
reviewer is the stronger gate, and the useful part, invalidation when the judged state changes, is
the sentence above.

### 5. Tooling stays outside the templates

The templates name "the project's acceptance runner" in the project's testing guide. DynaDocs adopts
Reqnroll, the maintained fork of SpecFlow (end of life 2024-12-31), in its own Issue; the Acceptance
Pipeline Specification's Babashka and Go toolchain is not imported. The format and the two ideas are.

## Consequences

- The delivery chain is four verbs, and the vocabulary loses one name: issue-planner, code-writer
  and test-writer are retired; specifier, implementer and hardener stand.
- The human reads scenarios at plan approval, in spec reviews they require, and in walkthroughs.
  Scenarios too many or too internal to read are wrong, and that is the guard against Gherkin
  theatre.
- Step-definition glue is a real cost, bounded by the line in §1: a CLI's steps are few and reused;
  an internal seam gets a test, never a scenario.
- Issue branches carry more commits, and history reads as hops.
- Templates: specifier and the `spec` rubric replace issue-planner and `issue-plan`; implementer,
  hardener, reviewer, the code and merge rubrics, issue-captain, project-planner, coding-standards §6,
  the working-tree contract, and the glossary carry the rules above. The DynaDocs acceptance runner
  and the first feature files are a separate Issue and the dogfood of this decision.

## Supersedes and amends

Amends DR 045: §2 workers are specifier · implementer · hardener · docs-writer · reviewer ·
inquisitor · research, with issue-planner, code-writer and test-writer retired; §3 the review block
gains the Contract line and the rubric `issue-plan` becomes `spec`; §5 Issue planning becomes
specification, scenarios and gates before the route. Records the 2026-09-01 hardener decision, which
reverses DYD-69's resolution.

---

## Affects

- [Work Model](../../understand/work-model.md)
- [Linear Issue Lifecycle](../../understand/task-lifecycle.md)
- [dydo Glossary](../../reference/dydo-glossary.md)
- [Working-Tree Contract](../../guides/working-tree-contract.md)
- [Coding Standards](../../guides/coding-standards.md)
- [Testing Strategy](../../guides/testing-strategy.md)
- [Writing Good Briefs](../../guides/writing-good-briefs.md)
- [Harmonize the skill system](../plans/dydo-3-skill-harmonization.md)
- [DR 045 — Flow Map, Hats and Workers, Review Tiers, and the Working-Tree Contract](./045-flow-map-hats-review-tiers-and-working-tree-contract.md)
