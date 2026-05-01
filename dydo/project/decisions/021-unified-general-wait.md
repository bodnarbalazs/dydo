---
area: project
type: decision
status: accepted
date: 2026-04-29
---

# 021 — Unified General Wait for All Roles

Every claimed agent registers a single always-active general wait once their role is set, replacing the orchestrator-only general-wait policy. Task-channeled `dydo wait --task <name>` is removed from the standard workflow. The `dispatch --wait` privilege stays as an oversight-role capability but its meaning shifts: it now enforces a *release-block* on the dispatched agent until they have messaged the dispatcher back.

This **partially supersedes Decision 005** for the question of *what `--wait` means*. The "fresh agent for review feedback loops" stance from 005 is unaffected.

---

## Context

Three observations from lived practice drove this:

1. **Reachability gaps in non-orchestrators.** Reviewers hold for decisions, co-thinkers don't generally release on their own, and other roles routinely sit in states where the user needs to redirect them. Today reachability is bounded: messages land in `unread-messages` but only surface on the agent's *next* tool call, and no real-time push exists for non-orchestrators. The user has had to type "check your inbox" too many times.

2. **Stale task-channel waits in orchestrators.** Operationally, orchestrators frequently end up with open `--task` waits on channels nothing will ever message — a mismatch between dispatched work and the channel set kept open. The per-task channel was meant to be explicit parallel-tracking, but in practice it tracks the orchestrator's intent, not actual deliverables, and drifts.

3. **The orchestrator/non-orchestrator asymmetry has no real upside.** The current model has an orchestrator-only "must keep general wait active" gate (`GuardCommand.cs:1331` `OrchestratorMissingGeneralWait`) plus an orchestrator-only `--wait` privilege (`DispatchService.cs:490` `CheckWaitPrivilege`). Two separate enforcement layers for one role shape, with non-orchestrators getting neither — meaning the user can't reach them in real time even when there's a clear reason to.

Cost of running a general wait is low: a single background polling process per agent (`Thread.Sleep(10_000)` loop in `WaitCommand.cs:117`). Making it part of the basic post-claim setup is cheap; making it universal removes the asymmetry.

## Decision

### Every agent registers a general wait once their role is set

After `dydo agent role <role> --task <task>` lands, each agent starts `dydo wait` (no `--task`) in the background. This is part of the basic workflow for *all* roles, captured in every mode template.

Why post-role and not post-claim: the guard's stage model (claim → role → work) only allows the marker-creation write once a role is set. Registering the wait pre-role would be blocked by the guard. The "right after the agent has identity" intent of this decision is preserved; the placement in the workflow is one step later than the original draft of this doc suggested.

The general wait blocks until a new unread message arrives that is not claimed by an active task wait, then exits. The agent rearms it after handling the message — same pattern orchestrators use today.

### Task-channeled waits removed from the standard workflow

The `--task` form remains in the CLI for special cases, but the standard orchestrator pattern of "register a `dydo wait --task <sub-task>` per dispatched agent" goes away. Orchestrators rely on:

- The general wait for real-time message arrival.
- `dydo agent list` / `dydo agent tree` and inbox state for tracking what's outstanding.

Loss of explicit per-channel tracking is acceptable — in practice it drifted from reality, and inbox + agent list are the source of truth anyway.

### `--wait` privilege stays, with shifted semantics

`dispatch --wait` remains an oversight-role privilege (`CanOrchestrate=true`). What it means changes:

- **Before**: dispatcher's own session blocks until the dispatched agent's reply arrives via task wait.
- **After**: the *dispatched* agent cannot release until they have messaged the dispatcher back on the dispatched task's subject. The dispatcher does not block — their general wait surfaces the reply when it lands.

This is a cleaner separation of concerns: `--wait` becomes a *release constraint* on the callee, not a blocking call on the caller. The caller can continue working in parallel; the callee owes a reply.

### Issue #0133 is a separate prerequisite

