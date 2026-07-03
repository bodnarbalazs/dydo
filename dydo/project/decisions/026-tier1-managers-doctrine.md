---
area: general
type: decision
status: accepted
date: 2026-07-03
participants: [balazs, Brian]
---

# 026 — Tier-1 Agents Are Managers: Code Writes Happen in Workflows

Every Tier-1 agent (the named agents the human talks to — see [024 §1](./024-dydo-2-native-pivot.md)) is a **manager of its slice, not an implementer**. By default, Tier-1 agents do **not** write code. All code writing happens inside **dynamic workflows** (`run-sprint` and kin) executed by Tier-2 worker agents, with one exception: **trivial edits** (typo-level, one-liner config, a doc link) may be made directly. The Tier-1 mode set is **orchestrator / co-thinker / chief-of-staff**.

## Context

dydo 2.0 keeps a small pool of named Tier-1 agents (terminal sessions the human converses with) above anonymous Tier-2 workers spawned by workflows. Nothing so far pinned down *who is allowed to implement*. The human's operating doctrine from 1.x — "I talk to managers; code-writing happens in workflows" — was never written anywhere, and worse, the docs still encode the **pre-pivot inverse**: `guides/coding-standards.md` ("never let sub-agents write code... always use `dydo dispatch`") reflects the 1.0 world where worker-tier dispatch existed. Decision 024 removed that machinery; the guidance it assumes is now actively wrong.

## Decision

### 1. Tier-1 = managers, by default
A Tier-1 agent's job is direction, coordination, judgment, and capture: running workflows, triaging, messaging peers, maintaining PM objects and docs. Implementation is delegated to workflows, which bring the quality machinery for free (code↔review loops, review cap, raise-hand, worktree isolation, and — once wired — merge + sprint-audit).

### 2. The trivial-edit exception
Direct edits by a Tier-1 agent are acceptable when a workflow would be ceremony: typo fixes, single-line config toggles, doc-link repairs. Rule of thumb: *if it needs a reviewer, it needs a workflow.*

### 3. The Tier-1 mode set: orchestrator / co-thinker / chief-of-staff
This resolves the roadmap's open "single mode" question — the third mode is the **chief-of-staff**:
- **orchestrator** — owns a domain/campaign; runs workflows; primary interface for work in its slice.
- **co-thinker** — thinks with the human; captures decisions; no implementation.
- **chief-of-staff** — the human's right hand: backlog/idea-funnel triage and routing to domain orchestrators, status reports ("what do I do next"), mediation between agents, board hygiene. **An address, not a hop**: `dydo msg --to chief` always works for every agent; if no chief process is live, the watchdog materializes one via top-level dispatch (on-demand spawn). The human typically starts one at the beginning of a session regardless. Invariants: never in an approval path; the human's primary interfaces remain the domain orchestrators; writable surface is PM objects/docs, never code.

### 4. Enforcement: soft nudge, not RBAC
No path matrix returns. The guard already distinguishes tiers (absence of `agent_type` = Tier-1, per 024). A **soft nudge** fires when a Tier-1 agent Edits/Writes source paths (`{source}`/`{tests}`): a reminder that managers delegate to `run-sprint` unless the change is trivial. Warning, not block — the trivial-edit exception stays frictionless, and the lesson is taught at the moment of temptation (the guard's codified-taste pattern).

## Consequences

- **Doc fix required:** `guides/coding-standards.md` sub-agent guidance ("never let them write code / always `dydo dispatch`") must be rewritten to the inverse; a sweep for other 1.0-era delegation guidance rides along (docs-writer task).
- **Nudge to add:** Tier-1 source-write reminder (see §4) in the shipped defaults.
- **Skills/agents:** generated Tier-1 mode skills state the doctrine; worker skills already assume workflow context. A `chief-of-staff` mode skill joins orchestrator/co-thinker in `dydo sync` output.
- **Watchdog:** gains materialize-on-demand for the chief address (message to unclaimed `chief` → top-level dispatch).
- **Prerequisite hardening:** `run-sprint` must be completed with its missing tail — sequential merge of passed slices + a **sprint-auditor** final review over the merged diff (inquisitor-lensed, judge-strict, no subagent dispatch — enforced natively by omitting the Agent tool from its allowlist). Without the merge step, "all code via workflows" would strand work in worktrees.

## Revisit When

- Trivial-edit exception gets abused into scope creep (would tighten the nudge wording or add a diff-size threshold, not RBAC).
- The chief-of-staff drifts toward a mandatory hop (would cut its mediation surface, keeping triage + status only).
