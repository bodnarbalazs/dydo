# Reviewing a Merge

Target: the tree as it exists after a merge. Every merged Issue passed review alone; the seams
between them passed nothing, and neither did the merge itself. Read the integrated state at the
scale of what landed: a two-file merge is minutes of mechanical checking, a whole feature is not.
Scope is what this merge created or exposed.

## Scale

- **After every Issue merge** — steps 1–4, sized to the merged diff.
- **At the final feature merge** — steps 1–5. Plan acceptance is proved here or nowhere.

## Method

1. **Fix the unit.** Name the merge commit, both parents and the Issues that landed in it; diff the
   merged tree against the base the branch grew from, since each Issue's own review already covered
   its branch diff. Done when every landed Issue is named.
2. **Sweep for merge artifacts.** Conflict markers, hunks that arrived twice, hunks that disappeared
   in resolution, a neighbour's change reverted by the resolution, build products or local files
   committed by accident. Grep for the markers yourself; a clean build hides all of this. Done when
   each has been searched for.
3. **Walk the seams.** Every file, symbol and behaviour two merged Issues both touch: a caller left
   on the old contract, a name that moved under someone else, two implementations of one rule, an
   assumption one Issue holds and another broke. These are the defects no Issue review could see,
   and why this pass exists. Done when every shared file, symbol and behaviour is checked.
4. **Rerun the gates on the integrated state.** Each landed Issue's exact gate commands, run by you
   in the merged tree; green in an isolated worktree proves nothing here. Done when every gate has
   its command and result recorded.
5. **Prove acceptance** (final merge). Run every feature file the landed Issues wrote, then read the
   reviewed plan at its governing commit and prove each acceptance criterion against the merged
   tree, citing the scenario, command output or file:line that proves it. A criterion you cannot
   prove is a finding. Done when every criterion is proved or a finding.

The verdict goes in the review block, naming the merge commit and every gate rerun.
