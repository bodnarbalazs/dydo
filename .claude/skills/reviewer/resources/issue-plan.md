# Reviewing an Issue Plan

Target: one Issue-resolution plan before production, invoked only when its Issue Captain judges the
route risky enough to justify a separate gate. Judge it against the Issue contract, settled working
tree, governing Decisions and Project-plan section, and the Issue Planner skill.

## Boundary

This review buys down material route risk. A concrete contract conflict, false pattern, hidden design
choice, consequential missing case, or gate that cannot prove the outcome is a finding. Wording,
formatting preference, extra pseudocode, and an equally valid route are outside this rubric: do not
turn them into findings or notes.

## Method

1. **Pin the ground.** Match the plan to the target Issue or direct lane Sub-issue, its five contract
   fields, base SHA, branch, worktree, clean state, and owned paths. **Done:** it plans the exact tree
   and nothing outside its authority.
2. **Verify the route.** Read every cited Decision, specification, pattern, seam, file, and test at the
   base SHA. **Done:** the approach follows working precedent, or justifies why none can serve.
3. **Hunt hidden decisions.** Walk the steps as the delegated writer through behavior, files, seams,
   edge and failure handling, migration, compatibility, and proof. **Done:** no material choice is
   silently delegated to production.
4. **Check the proof.** Gates are exact and sufficient for the Issue outcome. Run those applicable
   before code; mark implementation gates `not run — pre-code` after verifying their commands and pass
   conditions. **Done:** the eventual result can fail as well as pass.
5. **Check the fog.** A missing answer is either found in the searched ground or returned through the
   Captain as a prepared, blocking question Issue. **Done:** no assumption bridges an unknown route.
6. **Return the review block.** PASS only when a writer can implement mechanically without being
   misled. **Done:** every finding states one material route correction.

## Checklist

- [ ] Exact Linear record, base SHA, branch, worktree, clean state, and owned paths match
- [ ] Approach cites a verified working pattern or proves why a new one is necessary
- [ ] Files and ordered steps account for every contract outcome without hidden design choices
- [ ] Edge cases, failures, migration, compatibility, and rollback are covered when consequential
- [ ] Exact gates can prove the outcome; only stage-applicable gates were run
- [ ] Any unanswered question records homework and blocks the work through the Captain
