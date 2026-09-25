---
area: project
type: decision
status: accepted
date: 2026-09-25
accepted: 2026-09-25
participants: [balazs, Claude admiral]
---

# 052 — `dydo map`: a Read-Only Linear View

`dydo map` is a read-only, on-demand view of one Linear Project. It reads Linear's GraphQL API with
the user's `LINEAR_API_KEY` when the page loads, and it never writes, caches, polls, subscribes to or
mirrors Linear. No state survives the process. [DR 044](./044-linear-canonical-pm-and-dydo-knowledge-boundary.md)'s
ownership rule stands: Linear owns live work, and this command only shows it. Settled by the human for
the [Visual Linear project map](../plans/visual-linear-project-map.md) Project, 2026-09-25.

---

## Context

DR 044 made Linear canonical for live work, ruled out any mirror of it, and allowed no permanent
runtime machinery against Linear without evidence that the official surfaces lack a required
capability. The texts that carried the boundary went further and said no dydo command reads Linear
at all, and that agents reach Linear only outside the dydo runtime.

The human asked for an at-a-glance picture of how a Project lays out now: its issues across all
statuses, parent issues holding their sub-issues, what blocks what, and what is pickable. Drawing
it from Linear's current state means a dydo command must read Linear.

[DYD-261](https://linear.app/bodnar-balazs/issue/DYD-261) established the reads. Three GraphQL
queries cover a Project, and a 150-issue Project costs about 26k complexity points, well inside a
personal key's hourly budget. Linear answers keyless requests on a low-limit unauthenticated tier
rather than rejecting them. A relation keeps type `blocks` after its blocker is resolved; only
Linear's sidebar files it under Related.

## Options Considered

### Option A: Keep the rule that no dydo command reads Linear

- **Pros:** the boundary texts stay as they are.
- **Cons:** no map. The picture stays in the human's head or in hand-drawn diagrams that go stale.

### Option B: A synchronized local copy of the graph

- **Pros:** fast reloads, offline viewing, history.
- **Cons:** a mirror, which DR 044 forbids, with the cache, polling and staleness it retired.

### Option C: A read-only view that fetches on demand (chosen)

- **Pros:** the picture is always Linear's current state; nothing to keep in step; nothing is
  written anywhere.
- **Cons:** every page load costs a round of API reads; the command needs the user's key.

## Decision

1. **Read-only and on demand.** `dydo map` reads Linear's GraphQL API only when the page asks, and
   F5 fetches again. It never writes, caches, polls, subscribes to or mirrors Linear. No state
   survives the process.
2. **The user's key.** Authentication is a personal API key in `LINEAR_API_KEY`. When it is
   missing, the command exits 2 before any request.
3. **The key stays in the CLI.** The CLI serves the viewer on `localhost` and makes every Linear
   request itself. The browser talks only to `localhost`.
4. **DR 044's ownership rule stands.** Linear owns live work. This command is a narrow exception to
   the wording "no dydo command reads Linear", not to who owns what.
5. **The human's display policies:**
   - Archived issues are excluded, together with every relation touching one.
   - An external blocker outside every Project is a dashed node whose body only focuses it; its
     Linear button still opens it in Linear.
   - A resolved blocker keeps its `blocks` edge, drawn resolved and muted. The map shows the
     current relation and infers nothing.

## Consequences

- **Gained:** the human sees a Project's live shape on demand, from Linear's data and nothing else.
- **Accepted:** dydo now contains a Linear client, bounded by decisions 1 to 3. The texts that said
  no dydo command reads Linear, or that agents reach Linear only outside dydo, are narrowed to name
  this exception.
- **Unchanged:** no dydo command creates, updates, provisions or mirrors Linear objects. Any
  command that would write to Linear, or keep its state, still needs its own decision.

---

## Supersedes and amends

Amends [DR 044](./044-linear-canonical-pm-and-dydo-knowledge-boundary.md), Integration posture:
`dydo map` is the one permanent dydo component that talks to Linear's API, and it only reads.
DR 044's canonical ownership and its no-mirror rule are unchanged.

## Affects

- [Architecture Overview](../../understand/architecture.md)
- [Work Model](../../understand/work-model.md)
- [Adding a Command](../../guides/adding-a-command.md)
- [Troubleshooting](../../guides/troubleshooting.md)
- [DynaDocs](../../reference/about-dynadocs.md)
- [Visual Linear project map](../plans/visual-linear-project-map.md)
