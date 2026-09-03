# Reviewing a Merge

Target: the tree as it exists after a merge. Every merged Issue passed review alone; the seams between
them passed nothing, and neither did the merge itself. Read the integrated state — not the reports
about it — at the scale of what landed: a two-file merge is minutes of mechanical checking, a whole
feature is not.

Scope is what this merge created or exposed. A defect it neither touched nor uncovered goes back to
the invoker as an observation, outside the findings, and does not bind this verdict.

## Scale

- **After every Issue merge** — steps 1–4, sized to the merged diff.
- **At the final feature merge** — steps 1–5. Plan acceptance is proved here or nowhere.
- **At full scale** — as the judge inside the inquisition: steps 1–6 over the entire feature diff.

## Method

1. **Fix the unit.** Name the merge commit and both parents, and list the Issues that landed in it.
   Diff the merged tree against the base the branch grew from, rather than re-reading the branch diff
   each Issue's own review already covered.
2. **Sweep for merge artifacts.** Conflict markers left in files, hunks that arrived twice, hunks that
   disappeared in resolution, a neighbour's change reverted by the resolution, and build products or
   local files committed by accident. Grep for the markers yourself; a clean build hides all of this.
3. **Walk the seams.** Take every file, symbol, and behaviour that two merged Issues both touch: a
   caller left on the old contract, a name that moved under someone else, two implementations of one
   rule, an assumption one Issue holds and another broke. These are the defects no Issue review could
   have seen, and they are why this pass exists.
4. **Rerun the gates on the integrated state.** Run each landed Issue's exact gate commands yourself,
   in the merged tree. Green in an isolated worktree proves nothing here. Record command and result.
5. **Prove acceptance** (final merge and full scale). Read the reviewed plan at its governing commit
   and prove each acceptance criterion against the merged tree, one at a time, citing the command
   output or the file:line that proves it. A criterion you cannot prove is a finding.
6. **Judge the findings** (full scale). Resolve every reported finding to `confirmed`, `plausible` or
   `refuted` on evidence you verify yourself. Default to `refuted`; reach for `plausible` only when
   the deciding state is genuinely unavailable, and name the missing fact.

The verdict goes in the review block, naming the merge commit and every gate rerun.

## Checklist

- [ ] Merge commit, both parents, and every landed Issue named
- [ ] Merged tree diffed against the branch base
- [ ] Conflict markers, doubled hunks, lost hunks, and reverted neighbours searched for
- [ ] Build products and local files kept out of the commit
- [ ] Every file, symbol, or behaviour touched by two Issues checked for a broken assumption
- [ ] Every landed Issue's gates rerun in the merged tree, with recorded output
- [ ] Every acceptance criterion of the reviewed plan proved with evidence (final merge, full scale)
- [ ] Every reported finding resolved to confirmed, plausible or refuted (full scale)
