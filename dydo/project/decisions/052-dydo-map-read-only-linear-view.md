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
the user's `LINEAR_API_KEY` when the page loads. It never writes, polls, subscribes to or mirrors
Linear. The 2026-10-04 amendment below permits disposable saved maps across process restarts. [DR 044](./044-linear-canonical-pm-and-dydo-knowledge-boundary.md)'s
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
   F5 fetches again. It never writes, polls, subscribes to or mirrors Linear. The amendment below
   permits one disposable saved map per opened Project.
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
  command that would write to Linear or retain state beyond the display cache below still needs its own decision.

---

## Amendment 2026-10-04 — disposable saved maps

The human approved [DYD-314](https://linear.app/bodnar-balazs/issue/DYD-314) and the
[map change memory Project](https://linear.app/bodnar-balazs/project/dydo-map-change-memory-313-b8ac698c6886):
revisits show what the map looked like last time before animating fresh changes. This supersedes
this record's original no-cache and no-surviving-state clauses. Linear remains authoritative.

A successful complete fetch replaces one machine-local snapshot for the opened Project. The
server owns this disposable cache outside repositories, under the OS user's local application data
folder (`dydo/map`). A SHA-256 partition of the normalized effective endpoint and exact API key
isolates credentials and endpoints; a hashed canonical Project ID names each file. Credentials are
never stored in envelopes or sent to the browser. Key rotation starts cold.

Versioned, source-generated JSON envelopes contain the complete normalized graph, Project identity,
and UTC fetch timestamp. Invalid or inaccessible envelopes are misses. Writes use unique sibling
temporary files and atomic replacement; failed writes leave a valid old snapshot intact. Same-Project
fresh reads serialize through commit in one process; across processes the last successful complete
commit wins. Cache I/O never makes a successful fresh read fail. The cache root is injectable for tests.

Opening a Project concurrently requests its saved snapshot and a fresh graph. `/api/graph` always
reads Linear; `/api/saved` reads only the cache. Requests run independently and drain on shutdown.
After the saved canvas has laid out, fitted its viewport and rendered, a monotonic two-second
viewing window precedes all fresh changes and the existing refresh cascade. Every visit owns its
requests, layout and hold, including A→B→A. A late saved response cannot replace accepted fresh data.
A failed saved layout falls back to ordinary fresh loading. Unchanged data settles quietly;
reduced motion keeps the viewing window and suppresses movement.

While saved data is shown, its timestamp remains visible. A failed fresh fetch retains it with a
failed-to-fetch warning and retry through Refresh. Successful fresh data clears the warning.
No history, unopened-Project prefetch, polling, background synchronization or Linear writes are added.
The trade-off is intentionally stale display for a short comparison window, clearly timestamped,
in return for continuity across visits and restarts.

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
