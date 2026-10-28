---
area: general
type: decision
status: accepted
date: 2026-10-28
---

# 060 - Milestone Walkthroughs and the Walking Skeleton

A Project's Linear Milestones are the user's checkpoints. Each one ends on a Walkthrough Ticket that
walks the whole product as it stands, and only the first, the walking skeleton, holds back the work
built on it, except overnight. Ruled by Balazs on 2026-10-27 in a co-think and on 2026-10-28 on the
wording, in [DYD-399](https://linear.app/bodnar-balazs/issue/DYD-399).

## Context

The user's attention entered a Project only at its end: land, inquisition, walkthrough, a lap, another
walkthrough. After he approved the charted route, the admiral's one touchpoint with him was its
landing step. Taste is cheapest early and most expensive after landing. `to-ticket` already cut
tracer-bullet slices, each "demoable or verifiable on its own", but he was never shown the demo.

He is the one resource that does not scale, and the bet is on agents getting better, so admirals
running AFK stays right. The flaw was where his attention enters, not how much the agents do alone:
he must stay in the loop as the product grows without becoming its bottleneck.

Three options were weighed in the co-think "What can dydo learn from tstack?" (2026-10-27):

- **A.** The status quo: one walkthrough, after landing.
- **B.** Milestone checkpoints: the user walks the product at a few Milestones the map holder places.
- **C.** Per-ticket driving in the style of t3dotgg (Theo): the user steers each ticket as it lands.

## Decision

Option B.

1. **Milestones are the checkpoints.** The map holder places a Project's Milestones in Linear while
   charting: few, each a meaningful chunk of work where a drift from the user's intent would show.
   There is no count rule; the map holder judges. The first is the **walking skeleton**: the rough
   foundation the Project is built on, the first solid thing where he can make decisions and
   understand the architecture. The landing is the last. Every delivery ticket belongs to one.
   Milestones are per-Project in Linear while Initiatives span Projects, so a Milestone is the right
   level; it also gives a large board a coarser layer to read.
2. **Each Milestone has a Walkthrough Ticket.** When a Milestone's last ticket has merged into the
   feature branch, the map holder prepares its Walkthrough Ticket and offers it to the user; the last
   Milestone's is the landing's, after the landing. He walks it with the map holder, by `walkthrough`,
   or skips it. **A skip is a verdict to go on.** The ticket is prepared even when he skips it.
3. **Only the walking skeleton blocks.** Nothing is built on the skeleton until his verdict: its
   Walkthrough Ticket, filed with the map, is a native blocker of the next Milestone's delivery
   tickets until his verdict or skip closes it (Balazs, 2026-10-29). Later Milestones never stop
   the work: their findings become tickets in the Project as they arrive.
4. **Overnight, the skeleton does not wait either.** Its Walkthrough Ticket blocks nothing; the
   admiral posts `DECIDED` on it, carries on, and treats a wrong direction as a revert. A call above its say goes through
   `what-would-balazs-do` as usual.
5. **The walkthrough walks the whole current state**: the feature branch at a Milestone's SHA, or main
   at the landing. It must make sense to a user who is not close in, with the emphasis on what
   changed since the last walkthrough and what needs the most careful look. A checkpoint is the same
   skill and the same Type, not a new concept. Its part on what reviewers flagged or deferred uses
   the severities of [DR 058](./058-review-severities-blocker-should-nit.md): each deferred should and
   where it lives, and at the landing every unaddressed nit, for the user to promote any.
6. **The Walkthrough Type changes.** It stays primary only. It was "the user inspects what landed",
   closing when he had walked it; it now walks the whole product as it stands, one per Milestone,
   the landing's last, and closes on his verdict quoted on the ticket, or his skip recorded, with
   findings filed as tickets. Its template gains `## Verdict` and replaces `## What landed` with
   `## Checkpoint`: the Milestone or the landing, its branch and SHA.
7. **Verdicts are case law.** His Walkthrough verdicts, findings and skips are his own words and
   samples of his taste, so `what-would-balazs-do` reads them as precedent. A skip records a chunk he
   trusted without looking.

The inquisition still runs before landing, as rebuilt in
[DYD-400](https://linear.app/bodnar-balazs/issue/DYD-400): follow-up inquisitions until one finds no
blocker or should. This record references that rebuild and does not define it.

## Consequences

- His taste enters at the walking skeleton, where a wrong direction is cheapest to correct.
- AFK work keeps moving past every later Milestone; his findings arrive as ordinary tickets.
- Overnight, work can build on a skeleton he has not seen; a wrong direction then costs a revert.
- Every Project carries one Walkthrough Ticket per Milestone, prepared even when he skips it.
- Each verdict, finding and skip adds to the evidence `what-would-balazs-do` reads, so later Projects
  should need fewer checkpoints.

## Supersedes

Partially supersedes
[DR 047](./047-supersymmetry-hop-statuses-merge-issues-and-the-release-protocol.md)'s Walkthrough
Type, "the human has inspected what landed", and its single walkthrough after the landing, for those
questions only. It also ends the optional status of Milestones that
[DR 044](./044-linear-canonical-pm-and-dydo-knowledge-boundary.md) and DR 047 recorded: a charted
Project now places them. Both stay `accepted` otherwise.

## Related

- [Linear Workspace Standard](../../reference/linear-workspace-standard.md#ticket-labels) - The Walkthrough Type and its template
- [Work Model](../../understand/work-model.md) - The flow map's Walkthrough row
- [Glossary](../../reference/dydo-glossary.md) - Milestone and walking skeleton
- [DR 058 - Review Severities: Blocker, Should, Nit](./058-review-severities-blocker-should-nit.md)
