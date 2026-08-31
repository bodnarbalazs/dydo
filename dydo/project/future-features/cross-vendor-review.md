---
title: Cross-Vendor Review
area: project
type: concept
status: idea
---

# Cross-Vendor Review

Bind the reviewer to a different vendor's model than the writer, so a candidate written on Claude is
judged on Codex and the reverse.

## Today

DR 045 §3 makes independence of *context* the requirement, not independence of vendor: same-vendor
review is acceptable as long as the reviewer is bound to the strong tier and arrives fresh. The
binding is one shared role → tier map in `dydo.json` (`reviewer: strong`), resolved at compile time
through `models.tiers[<vendor>]` — `Services/ConfigFactory.cs` `CreateDefaultModels` ships
`anthropic.strong = claude-fable-5` and `openai.strong = gpt-5.6-sol`. `dydo sync` therefore writes
the anthropic model into the Claude agent and the openai model into the Codex one, and a reviewer
runs on the vendor of the session that spawned it.

## What the feature adds

A reviewer whose vendor is chosen against the writer's rather than inherited from the spawner.

Two things would have to exist. First, per-host model bindings in `dydo.json`: today one role map is
read through whichever vendor table the host compiles against, so there is no way to say "the
reviewer is openai wherever it runs". Second, a way for a session on one host to spawn an agent on
the other — dydo owns none, and each host spawns its own vendor's models. Evidence needs nothing
new: the review block already carries `Reviewer: <label> (<model>)` on every Issue and PR.

## The evidence that would justify it

The recorded models in review blocks. With writer and reviewer models both on the record, the
question becomes answerable rather than assumed: does one vendor's reviewer keep passing what the
other's would have caught? A run of findings that only ever appear when the judge changes vendor is
the case for crossing; its absence is the case for leaving it alone.

## Trigger

Pick this up once spawning a stateless reviewer is routine, per DR 045's consequences, and once
enough review blocks exist to compare verdicts by model.

## Rationale

FutureFeature is a repo-native idea record. It remains unpromoted until the human promotes it to
exactly one Linear Initiative, Project, or Issue.

## Related

- [DR 045 — Flow Map, Hats, Review Tiers, and the Working-Tree Contract](../decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md) — The review tiers, the review block, and why same-vendor review is acceptable today
- [DR 037 — Cross-Vendor Dispatch: Same-Vendor Default](../decisions/037-cross-vendor-dispatch-same-vendor-default.md) — Why vendor binds where a session is created, not on a role
