# Reviewing a Merge

Target: the tree as it exists after a merge. Every merged ticket passed review alone; the seams
between them passed nothing, and neither did the merge itself. Read the integrated state at the
scale of what landed: a two-file merge is minutes of mechanical checking, a whole feature is not.
Scope is what this merge created or exposed.

Every operation has its own Merge Ticket: a lane into its parent, a ticket into the feature, or
the landing. Read that record's pinned source, target and combined gates.
No review at another level substitutes for this one; final landing also proves Project acceptance.

## Method

1. **Fix the unit.** Name the merge commit, both parents and the tickets that landed in it; diff the
   merged tree against the base the branch grew from, since each ticket's own review already covered
   its branch diff. Done when every landed ticket is named.
2. **Sweep for merge artifacts.** Conflict markers, hunks that arrived twice, hunks that disappeared
   in resolution, a neighbour's change reverted by the resolution, build products or local files
   committed by accident. Grep for the markers yourself; a clean build hides all of this. Done when
   each has been searched for.
3. **Walk the seams.** Every file, symbol and behaviour two merged tickets both touch: a caller left
   on the old contract, a name that moved under someone else, two implementations of one rule, an
   assumption one ticket holds and another broke. These are the defects no ticket review could see,
   and why this pass exists. Done when every shared file, symbol and behaviour is checked.
4. **Put the gates on the integrated state.** Each landed ticket's exact gate commands have run once
   on this exact merge commit: where a gate record names that commit you verify it, and where none
   does you run them yourself in the merged tree. A green from any other tree - an isolated
   worktree, either parent - proves nothing here. Done when every gate has its command and result
   recorded.
5. **Prove acceptance** (final merge). Run every feature file the landed tickets wrote, then read the
   governing plan and feature specs at pinned commits and prove each acceptance criterion against the merged
   tree, citing the scenario, command output or file:line that proves it. A criterion you cannot
   prove is a finding. Done when every criterion is proved or a finding.

The verdict goes on the Merge Ticket in the review block, naming the merge commit and every gate
result. Distinguish integration defects from defects in the source work so the Captain can route a
fix here, or a revert and source rework; a later dependent merge requires a following fix ticket.
