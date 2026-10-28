---
area: general
type: decision
status: accepted
date: 2026-10-28
---

# 058 - Review Severities: Blocker, Should, Nit

Every review finding carries one of three severities, `blocker`, `should` or `nit`, and only a
blocker makes the verdict FAIL. Should and nit findings are deferred, never dropped. Ruled by Balazs
on 2026-10-27 in [DYD-398](https://linear.app/bodnar-balazs/issue/DYD-398).

## Context

Until now a reviewer had no severity: "a finding is a FAIL - YOU SHALL NOT PASS", the captain
treated every FAIL as binding, and round 1 merged two reviewers' findings. That ratcheted scope:
every nit became a fix hop, and the only brake was the fifth-FAIL stop.

The rule was a reaction to early dydo, where reviewers self-assigned severity, returned "pass with
notes", and the notes were ignored. Balazs's correction (2026-10-27): the old rule was right that
findings must be answered, wrong that every finding must be answered *now*. With agents it is often
better to make progress, let the whole exist, then fix; some problems solve themselves. **The
innovation is deferring fixes, not eliminating them.**

Source: t3dotgg/tstack `sol-loop` and `babysit-pr` (MIT), read 2026-10-27,
<https://github.com/t3dotgg/tstack/blob/main/skills/sol-loop/SKILL.md>. Adopted as an idea; no text
is copied.

## Decision

1. **Three severities, on every rubric** (code, docs, spec, project-plan, merge):
   - **blocker** - the intended case is wrong. Only a blocker makes the verdict FAIL and returns the
     ticket to `Implementing`. A merge-review FAIL is a blocker, routed as before.
   - **should** - a real bug callers will hit, inside the intent. It does not block the ticket. The
     captain, or the map holder for a project-plan review it runs, files it directly as its own
     ticket: Type matching the finding (Bug by default), Mode AFK, `Backlog`, in the source's
     Project, linked to the source ticket and the review block, natively wired as a blocker of the
     Project's landing Merge Ticket when that exists; otherwise the map holder wires it when it
     creates the landing ticket. Outside a Project it is a team ticket in `Backlog` the user sees.
     This is the captain's one exception to taking an adjacent outcome only on the user's word.
   - **nit** - everything else. Another code path, a rare case, a small perf cost, extra compat or
     style is a nit whatever label the reviewer gave it. It is one line on the ticket's record, with
     no Linear ticket; the final walkthrough lists unaddressed nits and the user promotes any.
2. **Verdict.** FAIL iff at least one blocker. A PASS may list its should and nit findings in the
   form's `Deferred` tail; a FAIL finding carries `severity:`. Round 1 still seats two reviewers;
   their findings merge into one deduplicated block, which FAILs iff either found a blocker. The
   fifth consecutive FAIL still stops the loop.
3. **Severity check.** The fix-hop author checks each finding's severity against the source, not
   the reviewer's label, fixes only the blockers, and may disagree with a label with evidence
   (file:line, test, output) in its `IMPLEMENTED` behaviour. The captain records every changed label
   with its reason. A should-ticket's own author may close it with a reason.
4. **Fix hops narrow.** A fix hop prefers the change that deletes or narrows code; review must not
   grow the PR.
5. **Landing.** A Project lands only when every should-ticket is `Done` or closed with a reason;
   the map holder sequences them.

"No PASS with notes" survives in a new sense: there is no unlabelled note. Every finding carries a
severity and is answered - fixed, ticketed, recorded, or relabelled with evidence. Nothing is
silently dropped.

## Consequences

- A nit no longer costs a fix hop, so review rounds stop ratcheting scope.
- Should-tickets add Backlog tickets to a Project and gate its landing; the map holder sequences
  them like any other work.
- Unaddressed nits reach the user at the final walkthrough rather than vanishing.
- A reviewer's label is a claim, not a ruling: the author can contest it with evidence, and the
  record shows every change.

## Supersedes

Partially supersedes [DR 045](./045-flow-map-hats-review-tiers-and-working-tree-contract.md)'s
"a note is a finding, and a finding is a FAIL" and
[DR 031](./031-sprint-auditor-charter-rewrite.md)'s "no pass with notes", for that question only.
Both stay `accepted` otherwise.

## Related

- [Work Model](../../understand/work-model.md) - The review block and its severities
- [Glossary](../../reference/dydo-glossary.md) - Severity and Ticket review
- [Linear Workspace Standard](../../reference/linear-workspace-standard.md#communication-and-evidence) - The PASS and FAIL forms
