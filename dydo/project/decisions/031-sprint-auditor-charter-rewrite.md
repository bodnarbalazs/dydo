---
area: general
type: decision
status: accepted
date: 2026-07-03
participants: [balazs, Adele]
---

# 031 — Sprint-Auditor Charter Rewrite: One Identity, One Invariant, a Budget Instead of a Prohibition

The sprint-auditor skill is rewritten from scratch. Its identity is a single question — **"do the
slices compose?"** — not the current "inquisitor + judge" dual-character framing. The blanket
subagent prohibition ("you have no Agent tool, by design") is replaced by an **invariant plus a
budget**: the whole merged diff is read in one context, always, by the auditor itself; a small
subagent budget (guideline: at most 3) may be spent to **verify findings already formed**, never to
delegate the reading. Depth escalates selectively: when seam analysis flags a slice as suspicious,
the auditor may spend budget re-reviewing that one slice — including dispatching a dedicated
reviewer at it — but never re-reviews every slice by default. The strict verdict is retained:
findings imply FAIL; there is no "pass with notes".

## Context

The sprint-auditor was added as the final review over an entire merged sprint, after every slice
passed its own review. The current SKILL.md is a soup of borrowed identities: it tells the agent to
be "two characters at once" (Inquisitor and Judge) while its own premise is that the role is not an
inquisitor, and its lens list is essentially the inquisition's lens list copied down a tier —
burying the one lens that uniquely belongs to this role (cross-slice seams) as one bullet among
four. The subagent prohibition was originally justified as "not an inquisitor, so no subagents",
which confuses a budget decision with an identity decision: the inquisition's power is not merely
that it spawns subagents.

The role sits in a three-tier ladder defined by scope and verification depth, not tool prohibitions:

- **code-reviewer** — "is this slice right?" One diff, inline verification, cheap.
- **sprint-auditor** — "do these slices compose?" Whole merged diff in one context, small
  verification budget. Catches issues early so fewer reach the inquisition.
- **inquisition** — "is the campaign sound?" Multi-lens fan-out, adversarial verification, expensive.

## Decision

1. **The invariant.** The entire merged sprint diff is read end to end in the auditor's own
   context. Seams are only visible when one head holds all the slices at once. Fanning out
   per-slice sub-reviewers and aggregating their verdicts reproduces exactly the blind spot the
   role exists to close, at higher cost — it is the signature failure mode and is named as such in
   the skill.
2. **Hunt priorities.** In order: (a) seams — slices touching the same file or behavior, one slice
   breaking an assumption another relies on, duplicated or contradictory logic, interface drift
   between brief and merge; (b) merge artifacts — lost hunks, doubled code, conflict leftovers;
   (c) whole-sprint gaps — behavior that emerges only from the combination of slices, untested as a
   combination; (d) escaped per-slice defects — reported when seen, but bycatch, not quarry.
3. **Verification budget.** Subagents (guideline: at most 3) verify findings the auditor has
   already formed: adversarially check a suspected seam, run an expensive check in parallel, chase
   a suspicion through unfamiliar code. Subagents verify the reading; they never do the reading.
4. **Selective depth escalation.** If the whole-diff read flags a specific slice as suspicious, the
   auditor spends budget there — up to a dedicated re-review of that one slice. On small sprints
   the careful whole-diff read is already a slice re-review; the distinction only bites on large
   sprints, exactly where uniform re-review would blow both budgets.
5. **Cost position.** More expensive than a code-reviewer, well below an inquisition — in tokens
   AND wall-clock, since the audit delays merge-back and subsequent implementation. Parallel
   verification exists partly to protect wall-clock.
6. **Verdict.** Strict and binary. Findings imply FAIL, each specific enough that a code-writer can
   act without asking questions. PASS means: seam-clean, artifact-free, covered as a unit, tests
   green on the merged state.

## Consequences

- `.claude/skills/sprint-auditor/SKILL.md` is rewritten from scratch per this charter (a draft was
  produced and approved in direction during the co-thinking session).
- The sprint-auditor agent definition regains the Agent tool, bounded by the budget above.
- run-sprint documentation referencing the "works alone by design" rule is updated to match.
