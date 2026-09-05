---
name: walkthrough
description: Show me what landed, where to look first, and how to try it.
---

# Walkthrough

Give the human the tour of what landed.

The admiral runs the Walkthrough Issue with the human after landing. Its findings decide whether
the Project needs another lap.

The argument names what to walk — a feature branch, a Project, an Issue, a diff range. Ask which one
when it is missing.

Walk it yourself first: the diff and its hops, the Issues it closed with their review blocks, the
DRs those Issues cite, the returns their workers posted. Then brief the human in four parts.

- **What changed and why** — the outcome in one paragraph, each part traced to the Issue that carried
  it and the DR that decided it, with the shape drawn by `show-me` where a tree or a diff says it
  faster than prose.
- **Where to look first** — a route for their own file-by-file pass: the files that drew findings,
  then the ones a worker left an open doubt on, then the seams the change moved.
- **How to try it** — the exact commands to build it and see the new behaviour, in order, each one run
  once by you so it is true when they type it.
- **What reviewers flagged or deferred** — the findings the review blocks recorded and where each
  deferred one now lives, reported as they stand.

The tour ends when the human has inspected the files and tried the behavior. Record findings as
Issues in the same Project; the admiral commissions the first fix Captain to re-cut its feature
branch from main for fixes and another landing/walkthrough. An empty walkthrough lets the Project close.

Keep the brief in the terminal, or in a scratch file when the human wants to read it beside the diff.
The durable record stays where it already is: the Issues, the review blocks and the DRs the tour
points at.
