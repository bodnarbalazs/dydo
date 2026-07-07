---
id: 222
area: general
type: issue
severity: medium
status: resolved
found-by: manual
date: 2026-07-07
resolved-date: 2026-07-07
---

# chief-of-staff + co-thinker mode templates omit the dydo dispatch --auto-close example (orchestrator/code-writer/etc. have it); no guard nudge soft-blocks dispatch without --auto-close, so released tabs leak

A chief-of-staff/co-thinker onboards without ever being shown `dydo dispatch --auto-close`, and nothing enforces the flag — so dispatched work agents leave lingering terminal tabs after they release. Surfaced live 2026-07-07: Adele (CoS) dispatched Brian/Emma/Frank without `--auto-close`; tabs lingered, and even after acknowledging it, the next dispatch still omitted it. Fixing the operator's memory is a crutch; the system should teach and enforce it.

## Description

Two systemic gaps:

1. **Docs gap.** `Templates/mode-chief-of-staff.template.md` and `Templates/mode-co-thinker.template.md` contain **no `dydo dispatch` example** — they only describe routing prose ("a top-level dispatch of an orchestrator or co-thinker"). Meanwhile `mode-orchestrator/code-writer/docs-writer/planner` templates all show `dydo dispatch --auto-close …`. So the two Tier-1 roles that route work most are the ones whose onboarding never demonstrates the flag. (The shared `agent-workflow.template.md` quick-ref does list it, but the role playbook doesn't reinforce it.)
2. **Enforcement gap.** No guard nudge soft-blocks `dydo dispatch` when `--auto-close` is omitted. A 1.x-era soft-block for this existed but was removed with the worker-tier-dispatch teardown ([DR 024](../decisions/024-dydo-2-native-pivot.md)). `dydo.json` (nudges) and the dispatch guard path are off-limits to CoS → code-writer work.

Not a regression in the auto-close feature itself — a `--auto-close` test dispatch closes correctly. The failure mode is purely "flag not taught, not enforced."

## Reproduction

`grep -L "dydo dispatch" Templates/mode-chief-of-staff.template.md Templates/mode-co-thinker.template.md` (both lack it); compare `Templates/mode-orchestrator.template.md` which has it.

## Resolution

Two disjoint slices:
- **A (docs) — DONE (commit `1ecdf05`, Sonnet-reviewed PASS, 2026-07-07).** Added a fenced `dydo dispatch --auto-close …` example + one-line why to `mode-chief-of-staff.template.md` (Triage the Funnel) and `mode-co-thinker.template.md` (Task Emerged → Plan It), modeled on the orchestrator template. **Regeneration still pending:** `dydo sync` was intentionally NOT run (would spray a sweep-prone regen diff across the unstable tree) — the template *source* is fixed, but the generated mode files/skills only carry it after balazs runs `dydo sync` once the tree settles.
- **B (guard nudge, needs a compiling tree):** restore a soft-block nudge — `dydo dispatch` without `--auto-close` warns + re-prompts (exit 2, re-run to proceed for the deliberate keep-terminal-open case), mirroring the existing worktree-merge-force nudge in `dydo.json`. Consider instead making `--auto-close` the default with an explicit opt-out; decide in the slice. Add a regression test. **Blocked until balazs's InitCommand.cs AOT WIP compiles** (gap_check/tests can't verify a guard change on a broken tree).
