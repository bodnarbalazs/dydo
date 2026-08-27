# Reviewing a Plan

Target: one Git Project plan proposed as the reviewed intent for coordinated, cross-cutting, or
architecture-sensitive work. Fresh eyes are the point: review the committed artifact, not the planning
conversation. The verdict is recorded in the plan's Linear Project; implementation Issues are not ready
until the plan passes and the exact governing commit is linked.

An atomic Linear Issue may be reviewed intent without a Project plan. Do not require Project-plan
ceremony when one autonomous Issue fully closes its own decisions, ownership, acceptance criteria, and
gates.

## Method

1. **Check the contract shape first** — the plan must identify its Linear Project, intended outcome,
   binding scope, implementation Issue map, ordering and isolation, exact gates, acceptance criteria,
   risks, and assimilation expectations. A missing contract element FAILS before content review.
2. **Verify claims against the codebase** — read the cited files and patterns at the proposed governing
   commit. A plan that misdescribes the code it intends to change is the highest-value catch this review
   makes.
3. **Read every planned Issue as its implementer** — can each Issue become a self-contained Linear
   contract with one owner, exclusive paths, explicit dependencies, and exact gates, without requiring
   another architectural decision? Any interpretive latitude is a finding.
4. **Interrogate the specification** — every question answered, acceptance criteria testable, and
   out-of-scope binding. Verify that independent Issue review, Project-level integrated audit, and a
   proportionate durable assimilation brief are required before completion.
5. **Return a strict verdict** — PASS only when the committed plan can govern execution unchanged.
   Record the review against the Linear Project and link the exact passing commit before Issues start.

Wayfinding Fog is not a specification gap unless the current Project depends on resolving it. Review
the bounded Project contract; do not fail it for uncertainty deliberately left outside its frontier.

## Checklist

- [ ] One `linear-project` provenance URL identifies the owning Linear Project
- [ ] Intent, binding in/out of scope, Issue map, ordering/isolation, gates, acceptance, risks, and
      assimilation expectations are complete
- [ ] Specification is closed: zero open questions — one unanswered execution question is an automatic FAIL
- [ ] Implementation requires no new architectural decisions; concrete examples are included where needed
- [ ] Named code patterns and paths verified at the proposed governing commit
- [ ] Prior art and governing Decisions are evidenced; rejected alternatives have a stated reason
- [ ] Planned Issues are atomic, independently reviewable, and disjoint by owned path, or explicitly serial
- [ ] Dependencies and exact per-Issue test/check commands are explicit
- [ ] Native worktree isolation and merge order prevent parallel workers from colliding
- [ ] Data-shape, migration, compatibility, and rollback hazards are handled
- [ ] Each Issue can stand alone in Linear without copying the entire Project plan
- [ ] Every Issue requires independent review; Project completion requires integrated audit and assimilation
- [ ] Passing commit and branch-following plan links are ready to attach in Linear
