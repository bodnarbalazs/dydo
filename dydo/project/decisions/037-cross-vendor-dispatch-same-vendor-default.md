---
area: general
type: decision
status: accepted
date: 2026-07-08
participants: [balazs, Henry]
---

# 037 — Cross-Vendor Dispatch: Same-Vendor Default, Explicit Override at the Dispatch Boundary, No New Abstraction

Cross-vendor work (Claude + Codex in one flock) needs **no new dydo abstraction**. The rule is one invariant and one doctrine line:

- **Invariant:** roles are vendor-free; workflows are vendor-homogeneous by construction; **vendor binds only at the dispatch boundary**, as a property of the launched session.
- **Doctrine:** agents dispatch agents of their **own vendor by default**. Cross-vendor is an **explicit dispatch-time override** (`--codex` / `--claude`), typically human-initiated ("dispatch codex Tier-1 agents to run this sprint").

Everything else — model choice, tier resolution, compiled agent bodies — already flows from the existing config surfaces ([DR 028](./028-model-tier-abstraction.md) tiers, per-vendor mappings, `dydo sync` dual compilation). Nothing existing changes meaning.

## Context

The motivating idea (balazs, from the "advisor + workhorse" pattern circulating publicly): dynamic workflows deliver quality but are slow — recent sprints run 1h45m+, with visible multi-minute token stalls (long tool calls or API throttling; both have the same remedy). Dispatched Codex sessions promise faster grinding, true parallelism outside one session's concurrency cap and context budget, and **dual-rail capacity** — implementation spend moves to the second vendor while strong-Claude tokens concentrate on judgment-dense work (co-thinking, review gates, audits). Realistic expectation set in the round: a few-x wall-clock win, not orders of magnitude, until measured.

Both runtimes now support the same enforcement model: Codex ships a hooks engine with pre/post tool-use events that can inspect and block calls, so the dydo guard can be installed symmetrically on both stacks (a Codex guard adapter is follow-up work, payload shapes differ).

## Decision

### 1. The invariant: vendor is a session property

A Claude Code session can only spawn Claude-model subagents in its workflows; a Codex session likewise spawns only its own vendor's models. Therefore a **workflow is vendor-homogeneous by construction**, and any config that attaches a vendor to a role, skill, or workflow stage would be violated the moment that role runs inside a foreign workflow. The only place a vendor question exists is where a **new session** is created: `dydo dispatch`. Vendor binds there and nowhere else.

### 2. The doctrine: same-vendor default, explicit cross-vendor override

Default dispatch vendor = the dispatching agent's own vendor. Crossing vendors is an explicit `--codex` / `--claude` override on the dispatch — a deliberate act, normally the human's ("run this sprint on codex"). No routing table, no automatic vendor selection. If a standing preference ever emerges (e.g. capacity-based reroute per the [cross-vendor backlog](../backlog/cross-vendor-agent-integration.md)), it is expressed by whoever dispatches, not by dydo policy — revisit only if manual overrides become routine.

Consequence of the default: unless the human says otherwise, everything stays on the vendor they addressed. Ask the (Claude-hosted) chief-of-staff for an inquisition or a thorough review → it runs Claude. Tell it to dispatch codex Tier-1 agents for a sprint → that sprint's sessions are Codex, and everything *those* sessions dispatch is Codex by the same default.

### 3. Sprint vs. workflow: coordination may mix, execution never does

A **workflow** is an execution unit — vendor-pure (per §1). A **sprint** is a coordination unit — task lifecycle, records, board — and may mix: a manager can dispatch some slices as sessions of another vendor while running others through its own in-session dynamic workflow. The task lifecycle (`pending → in-progress → review-pending → approved/rejected`) is the vendor-neutral contract; records land identically either way.

Review therefore lives **inside the execution unit** (vendor-native per slice — a Codex slice gets Codex review rounds), and the cross-vendor quality catch is the **sprint-audit / inquisition gate**, which runs on whatever vendor hosts the session asked to do it. The reviewer-tier asymmetry of [DR 028 §5](./028-model-tier-abstraction.md) applies within each vendor unchanged.