The general-wait blocking bug (#0133) must be fixed before this ships, otherwise universalising the general wait deadlocks every agent the same way it currently deadlocks orchestrators. Fix and regression tests are required:

- A test that verifies `dydo wait` (general) actually blocks when the inbox has only known/old unreads.
- A test that verifies the wait marker reaches `Listening=true` before the next guard check (race scenario from `OrchestratorMissingGeneralWait`).
- A test that verifies the marker is cleaned on `dydo agent release` so the wait teardown can't outlive the agent.

These tests are part of the slice that fixes #0133, not part of the simplification slice.

## Consequences

### Code changes

- **Workflow / templates.** Every `Templates/mode-*.template.md` adds a "Register general wait" step right after the role-set step. The orchestrator template's "Dispatch" section drops per-task `dydo wait --task` registration and rewrites the dispatch pattern to rely on the general wait + agent list. Workflow files in `dydo/agents/*/` regenerate from templates.
- **Guard.** `OrchestratorMissingGeneralWait` (`GuardCommand.cs:1331`) generalizes to `MissingGeneralWait` — applies to all roles once a general wait is expected post-claim. The orchestrator-specific carve-out goes away.
- **Wait command.** `dydo wait` (no `--task`) keeps current semantics but the bug behind #0133 must be resolved as a hard prerequisite — see above for required tests.
- **Dispatch.** `CheckWaitPrivilege` (`DispatchService.cs:490`) stays — `--wait` remains oversight-only. The dispatch-time auto-registration of `dydo wait --task` (today's orchestrator pattern) is removed. Release-time check on the dispatched agent gains a new constraint: if the dispatch was `--wait`, the agent cannot release until a message has been sent on the dispatched task's subject to the dispatcher.
- **Tests.** Every test that exercises agent lifecycle or dispatch-with-wait needs updating. Mechanical but broad.

### Process changes

- The orchestrator workflow becomes: dispatch → continue working → general wait surfaces replies → handle. No more "register N task waits and rearm a general wait alongside."
- The dispatched-agent workflow under `--wait` gains: "you cannot release until you've messaged the dispatcher." Today the obligation is implicit in the brief; this makes it a guard-enforced release constraint.

### Migration

- Breaking change to templates and a behavioural change to `--wait`. Minor version bump (v1.4.0).
- Ship #0133 fix first or in the same release; if same release, fix is the first slice to land.
- Existing workflow files in `dydo/agents/*/` regenerate from templates on next `dydo fix` / template update.

### Re-evaluate

- After ~2 weeks of lived practice, check whether the loss of per-channel tracking causes orchestrators to lose anything important. If it does, the per-task wait can be re-introduced as opt-in tooling without rebreaking the unification.

---

## Affects

- `Templates/mode-orchestrator.template.md` — Dispatch section rewrite (drop per-task waits), general-wait step after the role-set step.
- `Templates/mode-code-writer.template.md` — General-wait step after the role-set step.
- `Templates/mode-reviewer.template.md` — Same.
- `Templates/mode-planner.template.md` — Same.
- All other `Templates/mode-*.template.md` — Same.
- `Commands/GuardCommand.cs` — `OrchestratorMissingGeneralWait` → `MissingGeneralWait`.
- `Commands/WaitCommand.cs` — #0133 fix landing here as a prerequisite slice.
- `Services/DispatchService.cs` — Drop dispatch-time task-wait registration; add `--wait` release constraint on the dispatched agent.
- [Decision 005 — Fresh Agent Over Wait-for-Feedback](./005-fresh-agent-over-wait-for-feedback.md) — Partially superseded for the *what `--wait` means* question; "fresh agent for review feedback" stance unchanged.
- [Issue #0133](../issues/resolved/0133-orchestrator-general-wait-deadlock-recurs-bcff3f4-incomplete.md) — Hard prerequisite. Must ship first or in the same release with regression tests.
