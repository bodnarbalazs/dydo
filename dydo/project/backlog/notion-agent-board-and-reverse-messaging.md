---
area: general
type: context
status: open
created: 2026-07-07
created-by: Adele
origin: balazs — "a live board of agents in Notion (dydo agent list as a Notion view), and message agents FROM Notion by creating a row that the guard delivers like an agent message."
related-decisions: [025, 029, 030, 034]
---

# Notion Agent Board + reverse (Notion → agent) messaging

FutureFeature-class (campaign-sized). Two halves; the first is cheap, the second is the real design work.

## Half A — live Agent board (read: repo → Notion)

A live view of every agent and what it's working on — `dydo agent list` as a Notion database.

**Feasibility: high.** The spine sync already turns repo `.md` records into filterable Notion DBs (DR 025/029). Agent state already lives as files (`dydo/agents/<name>/state.md` + workspace). So: add an **`Agent`** spine object type (`sync-model`) whose rows derive from agent state — properties: `name`, `status` (free/working/dispatched/needs-human), `role`, `task` (relation → the new `Task` DB from DR 034), `assigned-human`, `waiting-for`, `last-activity`. Views: "Working now", "Needs human" (board grouped by status), "By human". The `needs-human` flag (DR 030) becomes a first-class board signal — the human sees raised hands in Notion without a terminal.

**Wrinkles:**
- Agent state is *runtime/ephemeral* — unlike PM records it changes second-to-second. Either sync on a tick/on-change (not the current file-driven `dydo notion sync` cadence) or accept staleness. `last-activity` + a "stale" formula (like SprintTask) softens it.
- `state.md` is **off-limits** to all agents (guard) — the sync path (a command, not an agent) must read it; fine, but the model/loader must special-case a runtime source rather than a normal doc dir.
- Whether Agent rows are canonical files or a projection: they're a *view* of live state, not editable records — closer to a computed/engine-owned DB than a normal spine type.

## Half B — reverse channel (Notion → agent message)

Message an agent *from Notion* by creating a row/page (or a property edit) that gets turned into a file which the guard delivers as an inbox message, identical to an inter-agent `dydo msg`.

**Feasibility: medium — this is the design-heavy half.** The spine sync is already **two-way for data values** (DR 025), so Notion→repo writes exist. The path: a Notion-authored message row → sync pulls it down → writes a `dydo/agents/<to>/inbox/<id>-msg-*.md` in the exact inbox schema → the target agent's `dydo wait` / guard delivers it like any message.

**Open questions to settle first:**
- **Authorship/trust.** An inbox item has a `from:`. A Notion-authored message is from *the human*, not an agent — needs a distinct sender identity (e.g. `from: balazs@notion`) and the guard must trust it without an agent claim. Security surface: anyone with board access can now inject messages.
- **Trigger + latency.** Notion→repo currently syncs on a `dydo notion sync` run, not a webhook. Real-time delivery needs polling or Notion webhooks; batch delivery is simpler but laggy.
- **Delivery semantics.** Dedupe (don't re-deliver the same row each sync), delivered-state write-back, and what happens if the target agent is free/asleep (does it dispatch, or queue?).
- **Schema.** A dedicated "Messages" DB vs a message property on the Agent row. A DB is cleaner (one row = one message, has `to`/`body`/`delivered`).

## Suggested shape
- **Ship Half A first** — high value (live attention board), low risk, rides existing spine machinery. Natural follow-on once DR-034's `Task` DB + the runtime→board bridge (`notion-board-followups.md` §A) land.
- **Half B as its own scoped design** (co-thinker pass) — the trust/trigger/dedupe questions deserve a decision record before implementation.
