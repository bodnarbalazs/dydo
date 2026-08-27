---
title: Linear PM Pivot
goal: Replace dydo's repo-mirrored Notion PM system with a Linear-native work graph while keeping durable knowledge, reviewed plans, and delivery evidence in Git.
priority: High
release: 3.0.0
status: active
area: project
type: context
---

# Linear PM Pivot

Active Campaign for replacing the Notion-backed duplicate PM system with a Linear-native work graph and
shipping the simplified boundary as dydo 3.0.

For the DynaDocs dogfood, the reviewed plan intentionally uses only Projects owned by the `Dydo` team.
It does not create a workspace Initiative, keeping dydo strategy out of the main project's workspace
layer on the Basic plan.

## Destination

dydo uses Linear's native Initiative, Project, Issue, Milestone, Cycle, dependency, view, and agent
surfaces for live project management. dydo retains the durable knowledge and delivery contracts that
make agents precise. The old Notion sync, duplicate PM record hierarchy, watcher, and unused merge
machinery are removed only after a lossless cutover and an accepted live pilot.

The result increases autonomous throughput without turning the human into an agent switchboard:
top-level conversations are for work the human expects to steer; autonomous Issues are recognized by
their reviewed fruit and an assimilation brief.

## Settled evidence

- Linear already provides the graph and agent primitives dydo was recreating: Initiatives, Projects,
  Issues, dependencies, views, official MCP, delegation, and Agent Sessions.
- The Basic plan removes the 250-Issue limit and supports managed coding sessions.
- Current dydo PM records encode competing Task/Slice/Issue/backlog models and incompatible statuses.
- The Notion path is a deletion-scale boundary: about 11.5k production lines across provider and generic
  sync, plus 12.6k Notion-specific test lines and broad command/config/docs coupling.
- The detailed discovery and design remain in the campaign's local co-thinking workspace until W1 is
  ratified and promoted into a decision record.

## Settled outcomes

- **W1 — Ownership and ontology (settled 2026-08-27).** Linear is canonical for volatile PM; Git/dydo
  is canonical for durable knowledge and proof; there is no ongoing mirror. The release is 3.0.0 and
  DynaDocs dogfoods the model before the main project.
- **FutureFeature boundary (settled 2026-08-27).** FutureFeature remains a repo-native, unscheduled idea
  record. Human promotion creates a linked Linear Initiative, Project, or Issue; delivery state does not
  sync back into the idea record.

## Waypoints

- **W2 — Concrete dydo migration model (HITL, current frontier).** Remodel the current live records in Linear
  semantics; explicitly disposition the unexecuted M0 plan, open Issues/backlog, stale Tasks, and old
  Campaign, while normalizing the retained FutureFeature schema without promoting ideas.
- **W3 — Safe Notion freeze and export proof (AFK evidence, after W1).** Prove a final full reconcile,
  zero pending writes/shadows, and a committed canonical baseline without deleting remote data.
- **W4 — Rolling delivery Projects (after W2).** The reviewed migration plan refines this Campaign into
  five dependency-ordered Linear Projects. Project 1 is the only initially decomposed Project; each later
  Project receives detailed Issues only after its own fresh plan gate.
- **W5 — Linear dogfood and migration acceptance (HITL).** Run real work through the model while the
  frozen Notion runtime remains available for rollback, and revise only from observed friction.
- **W6 — Runtime removal, 3.0 release, and main-project playbook.** After explicit pilot acceptance,
  delete the Notion/runtime surface, ship the audited release, and produce the exact adoption sequence
  for the main project.

## Fog

- The exact minimal permanent CLI/config surface is deliberately unsettled. Official OAuth-backed MCP
  may eliminate the need for any Linear token, client, watcher, or schema model in dydo.
- The current unfinished work records may be stale intent; status alone cannot decide migration.
- Linear managed coding sessions may complement local Codex/Claude execution, but runtime choice must
  remain capacity- and machine-dependent rather than hard-coded into the PM ontology.
- Assimilation Brief placement (Project update, linked repo report, or both) should be proven in the live
  pilot.

## Out of scope

- Recreating the Notion bidirectional mirror against Linear.
- Treating sessions, worktrees, branches, or subagents as PM hierarchy.
- Bulk-importing completed history or stale runtime tasks.
- Depending on Business-only Linear Releases for dydo's release truth.
- Migrating the main project before this framework's model is dogfooded and accepted.
