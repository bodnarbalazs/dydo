# Scaffold a Project

Follow this checklist when you create a Project, or first chart one that has no working spec. The
working spec keeps what the user agreed in Git for the whole implementation, where every captain
and reviewer can pin it.

- [ ] Settle the intent with the user (co-think, grilling, `to-project`), then create the Linear
      Project, or take the user's, naming you its map holder.
- [ ] Draft the working spec for `dydo/reference/working-specs/<project-slug>/<project-slug>.md`
      with the headings in `dydo/reference/feature-spec-convention.md` (from the repository root).
      It holds what the user agreed: the problem, the behavior, the decisions, what's out of scope
      and the acceptance checks. Keep his words where they matter. Put the draft in the Project
      description under `## Working spec draft`.
- [ ] Chart and get approval by Method step 2. Contract each ticket from the spec sections it serves.
      Before any commission, wire the first ticket as a native Linear blocker of every other first
      lane: `dydo survey` and the check-in Tick then see those lanes blocked until it is `Done`.
- [ ] Open by Method step 3, starting the check-in schedule first. Brief the first captain to commit
      the draft, unchanged, directly on `feature/<project-slug>` as its first commit, push it and
      post the SHA on its ticket, before it cuts its ticket branch. The check-in finds a stalled or
      released first captain; resume or replace it from the draft.
- [ ] The spec is on the remote: link it from the Project description and delete the draft.
- [ ] Intent changes: update the spec first, then reconcile the affected contracts.
- [ ] Landing or cancellation: retire the spec by the convention's Retirement section.
