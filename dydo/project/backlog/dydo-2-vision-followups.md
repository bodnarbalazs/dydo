---
area: project
type: context
---

# dydo 2.0 — Vision Follow-ups (backlog)

Items that emerged from the 2026-07-03 co-thinking session ([Decision 026](../decisions/026-tier1-managers-doctrine.md), Brian's notes) that are approved in direction but deliberately **not** in the current implementation sprints. Nothing here should be forgotten.

## Approved, sequenced later

- **Docs-tree → Notion body sync.** Balazs overruled the "PM plane only" position: docs sync to Notion too (canonical stays repo; PM objects need doc pages as relation endpoints; doc churn is low). Rollout order agreed: Release + Issue objects first (in-sprint), then decisions, then the rest of the docs tree. Gate: the block↔markdown converter must pass a **round-trip idempotence test** (`export(import(x)) == x`) so a no-edit sync tick never manufactures diffs.
- **`dydo ask` — answer-from-Notion loop.** Questions become documents: agent blocked on a decision writes a question file with options (or a Tier-2 raise-hand materializes a Blocker per 025 §7); sync pushes to Notion; human sets the Answer property from anywhere; sync pulls it back; the agent's file-watching wait fires with the answer. Turns the board from dashboard into cockpit; works identically from Obsidian. Needs: question/blocker `answer` field, a nudge/skill instruction to externalize decisions when the human isn't at the terminal, and run-sprint escalation optionally blocking on the answer file.
- **Idea funnel.** Any capture surface (Notion row, Obsidian file, CLI one-liner) lands as a domain-tagged `status: backlog` task via existing sync; domain orchestrators pull from their queue; the chief-of-staff triages. Depends on: chief-of-staff mode (in-sprint), Release/Issue model work.
- **Chief-of-staff watchdog materialization.** The `msg --to chief` address always works; when no chief process is live, the watchdog launches one via top-level dispatch (message-to-unclaimed-well-known-identity trigger). Sequenced after the chief mode artifacts exist.
- **Agent-first install doc.** A fresh Claude on a fresh repo, told "add dydo," guides the human through everything (env vars, PATs, Notion token) without the human reading docs. Acceptance test is automatable (agent-driven install E2E). Secrets stay human-entered.
- **Codex paving (not building).** Keep the compiler multi-target at the seam; optional cheap win: emit `AGENTS.md` from the same role/doc sources. Guard rule engine stays separated from hook transport. No enforced guard on non-Claude runtimes until a real need exists (024's revisit clause applies).

## Already tracked elsewhere

- **Git-derived Files-Changed at `dydo task approve`** — in Adele's `dydo-2-hardening.md` backlog (feature lost in the audit teardown, `dcf42c7`; important for bug hunts).

## Watch items (not work yet)

- Stranded worktrees from pre-merge-fix run-sprint runs (`.claude/worktrees/wf_*`) — audit once the merge phase lands; salvage or clean.
- Sprint-auditor cost/benefit — revisit the default-on flag if sprint-auditor findings prove rare.
- Chief-of-staff bureaucracy creep — 026's revisit clause.
