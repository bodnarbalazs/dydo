---
area: general
type: decision
status: proposed
date: 2026-06-17
participants: [balazs, Adele]
---

# 025 — Notion Sync: Canonical Files + a Swappable View Adapter

dydo's repo files stay the **single source of truth**. External UIs are **views** onto that truth: Obsidian and the IDE edit the canonical files directly (no adapter), and Notion is **one swappable adapter** behind a generic interface. A Notion-agnostic **sync engine** reconciles canonical files against any external view **bidirectionally**, using a Git-style base-snapshot + 3-way merge, so edits made anywhere — including by a colleague in Notion — converge without manual pull/merge. The guiding constraint is **don't get captured by Notion**: if the Notion adapter were deleted tomorrow, everything must remain intact in the repo. This implements the watchdog→Notion-sync piece deferred by [Decision 024](./024-dydo-2-native-pivot.md).

## Context

With the 2.0 pivot, the human's role shifts from operator to **PM of agents** — managing not just current work but the big picture: campaigns, tasks, requirements, blockers, priorities, progress. That wants a real management UI, and Notion's databases provide one **without us building or maintaining it**. But a UI we don't control must not become a dependency we can't escape: the human will keep editing raw files in Obsidian and the IDE as much as editing in Notion, and may one day swap Notion for a custom UI or Obsidian-with-plugins. So Notion has to be a thin, replaceable edge — never the center.

The naive framing ("repo is canonical, Notion is a view, sync both ways") hides a contradiction: if the repo always wins, Notion edits are second-class and can be clobbered. This decision resolves that.

## Decision

### 1. Canonical core, swappable adapter
Repo files (markdown + frontmatter) are truth. The sync engine is **Notion-agnostic** and speaks a small adapter interface (list objects + external state + external IDs; apply a change set; report external changes since last sync). Notion is one adapter implementing it; all Notion-specific knowledge (DB schema, property types, block↔markdown conversion, API quirks) lives **inside that adapter and nowhere else**. Obsidian and the IDE need **no adapter** — they edit canonical files, which the engine already observes. The acceptance test for any change: *delete the Notion adapter and the repo is still whole.*

### 2. Sync the data, not the presentation
The canonical repo owns **every field and relation** as plain frontmatter (`status`, `priority`, `blocked-by: [id]`, `sprint: …`). Notion owns **disposable presentation** — boards, filtered views, timelines, rollups — which is Notion-specific and **not synced back**. Swapping to another UI keeps 100% of the data and rebuilds only the views.

### 3. Bidirectional via base-snapshot + 3-way merge
The engine keeps a **shadow copy of the last-synced state** per object. Each tick: diff `base→repo` and `base→external`. One side changed → apply it to the other. Both changed → **3-way merge against the base** (the `git merge-file` strategy), write the merged result to **both** sides, advance the base. Non-overlapping edits (the common case) merge automatically with no human and no pull; genuinely overlapping edits to the same lines get a deterministic winner **plus a visible conflict record**, never a silent clobber. This is what makes "a colleague edits in Notion and it lands in the repo without me merging" both true and safe.

### 4. No ownership or directionality policy
All authored content is **uniformly bidirectional** — edit in Obsidian, Notion, or the IDE; the merge reconciles. We deliberately do **not** reintroduce per-field write-ownership (that would be the RBAC [024] deleted). The only things that aren't symmetric aren't policy restrictions — they simply aren't synced documents:
- **Rollups / progress** are **computed at the edge** (Notion rollup, Obsidian Bases formula, dydo on render). Nothing is stored, so there is nothing to own. Editing a rollup isn't forbidden; it just isn't a thing, like editing a formula's output cell.
- **The live Agent board** ("who's doing what right now") is a one-way **telemetry feed**, system→Notion, because it mirrors a running process rather than a document. If unwanted, it simply doesn't exist.