### 4. The advisor pattern is already built

A stuck agent — any vendor — escalates via `dydo msg` to the chief-of-staff, who answers or elevates to the human. That *is* the advisor pattern; it requires no new machinery, and its elevation property (hard questions reach the human) is desired, not a defect. Codex-hosted manager mode files should state the escalation line explicitly; that is a docs task, not a feature.

### 5. Rejected abstractions (recorded so they are not re-invented)

- **`vendor` field on roles** — flaky: roles also run inside vendor-homogeneous workflows where the field would be a lie (§1).
- **"Seats" / org-chart vendor bindings** — a new taxonomy overlapping roles, skills, and agents; too many loosely defined things.
- **Engine registry / capability profiles** — over-machinery for a decision that is one flag at dispatch time.
- **dydo-owned execution mechanics (worktrees, sprint state machine)** — re-litigated and re-rejected; 1.x owned worktrees and it was a nightmare ([DR 024](./024-dydo-2-native-pivot.md)). Execution mechanics stay vendor-native; dydo stays the policy + context + coordination layer.

### 6. One hardening item

A vendor override targeting an unconfigured/unavailable vendor must fail fast with an actionable message — [issue 0239](../issues/0239-dispatch-vendor-override-needs-a-friendly-error-when-the-target-vendor-is-not-co.md). With tri-modal support (claude-only, codex-only, both), that mistake is routine, not exotic.

## Consequences

- No schema or behavior change to roles, tiers, skills, or workflows. The second vendor's tier mapping in `dydo.json` (anticipated by DR 028 §2) is the only config prerequisite.
- Issue 0239 (friendly override error) is the only net-new code item from this decision.
- Codex guard adapter (install the guard into Codex's hooks config at init/sync) is follow-up work — tracked on the [cross-vendor backlog](../backlog/cross-vendor-agent-integration.md), sequenced after the smoke test.
- Adoption path (each step shippable alone, gated on the v2.0.6 CLI release carrying the Codex dispatch/launch/resume fixes, issues 0227/0230/0231, with 0233 e2e coverage still open):
  1. Single Codex dispatch smoke test (the standing plan, unchanged).
  2. One real task on a dispatched Codex worker — validates the bridge under load and the guard adapter.
  3. A Codex Tier-1 manager running a sprint of Codex workers, Claude-hosted audit at the gate.
  4. Measure per DR 028 §6: rounds-per-slice and wall-clock vs. comparable Claude-workflow slices. The speed hypothesis is empirical, not assumed.

## Addendum — default work-split routing (balazs, 2026-07-08, same day)

Set hours after acceptance, when a 500k-token in-branch sprint ran ~3 hours wall-clock while
balazs actively waited: token spend is the normal part; **foreground latency is the problem**.
The standing routing policy (the "predictable class of work" the Revisit-When paragraph
anticipated — recorded here rather than waiting for it to become config):

- **Fable / Claude:** chief-of-staff, planning, and intelligence-critical work (design rounds,
  thorough reviews, inquisitions, audit gates).
- **Codex:** simple plans and implementation — "we'll give them a chance to do things fast and
  correctly" (probationary: adoption step 4's measurement still applies).
- **Dynamic workflows** (run-sprint and kin) stay first-choice for **background/parallel** work;
  anything a human is actively waiting on routes to the fast path (dispatched sessions) instead.
- Balance is an explicit goal: "with enough balance we can max out both of the subscriptions."

## Revisit When

- Manual vendor overrides become routine for a predictable class of work → consider a config-expressed dispatch default for that class (the deferred routing idea).
- Measurement (adoption step 4) shows cross-vendor sprints thrashing in review rounds → the speed win is illusory; rebalance what gets routed out.
- Either runtime gains cross-vendor subagents inside workflows → §1's invariant weakens and this decision should be reopened.
