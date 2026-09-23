# Reviewing a Spec

Target: the contract text on the Issue as it stands before any code, invoked when the Issue Captain
has bought a spec review. Judge it against the parent's criteria, the settled working tree, the
governing Decisions and Project-plan section, and the `code-writer` skill.

## Boundary

This review buys down material route risk. A scenario beyond its parent criterion, a concrete
contract conflict, a false pattern, a hidden design choice, a consequential missing case, or a gate
that cannot prove the outcome is a finding. Wording, formatting preference, and an equally valid
route are outside this rubric: do not turn them into findings or notes.

## Method

1. **Pin the ground.** Match the contract to the target Issue or direct lane Sub-issue, its five
   contract fields, base SHA, branch, worktree, clean state and owned paths. **Done:** it binds the
   exact tree and nothing outside its authority.
2. **Judge the scenarios.** Where the contract calls for Gherkin, each scenario stands at the
   product's boundary, in glossary words, deterministic, refining one criterion its parent carries;
   every example column changes an outcome. **Done:** the scenarios say what the Issue proves and
   nothing the parent did not ask for.
3. **Verify the ground it cites.** Read every cited Decision, specification, pattern, seam, file, and
   test at the base SHA. **Done:** each citation holds, or its departure is justified.
4. **Hunt hidden decisions.** Walk the contract as the delegated writer through behavior, files,
   seams, edge and failure handling, migration, compatibility, and proof. **Done:** no material
   choice is silently delegated to production.
5. **Check the proof.** Acceptance lines, scenarios and gates are exact and sufficient for the Issue
   outcome. Run those applicable before code; mark implementation gates `not run — pre-code` after
   verifying their commands and pass conditions. **Done:** the eventual result can fail as well as
   pass.
6. **Check the fog.** A missing answer is either found in the searched ground or returned through the
   Captain as a prepared Question packet naming its waiters. **Done:** no assumption bridges an unknown route.
7. **Return the review block.** PASS only when a writer can build it without being misled.
   **Done:** every finding states one material correction.

## Checklist

- [ ] Exact Linear record, base SHA, branch, worktree, clean state and owned paths match
- [ ] Every scenario stands at the boundary, refines a parent criterion, and has no idle example column
- [ ] Every cited Decision, pattern and path holds at the base SHA
- [ ] The acceptance lines account for every outcome without hidden design choices
- [ ] Edge cases, failures, migration, compatibility, and rollback are covered when consequential
- [ ] Scenarios and exact gates can prove the outcome; only stage-applicable gates were run
- [ ] Any unanswered question records homework and blocks the work through the Captain
