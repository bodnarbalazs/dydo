# Reviewing a Plan

Target: one Project plan proposed as the reviewed intent for a Linear Project, judged against the
skeleton in the planner's `project` resource. Fresh eyes are the point: review the committed artifact,
not the planning conversation. No Issue is pickable until this review passes and the governing commit
is linked.

An atomic Linear Issue can be reviewed intent on its own. Do not demand Project-plan ceremony when one
Issue closes its own outcome, owned paths, blockers, exact gates and base branch.

## Method

1. **Check the shape first.** Frontmatter carries `title`, `status`, `area`, `type` and the
   `linear-project` URL; the six numbered sections are present and in order — Specification (intent,
   in scope, out of scope, acceptance criteria, questions and answers), Prior art, Design,
   Implementation Issue map with its exact-gate blocks, Ordering and isolation, Watch-outs. A missing
   element FAILs before content review.
2. **Verify claims against the codebase.** Read the cited paths and patterns at the proposed governing
   commit. A plan that misdescribes the code it intends to change is the highest-value catch this
   review makes.
3. **Read every planned Issue as its implementer.** From its five fields — outcome, owned paths,
   blockers, exact gates, base branch — plus the section it cites, each row must become a
   self-contained Linear contract that needs no further architectural decision. Interpretive latitude
   is a finding.
4. **Weigh the map as a route.** Every in-scope bullet is claimed by an Issue; every Issue is a tracer
   bullet that cuts end to end and lands something; a wide refactor widens by expand–contract rather
   than by one Issue touching everything; the map closes with integration and with the durable
   knowledge this Project owes dydo. Acceptance criteria are each provable at the final merge.
5. **Return the review block.** PASS only when the committed plan can govern execution unchanged.

**Wayfinding Fog is not a gap.** In-scope fog belongs under `## Not yet specified`; fog sharp enough to
state as a question belongs in a question Issue wired as a blocker of what it holds up, even when
nothing can answer it yet. Judge whether the fog is placed, not whether it is cleared — but a plan that
pretends a complete route where fog exists FAILs, and so does a question left floating in prose.

**Amendments.** A dated amendment section that leaves reviewed text standing is the expected form. Only
a change to scope, acceptance criteria or the Issue map returns here; the rest is already reviewed.

## Checklist

- [ ] Frontmatter complete, `linear-project` names the owning Project, `dydo check` clean
- [ ] The six numbered sections are present, in order, and keep the numbers briefs cite
- [ ] Every in-scope bullet is claimed by an Issue; out of scope states what it excludes and why
- [ ] Acceptance criteria are numbered and each proved at the final merge by a command, diff or artifact
- [ ] Settled questions carry their answers; unsettled fog is `## Not yet specified` or a question Issue
- [ ] Prior art evidences the commits, sources and Decision Records read, and what each gave
- [ ] Design names paths and patterns verified at the governing commit instead of restating the code
- [ ] Every Issue row carries outcome, owned paths, blockers, gate and base branch
- [ ] Issues are disjoint by owned path or explicitly serial, with one owner per hot file at a time
- [ ] Each gate letter has a copy-pasteable command block and what its evidence must prove
- [ ] Ordering names the kickoff acts, the merge order and what runs in parallel
- [ ] Watch-outs, migration, compatibility and rollback hazards are stated rather than implied
- [ ] Each Issue can stand alone in Linear without copying the plan
