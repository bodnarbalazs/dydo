---
area: project
type: context
name: review-tiers-and-attention
status: open
---

# Review Tiers & Attention — Implementation Backlog

Follow-ups from the 2026-07-03 co-thinking session; design settled in
[Decision 031](../decisions/031-sprint-auditor-charter-rewrite.md) and
[Decision 032](../decisions/032-attention-ledger-and-housekeeping-nudge.md).

## Sprint-auditor rewrite (Decision 031)

- **Rewrite `.claude/skills/sprint-auditor/SKILL.md` from scratch** per the 031 charter: one
  identity ("do the slices compose?"), one-head whole-diff read invariant, hunt priorities
  (seams > merge artifacts > whole-sprint gaps > bycatch), subagent verification budget
  (guideline: at most 3, verify-never-read), selective depth escalation, strict binary verdict.
  An approved draft exists in the session transcript; treat as direction, not gospel.
- **Regrant the Agent tool** to the sprint-auditor agent definition, bounded by the budget.
- **Update run-sprint docs** that reference the "works alone by design" rule.

## Attention ledger + nudge (Decision 032) — build order, each step independently useful

1. **Registry seed** — generate the area registry from the directory tree + doc `area:`
   frontmatter (folders are the taxonomy; registry records only deviations); human-review once.
2. **Visit-artifact convention** — patrol/inquisition/sprint-audit skills end with a report
   artifact carrying covered areas + date in frontmatter; `dydo check` rejects unknown areas.
3. **Computed ledger view** — last-visit-per-area from artifacts + churn-per-area from git via
   the registry mappings; unmapped bucket first-class. (CLI or plain doc first — plain doc was
   the leaning; promote to CLI once the shape proves out.)
4. **CoS nudge item** — deterministic staleness x spare-capacity check embedded in the
   chief-of-staff methodology; suggests patrols/directed inquisitions, never dispatches.

Housekeeper role: explicitly deferred (032 §7) — revisit only if the nudge loop keeps firing
and paying off.
