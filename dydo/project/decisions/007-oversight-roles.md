---
type: decision
status: accepted
date: 2026-03-09
area: project
---

# 007 — Oversight Roles: Orchestrator, Inquisitor, Judge

Three new roles that enable swarm coordination, adversarial QA, and dispute resolution.

## Problem

The existing role set (co-thinker, planner, code-writer, reviewer, docs-writer, test-writer) covers the standard development workflow. But two gaps emerge at scale:

1. **No coordination layer.** When multiple agents work in parallel, nobody tracks the big picture. The human becomes the bottleneck — figuring out who's doing what, resolving conflicts, rebalancing work.
2. **No deep QA.** The reviewer catches surface issues in fresh code. Nobody goes back to scrutinize code that already passed review — the subtle bugs, untested paths, and silent assumptions.
3. **No dispute resolution.** When an agent produces a claim (especially adversarial findings), there's no mechanism to evaluate whether it's real or manufactured.

## Decision

### Orchestrator

Purpose: swarm coordination and the user's command center during parallel work.

- **Graduation-only** — only co-thinkers and planners can switch to orchestrator. Cannot be dispatched directly. The guard enforces this via `CanTakeRole`.
- **Long-lived session** — stays active until the user dismisses it. Not a dispatch-and-release role.
- **Privileges**: `dispatch --wait`. Can dispatch any role and wait for results.
- **Permissions**: workspace, tasks, decisions. No source code writes.
- **Responsibilities**: slice work into parallel-safe units, dispatch agents, monitor progress, resolve conflicts between agents, answer user questions about ongoing work, rebalance when things go wrong.

### Inquisitor

Purpose: adversarial hypothesis-driven QA. Finds what reviewers can't.

- **Dispatchable** — can be dispatched by orchestrators, humans, or other roles.
- **Autonomous** — designed for human-scarce operation. Asks the human only when genuinely stuck.
- **Privileges**: `dispatch --wait` (to test-writers and judges).
- **Permissions**: read-only source, write workspace and inquisition reports.
- **Workflow**: receive scope → read code → form hypotheses → dispatch test-writer to prove/disprove → dispatch judge to validate confirmed findings → produce inquisition report.
- **Quality over quantity.** The code already passed review. The inquisitor looks for what the reviewer couldn't see.

### Judge

Purpose: evaluate claims, resolve disputes, validate findings.

- **Dispatchable** — by inquisitors (to validate findings), co-thinkers (to evaluate ideas), or orchestrators (to break deadlocks).
- **Privileges**: `dispatch --wait` (including to other judges).
- **Self-dispatch for split decisions**: when uncertain, a judge can dispatch a second judge. If they disagree, a third casts the deciding vote. Maximum three judges per claim (guard-enforced).
- **Permissions**: read-only source, write workspace.
- **Mindset**: skeptical arbiter. Examines evidence for and against. Can dispatch agents for more evidence if needed.

## Open Questions

- **Agent pushback.** Currently, if a reviewer rejects code, the code-writer can't appeal (fresh agent model — see decision 005). There may be genuine value in allowing pushback. The judge could mediate, but the workflow for this isn't designed yet.
- **Judge scope beyond inquisition.** The judge is general-purpose by design — any agent or the human can invoke one to evaluate a claim. The full set of use cases will emerge from practice.

## Implications

- Total roles: 9 (interviewer dropped per decision 006, tester renamed to test-writer).
- `dispatch --wait` privilege: orchestrator, inquisitor, judge. Guard-enforced for all others.
- Orchestrator graduation: guard checks `TaskRoleHistory` for co-thinker or planner on the same task.
- Judge panel limit: guard tracks active judges per task/finding, caps at 3.
- New command needed: `dydo inquisition coverage` for tracking QA coverage and staleness.
- New docs needed: `reference/roles/` with per-role reference pages.
