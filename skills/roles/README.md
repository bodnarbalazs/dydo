# Roles

10 skills that carry an identity, sorted by whether they hold something or return a result.

## Officers

Officers hold something and never switch: the admiral holds a Project, the issue-captain holds an
Issue, the chief-of-staff holds the board. A session or agent has at most one officer role; a captain
never becomes an admiral, or the reverse.

- **admiral** (user-invoked): Run one Project through planning, captains, reviewed merges and the human's gates.
- **chief-of-staff** (user-invoked): Your attention, triaged — the Questions waiting on your answer, the gates waiting on your approval or your click, and what on the board has gone stale.
- **issue-captain** (model-invoked): One contracted Issue needs a captain: specify, direct the crew, review, merge and release from its recorded state.

## Crew

Crew are spawned for one bounded job, hold nothing, and return their result to whoever sent them.

- **code-writer** (model-invoked): A contracted Issue to build, a review FAIL to close, a merge to perform, or landed code to tighten. Bugs go red before the fix; Features prove their tests fail without the code.
- **docs-writer** (model-invoked): Documentation the repository can witness. Write or correct one reviewed change, including an inquisition's record through its delivery Feature, for its Issue Captain.
- **inquisitor** (model-invoked): Refute-first sweep of landed work. Use when the inquisition's Captain assigns one lens over the named scope, or over one part of it.
- **project-planner** (model-invoked): Ripe Project intent, no reliable route. Write the first pickable Issues and bearings; return the committed plan to the admiral.
- **research** (model-invoked): Primary sources, cited. Use when a fact a choice waits on could hide in Decision Records, plans, code, history, or outside sources, or when docs, specs, or API behaviour must be established before work depends on them.
- **reviewer** (model-invoked): An Issue's code or docs, a spec, a Project plan, or a merged tree — one candidate, one named rubric, one binding verdict.
- **scout** (model-invoked): One question, one source family, passages back, for a researcher who verifies and pools them.
