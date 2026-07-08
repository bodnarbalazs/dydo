---
area: general
type: decision
status: accepted
date: 2026-07-08
participants: [balazs, Leo]
---

# 038 — Auto-Memory Policy: dydo-First Routing, Memory as Buffer, Chief-of-Staff Sweep

Harness auto-memory (the per-user, per-project memory directory the agent runtime maintains **outside the repo**) is kept, but demoted from archive to **buffer**. One routing line in the dydo-generated CLAUDE.md steers writes toward dydo records; a recurring **chief-of-staff sweep** drains the store. Steady state: a handful of human-facts and pending-fix notes, each carrying its own path to obsolescence. This is generic dydo policy for every project, not a DynaDocs-repo special.

## Context

dydo's founding premise ([about.md](../../understand/about.md)): AI-tool memory is "unstructured, opaque, and not under your control" — the fix is repo-canonical, versioned documentation. The harness's auto-memory store is exactly such a parallel memory. balazs approved it in the 2.0 era on the understanding that project material lives in dydo and only *other* useful memory lives in the stack. That boundary drifted: at review time this machine's store held 24 facts, roughly a third duplicating existing dydo records (DR 024, DR 037's addendum, mode-file doctrine).

Two concerns drove this round — **staleness** and **efficiency**:

- Raw context cost is small (only the index is auto-injected, ~1.4k tokens). The real cost is **priors**: the index arrives before onboarding, reads as authoritative, and carries no freshness signal. Case study (2026-07-08): a memory holding a spend-limit-incident workaround outlived the incident and was applied preemptively against current config; the human had to catch it. Counter-example the same day: a human-convention memory prevented an agent from destroying deliberate edits — the category that genuinely belongs.
- Sorting the inventory showed most *behavioral* memories are confessions of a missing dydo default, nudge, or doc — the store was functioning as an invisible per-user bug tracker, hiding demand signal from the board. Memory about how to behave is duct tape; behavior belongs in the framework.

## Decision

### 1. Write-side routing line (all dydo projects)

The generated CLAUDE.md (`TemplateGenerator`) gains one short paragraph, phrased generically:

> Before creating a memory, check whether it belongs in dydo — it probably does (issue, decision, guide, or other record). Keep memory only for facts about your human and for harness mechanics no dydo record can hold. Never store incident state or temporary workarounds as memories.

Routing-shaped, not prohibition-shaped: mid-task capture stays cheap; misrouting — not capturing — is the failure mode.

### 2. What legitimately lives in auto-memory

- **Human-facts** — the assigned human's preferences, conventions, feedback on how to work with them.
- **Harness mechanics** dydo genuinely cannot hold (about the agent runtime itself, not the project).
- **Buffer entries** — mid-task captures awaiting routing. A memory that exists only because a dydo fix is pending must link its issue, so the sweep can retire it when the fix lands.

Never: incident state or workarounds as standing instructions; anything duplicating a dydo record.

### 3. Chief-of-staff memory sweep (a housekeeping duty — not a role, not a CLI feature)

On the existing hygiene rhythm (board hygiene / campaign end), the chief-of-staff walks the memory index and gives every entry one of three dispositions:

- **route** — create the dydo issue/record it belongs in, then delete the memory;
- **retire** — the fix landed or the fact expired: delete;
- **keep** — human-fact, or pending-fix with a linked issue.

The store is outside the repo, per-user, and off-canon by design, so dydo the CLI cannot manage it — the sweep is an **agent duty** written into the chief-of-staff methodology (compiled via `dydo sync`, so every dydo project gets it). Consistent with [DR 032](./032-attention-ledger-and-housekeeping-nudge.md) §7: the standalone housekeeper role stays deferred. Deletions on the first sweep are human-gated; steady-state sweeps report dispositions in the status summary.

## Consequences

- `TemplateGenerator` CLAUDE.md routing line — small code change, tracked on the [auto-memory backlog](../backlog/auto-memory-policy.md). This repo's own CLAUDE.md gets the same line (trivial edit).
- Chief-of-staff methodology gains the sweep item — same backlog.
- Initial manual sweep of this machine's store: routing proposal at `dydo/agents/Leo/notes-memory-sweep-routing.md`, awaiting balazs's sign-off before any deletion.

## Revisit When

- Sweeps repeatedly find nothing to route → the line works; relax cadence.
- The store keeps accumulating project facts despite the line → the harness's own memory guidance is overpowering CLAUDE.md; escalate the wording, or move enforcement into a guard nudge matching memory-directory writes (the guard already sees every Write).
- The harness's memory feature changes shape (built-in expiry, review UI) → re-evaluate the split.
