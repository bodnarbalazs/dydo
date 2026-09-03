---
area: project
type: decision
status: accepted
date: 2026-08-27
accepted: 2026-08-27
participants: [balazs, Codex]
---

# 044 — Linear-Canonical PM and the dydo Knowledge Boundary

Defines the canonical boundary between Linear's live work graph and dydo's durable repository knowledge,
including FutureFeature intake and the dydo 3.0 migration posture.

## Decision

Linear is canonical for volatile project management. dydo/Git is canonical for durable knowledge,
reviewed delivery contracts, and proof. There is no ongoing bidirectional mirror between Linear and
repo PM records.

This is a breaking ontology and product boundary change and ships as **dydo 3.0.0**. DynaDocs will
dogfood the model before the main project migrates.

## Canonical ownership

### Linear

Linear owns Initiatives, Projects, Issues, optional Milestones and Cycles, workflow status, priority,
dependencies, assignment/delegation, agent execution state, blockers, current updates, and attention
views. A session, worktree, branch, worker subagent, or reviewer attempt is execution evidence attached
to an Issue, never another PM object.

The live work hierarchy uses Linear's native nouns:

- **Initiative** — a broad goal spanning independently meaningful Projects.
- **Project** — one bounded product or technical outcome.
- **Issue** — the only actionable tracked work item. Type is expressed by template/label, not a separate
  record class.
- **Milestone** — an optional meaningful checkpoint inside a Project.
- **Cycle** — an optional repeating capacity timebox, orthogonal to Projects.

Campaign, Sprint, Slice, Task, and the separate observed-problem Issue type retire as canonical PM
records. Slice may remain a planning verb or implementation technique. Waypoint remains an optional
navigation term and is not a Linear entity.

### dydo/Git

The repository owns Decision Records; architecture and product doctrine; reviewed Project
plans/specifications; audit and inquisition reports; assimilation briefs; changelog and Git-tag
release history; and optional Wayfinding maps while committed work still contains Fog. Decision
Records remain canonical in dydo: Linear carries the question and links to the resulting record rather
than copying it.

Linear links to these artifacts. New durable knowledge discovered during work must flow back into dydo
rather than remain trapped in a Linear comment or agent session.

## FutureFeature is retained in Linear

FutureFeature remains a distinct record type for an unscheduled strategic possibility; it is not a
generic idea or delivery contract. Its canonical record is a Linear Issue labelled `FutureFeature`,
kept in `Backlog` without a Mode or Issue Captain until the human promotes or cancels it.

Promotion preserves one source of truth. An Issue-sized FutureFeature becomes the delivery Issue by
changing its Type and entering the delivery loop. A Project- or Initiative-sized FutureFeature creates
and links that native Linear record, then closes as promoted provenance. Only the human may promote it.

## Planning and acceptance

The prohibition becomes **no implementation without reviewed intent**:

- one atomic, autonomous-ready Issue may itself be the reviewed contract;
- coordinated, cross-cutting, or architecture-sensitive work receives a linked repo Project plan;
- every implementation Issue receives independent agent review before human harmonization;
- a Project completes only after an integrated audit against its linked plan/specification;
- acceptance includes an assimilation brief proportionate to the semantic change.

HITL/AFK describes whether live human participation is required to produce the work. It does not decide
whether the human will later inspect or accept the result.

## Integration posture

Use Linear's official OAuth-backed MCP, agent integrations, API/webhooks, and native UI. Do not rebuild
the Notion adapter against Linear, retain the polling watchdog, synchronize Markdown bodies, or provision
a duplicate PM schema. A one-time migration may use Linear's API/MCP but must not create permanent
runtime machinery without evidence that the official surfaces lack a required capability.

Git tags and changelog remain release truth. Linear Releases are optional projection where a workspace
plan supports them, never a dydo dependency.

## Migration posture

- Reconcile Notion one final time and resolve pending writes/conflict shadows before cutover.
- Import only live, human-ratified work; do not bulk-import completed history or stale runtime Tasks.
- Migrate each retained repository FutureFeature to one Linear Issue before removing its source file;
  preserve any durable knowledge separately rather than copying the FutureFeature body into dydo.
- Keep completed plans, decisions, reports, and changelog in Git.
- Do not delete the remote Notion workspace during migration; retain it as rollback evidence until the
  Linear pilot is accepted.
- Preserve stable provenance links or a one-time mapping manifest where durable records refer to retired
  work-record paths.

## Consequences

- The Notion adapter, generic sync engine with no remaining consumer, watchdog, token/vault surface,
  sync schema, and their tests become deletion candidates.
- The repository FutureFeature folder, template, validator, and promotion fields become deletion
  candidates after the retained records have migrated to Linear.
- The planner, orchestrator, reviewer, Wayfinder, chief-of-staff, and co-thinker methodologies must adopt
  Linear-native work nouns and the new reviewed-intent gate.
- The current final-audit workflow must receive the linked Project plan; passing only leaf briefs and a
  diff is insufficient.
- Existing projects require a deliberate migration, so removing the old commands and record contracts is
  correctly versioned as 3.0.0 rather than a minor release.

## Supersedes and amends

This decision supersedes DR 025, 029, 030, 033, 035, and 043, plus the Notion-as-view and repo-PM-record
parts of DR 041. It supersedes DR 042's mandatory Sprint-root/Slice shape while retaining its plan gate
as the reviewed-intent rule above. Its 2026-09-01 amendment moves FutureFeatures from dydo to Linear,
superseding the repository-home rulings in DR 023, 034, and 040 while retaining the distinct
FutureFeature type and human-only promotion rule. Wayfinding's Fog/frontier distinction remains.
