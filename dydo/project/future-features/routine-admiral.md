---
title: Routine Admiral
area: project
type: concept
status: idea
---

# Routine Admiral

An admiral that wakes on a cadence and keeps a Project moving while nobody is watching, instead of
only for as long as a human keeps a session open.

## Today

`admiral` is a hat, compiled as an explicit-only skill: a human invokes it, and it carries one
approved Project plan to a feature branch he can land. Inside that one session it opens the feature
branch, keeps N Issues in flight by spawning an `issue-captain` per pickable Issue, merges serially,
runs a merge review after every merge, amends the plan as fog clears, routes what the plan cannot
answer, and offers the inquisition. When the session ends, the Project stops advancing.

## What the feature adds

One trigger: a cadence. The same method, started without a human present — read the Project's live
state from Linear, top the in-flight Issues back up to N, merge what passed, and stop. Progress
between the human's gates stops depending on his attention being on a session.

## What it must not change

- **The human's gates.** Plan approval, HITL question Issues, escalations that survive the ladder,
  inquisition confirmation, and the feature → main merge stay his. A routine admiral runs *between*
  gates and never passes one; unattended is not unreviewed.
- **The review block.** Issue review before every merge and merge review after it, each from a fresh
  reviewer, each posted on the Issue and the PR. A cadence is not a reason to merge on a claim.
- **One writer per worktree.** Assignment is the claim, and the working-tree contract governs
  branches, base SHAs and cleanup exactly as it does now.
- **The conductor plays no instrument.** A routine admiral coordinates; it still never implements.

## Trigger

Pick this up when the admiral has carried whole Projects end to end with the human reached only at
his gates, and when a session can be started on a schedule without a human attached to it. Before
both are true, a cadence only makes unreviewed work arrive faster.

## Rationale

FutureFeature is a repo-native idea record. It remains unpromoted until the human promotes it to
exactly one Linear Initiative, Project, or Issue.

## Related

- [DR 045 — Flow Map, Hats, Review Tiers, and the Working-Tree Contract](../decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md) — The admiral hat, the review tiers, and the human's gates
- [Working-Tree Contract](../../guides/working-tree-contract.md) — Branch, worktree and cleanup rules a routine admiral still obeys
