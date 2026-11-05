# Scaffold a Project

Follow this checklist when you create a Project. The working spec keeps what the user agreed in
Git for the whole implementation, where every captain and reviewer can pin it.

- [ ] Settle the intent with the user (co-think, grilling, `to-project`), then create the Linear
      Project naming you its map holder.
- [ ] Write the working spec at `dydo/reference/working-specs/<project-slug>/<project-slug>.md`
      with the headings in `dydo/reference/feature-spec-convention.md` (from the repository root). It holds what the user agreed: the problem, the behavior, the decisions, what's out
      of scope and the acceptance checks. Keep his words where they matter.
- [ ] The first captain opens `feature/<project-slug>` and commits the spec, unchanged, as its
      first commit. Commission no other ticket until the spec is on the remote feature branch;
      then link it from the Project description, which keeps only the map.
- [ ] Chart by Method step 2. Contract each ticket from the spec sections it serves.
- [ ] Open by Method step 3: Merge Sub-tickets, the ledger, the check-in schedule.
- [ ] Intent changes: update the spec first, then reconcile the affected contracts.
- [ ] Landing or cancellation: retire the spec by the convention's Retirement section.
