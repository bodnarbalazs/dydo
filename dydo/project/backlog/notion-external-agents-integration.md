---
area: general
type: backlog
status: open
created: 2026-07-08
created-by: Adele
origin: balazs — "first class support for external agents (claude) inside notion... we should learn what this exactly means and how it applies to us. This could be a game-changer!"
related: [notion-agent-board-and-reverse-messaging, cross-vendor-agent-integration]
related-decisions: [034, 035]
---

# Notion 3.6 External Agents — what it is and how it applies to dydo

Research snapshot 2026-07-08 (Adele, from notion.com/releases/2026-07-01 + the External Agents
help category). Beta, moving target — re-verify before designing.

## What it actually is

- **"Orchestrate External Agents (Claude, Cursor, and more soon)"** — Notion 3.6, GA 2026-07-01,
  agents beta. Claude and Cursor are the first two External Agents.
- **Notion-HOSTED**: "Claude agents are hosted by Notion via Anthropic's infrastructure."
  "No Anthropic account is required (using your own Anthropic account isn't possible)."
  Billed in Notion credits; Business/Enterprise plans. NOT a registration API for self-hosted
  agents — our dydo agents cannot appear as External Agents today ("more soon" is the watch item).
- **Task-board loop** (pre-configured "Coding task board" use case): move a card to
  `Ready for Agent` → "Claude picks it up automatically"; or @-mention `@Claude` in a task comment;
  batch = move several cards. Board statuses: Backlog / Ready for Agent / In Progress / In Review /
  Done. Agent "posts updates to the task card"; humans can "jump in mid-run" via comments.
- **Delivers code via GitHub PRs**: agent uses a PAT (Contents + Pull Requests read/write) to "read
  repositories, propose changes, and open pull requests" — diff + description + screenshot.
- **Limits**: per-agent permissions ("only see what you share"); cannot browse the web; cannot
  "call other agents during a session"; no zero-data-retention.

## How it applies to dydo — three angles

1. **Our Notion Task board becomes an assignment surface (cheap, near-term).** DR-034 lands a Task
   DB; the spine sync already detects external row edits. A human moving a Task row to a
   "ready" status in Notion → sync pulls the status change → dydo dispatches locally. That is
   [[notion-agent-board-and-reverse-messaging]] Half B, implementable with machinery we already
   have — no webhooks, no Notion-side agent needed. Notion 3.6 just made this UX pattern the
   industry-standard one; our board matching it is now table stakes.
2. **Hosted Claude as a light-work satellite (optional, evaluate).** A Notion-hosted agent with a
   repo PAT could take PM-adjacent/doc tasks from the same board and deliver PRs — review-gated by
   construction, no dydo identity/guard, paid in Notion credits. Open questions: cost, quality
   without our test gates, whether PR-only delivery fits any real dydo task class. Could also be
   the Notion→dydo bridge (agent triggered by board event writes a repo file via GitHub → dydo
   picks it up), though angle 1 makes that mostly redundant.
3. **Watch: External Agent registration ("more soon").** If Notion opens registration so
   self-hosted agents can be External Agents, dydo agents become @-mentionable Notion teammates
   natively — that would supersede much of Half B's custom design and is the true game-changer
   case. Track developers.notion.com and the releases feed.

## Friction/risks

- Beta + hosted-only + Business/Enterprise plan + credits pricing (unknown real cost).
- Vocabulary collision: their canned board statuses vs DR-034 vocab (`backlog/in-progress/
  in-review/done` — nearly identical, deliberate alignment is cheap and worth it in S2a review).
- DR-036 just retired our human approve gate; a Notion-agent PR flow adds an external work
  source whose gate is the GitHub PR — decide where that meets the dydo lifecycle.

## balazs ruling (2026-07-08, same day)

"It's not that great." Angle by angle:
1. **ADOPT the pattern** — "good idea to adapt the pattern, it probably works, that's why it's
   there." Fold the move-a-row-to-ready assignment UX into the agent-board / DR-034 track.
2. **REJECTED** — "no need to divide things. Claude Code stays. no mix." No hosted-Claude
   satellite workforce; dydo work runs on Claude Code agents only.
3. **PARKED** — "maybe later it will be a game-changer. For now it's not that important."
   Watch item only (External Agent registration opening up).

No co-thinker round needed; the ruling replaces it. Design lands inside
[[notion-agent-board-and-reverse-messaging]] Half B when that campaign runs.