### 5. The sync engine is a command; triggers are just callers
The engine ships as a plain, transport-agnostic command (`dydo notion sync`). The **watchdog calls it when alive** (it already sees file changes), and a **cron/CI bridge calls it otherwise** (so Notion→repo reconciles even when no agent is running). In a multi-machine team, **one designated bridge** runs the Notion↔git sync so two syncers don't race to commit the same change. "The watchdog does the sync" becomes "the watchdog is one caller."

### 6. Notion access: direct REST, source-generated JSON, no SDK
Talk to the Notion REST API directly via `HttpClient` + source-generated `System.Text.Json` DTOs (the existing `DydoJsonContext` pattern), behind our `INotionClient`. Rationale: (a) **Native AOT** — the unofficial .NET SDKs use reflection-based serialization, an AOT liability; (b) **loose coupling** — a third-party SDK couples us to both Notion's model and the library's choices, which is exactly what §1 forbids. The surface we use is small (query a database, create/update a page, read/write block children). The `Notion-Version` header is pinned and the current data-source API shape is confirmed against live docs at build time. The token comes from `DYDO_NOTION_TOKEN` (env var or gitignored local config) and is **never written to a committed or synced file**.

> **Superseded in part by [DR 035](./035-docs-body-sync-via-notion-native-markdown-api.md) (2026-07-08):** the *custom* in-house block↔markdown conversion mandated here proved lossy enough to corrupt the repo via phantom conflicts (issue 0235). Notion's **native Markdown Content API** (`GET/PATCH /v1/pages/:id/markdown`, `Notion-Version: 2026-03-11`) now does that mapping server-side, so body sync uses it and drops the converter. Everything else in §6 — **direct REST, no SDK, source-generated JSON, AOT** — is unchanged; only "we convert blocks↔markdown ourselves" is retired.

### 7. PM object model (strawman — fields refined during build)
Spine **Campaign → Sprint → Task**, with **Requirements** and **Blockers** hanging off Tasks/Sprints, and **Agent** as a live read-only projection.

- **Campaign** — initiative: title, goal, status, priority, → Sprints, decisionRef.
- **Sprint** — task bundle capped by a QA gate: title, seq, → Campaign, status, gateResult (the inquisition verdict).
- **Task** — unit of work: title, brief, → Sprint (optional — backlog tasks may float), status, priority, role/assignee, dependsOn, plus agent-produced progress/filesChanged/reviewOutcome/escalation.
- **Requirement** — what must be true: statement, type (functional|constraint|acceptance), status, satisfiedBy → Tasks; may attach at Campaign (product) or Task (acceptance) level.
- **Blocker** — something stopping progress: detail, status, severity, raisedBy, blocks → Task|Sprint. **Standout: a worker's raise-hand (from `run-sprint`/`inquisition`) auto-materializes a Blocker**, so agent escalations surface directly on the PM board.
- **Agent** — live projection (read-only telemetry per §4): name, role, status, currentTask.

## Consequences & open items (resolve during build)
- **Body fidelity**: Notion block↔markdown conversion is lossy; bodies use best-effort conversion + 3-way *text* merge, while structured frontmatter↔properties is the clean, reliable path.
- **ID mapping**: where the dydo-object ↔ Notion-page-id link lives (frontmatter `notion-id` vs a sidecar map) — decide in the first slice.
- **Base-snapshot store**: location/format of the per-object shadow state (must be gitignored or kept out of the canonical tree to avoid syncing the shadow).
- **API evolution**: pin `Notion-Version`; confirm the data-source model before wiring live calls.
- **First validation** runs against a throwaway scratch workspace, not the real one.

## Status
Proposed. First slice: the Notion-agnostic engine (object model + base-snapshot store + 3-way reconcile), the `INotionClient` adapter interface, the REST client (auth + the handful of endpoints + source-gen DTOs), and a `dydo notion` command shell — all fixture-tested, no live calls — then a live smoke test against a scratch workspace once `DYDO_NOTION_TOKEN` is set.
