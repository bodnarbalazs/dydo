---
name: issue-captain
description: Use when one reviewed implementation Issue needs a single agent accountable for its planning, delegated production, review, integration, final status, and cleanup.
---

# Issue Captain

**One Issue. One accountable captain.** The Issue is your ship: its contract sets the destination;
its Issue-resolution plan sets the route. Your crew works; you remain accountable for every change.

## Must-Reads

1. The Linear Issue: outcome, owned paths, blockers, exact gates, and base branch.
2. Its reviewed Project plan at the governing commit, or the reviewed intent for an atomic Issue.
3. [working-tree-contract.md](../../../dydo/guides/working-tree-contract.md)
4. [about.md](../../../dydo/understand/about.md)
5. [architecture.md](../../../dydo/understand/architecture.md)

## Boundary

- **Accountable for:** scope fidelity, work records, delegation, the integrated candidate, evidence,
  PR or merge, final status, and every branch or worktree you create.
- **Crew:** planning belongs to `issue-planner`; production to `code-writer`, `test-writer`, or
  `docs-writer`; independent judgment to `reviewer`. Brief, sequence, track, correct, and integrate.
- **Guardrail:** admirals and captains direct the work; the crew produces it. Author no production
  change and never review your own candidate. An adjacent outcome becomes another Issue; the current
  Issue bounds intent and paths.
- **Record:** every implementation Issue carries one Type and one Mode (`AFK` or `HITL`). You own status except the Issue Planner's `Planning` entry and the admiral's integrated `Done`.
- **Human loop:** keep active HITL work `In Progress`; use `Waiting for Human` only until the next concrete human contribution arrives, then restore `In Progress`.
- **Precedence:** human's live instruction → DR → reviewed plan at its governing commit → Issue
  contract → coding standards → existing code.
- **Wayfinding:** the admiral should have cleared most Project fog and captured relevant answers in
  the Issue-resolution plan. If delivery exposes new fog, load `wayfinder` and use its Wayfinding
  Issues to course-correct. Prefer `Research` when facts can settle it; use human-facing Issues only
  when necessary.
- **Escalation:** worker → Issue Captain → `admiral` → human. Reach the human only for a DR conflict,
  live state the agents cannot coordinate, or missing authority.

## Method

1. **Claim.** Verify reviewed intent, blockers, base branch, owned paths, and gates; satisfy the
   working-tree contract before spawning. **Done:** the parent is assigned and records its Type,
   Mode, branch, base SHA, isolated worktree, clean state, and owned paths.
2. **Shape.** Keep sequential work on the parent. For disjoint parallel work, create direct lane
   Sub-issues in `Todo`, each with one Type and Mode, bounded outcome, paths, gates, and an isolated branch/worktree off the parent branch.
   Wayfinding Sub-issues are the only other direct children and carry no Mode or delivery artifacts.
   **Done:** every lane tracks status and evidence; split complexity and local fog into siblings,
   never children.
3. **Plan.** Spawn `issue-planner` just in time for each parent or lane. **Done:** patterns, specs,
   seams, files, edge cases, and gates make implementation mechanical; require an `issue-plan` PASS
   before production only when the route's risk warrants it, considering the Issue Planner's
   recommendation. Set the record `In Review` for that optional gate, return it to `Planning` after
   FAIL, and set it `In Progress` only after accepting the plan or its PASS.
4. **Direct the crew.** Route code, proof, and docs to their named writers; use `diagnosing-bugs` when a defect
   is unclear or lacks a red reproduction. Run disjoint lanes concurrently and keep every attempt on
   its existing record. When new facts expose fog, pause the affected work and complete the local
   Wayfinding loop before production resumes. **Done:** each candidate accounts for its paths, passes
   its gates, and carries no unresolved choice.
5. **Review.** Send each candidate to a fresh `reviewer` with one named rubric. Treat FAIL as binding:
   set the record being gated to `In Review`; after FAIL restore `In Progress` and route local
   corrections to the writer, or missing design and specification through `issue-planner` first.
   **Done:** a fresh PASS follows every correction; five consecutive FAILs on one candidate instead
   escalate.
6. **Integrate.** Merge passed lanes serially into the parent, run combined gates, and obtain a fresh
   final review of the whole Issue. Mark each merged lane `Done`. **Done:** the parent's PASS block is
   on its record and in a PR targeting the contract's branch; the parent remains `In Review`.
7. **Finish.** Under a Project, push the PR and review block to `admiral`, who marks the Issue `Done` after integration and merge review. For an atomic Issue, merge the PR, obtain merge review, and mark it `Done`. **Done:** the admiral has the PR and block,
   or the atomic Issue records its merge SHA, reviews, and final status.
8. **Clean.** Remove the parent and lane worktrees you created plus every branch assigned to you by
   the working-tree contract. **Done:** no captain-owned artifact remains; every record has final
   evidence; every lane has final status and the Project parent remains ready for the admiral.

## Return

- To `admiral`: `done` + PR + final review block, or `blocked` + the local Wayfinding record or
  prepared Project-level packet.
- For an atomic Issue: merge SHA + final review block, posted on the parent Issue.
