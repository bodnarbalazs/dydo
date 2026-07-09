---
title: c1-8 Codex Dispatch Live Smoke Re-Run
blocked-by: c1-7-codex-e2e-coverage
due:
needs-human: true
priority: High
sprint: c1-codex-adoption
status: ready
work-type: chore
area: backend
type: context
---

# c1-8 Codex Dispatch Live Smoke Re-Run

Human-gated ground truth before v2.0.7 ships: re-run the 2026-07-09 dispatch smoke against the
landed C1 and walk the full lifecycle that wedged last time. balazs at the terminal; a Tier-1
manager drives and records the exit report (pattern: Iris's smoke exit report, summarized in
issue 0254).

## Checklist (each item maps to a v2.0.7 acceptance criterion)

1. **Preflight:** `dydo dispatch --codex` with a deliberately broken prerequisite (e.g. hook
   trust disabled) fails fast with the actionable message; restore, proceed. [good path usage]
2. **Launch posture:** the codex session starts with the configured sandbox+approval posture; a
   read AND a workspace write run without a human approval click; a boundary-exceeding action
   still prompts. [auto-approved permissions — classifier posture, not yolo]
3. **Windows sandbox:** confirm the c1-3-documented setup on this box; record what
   `workspace-write` actually required. [posture ground truth]
4. **Onboarding + work:** claim (manual step, per the updated workflow template) → role →
   `dydo wait --register` → receive a message → `dydo read` it and the must-reads →
   `dydo whoami` shows host/model. [working flow; calls dydo commands correctly]
5. **Release:** `dydo inbox clear --all` succeeds; `dydo agent release` succeeds and removes the
   durable wait marker — no human force-clean. [can release]
6. **Resume (still untested since the adopt-orphaned-codex-slices fix):** kill the live codex
   session, verify the watchdog resume reattaches via the stored session id. If the payload's
   session id turns out not to be codex-resumable, that goes to the human gate list per issue
   0233's 2026-07-08 update — record, don't improvise.
7. **Provenance:** a message and an issue filed from the codex session render exact display
   models on their surfaces. [c1-6 live check]

8. **Pre-smoke human step:** refresh the project-local
   `dydo/_system/templates/agent-workflow.template.md` (and regenerate agent workflow files if
   applicable) from the c1-1-updated embedded `Templates/agent-workflow.template.md` — the
   `_system` path is guard-off-limits to all agents, so only balazs (or `dydo template update`)
   can apply it. Without this, the dispatched codex agent's onboarding text lacks the manual-
   claim + `dydo read` guidance.

## Deliverables

- Exit report in the driving agent's workspace; findings → issues (severity per finding).
- All-green checklist = the mechanical "can release v2.0.7" signal (balazs's criteria table in
  the sprint record).
- Issues 0253/0254 Resolution sections get the live-confirmation note (slices already moved them
  on land; this adds ground truth).

## Sequencing

Last; after c1-7. Nothing in the repo is mutated by this row (smoke only) — recovery from any
failure is re-run after fix.

## Success criteria

Every checklist item green in one uninterrupted session, or each red item filed as an issue with
the failure verbatim. balazs signs the v2.0.7 gate on this report.
