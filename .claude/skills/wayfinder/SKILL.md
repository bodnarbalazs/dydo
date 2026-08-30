---
name: wayfinder
description: Explicitly invoked by the human to navigate an active Linear Project whose route is too foggy for one agent session.
disable-model-invocation: true
---

<!-- Adapted from mattpocock/skills wayfinder at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Wayfinder

A committed Linear Project is too large for one agent session, and the route to its destination is
still wrapped in fog. Wayfinding finds that route; it does not charge at the destination. The Linear
Project is the shared map. Its decision Issues resolve questions, not slices of a build.

Naming the destination is the first act of charting because it shapes every decision. The destination
might be a spec ready for planning, a decision locked before implementation, or a change made in place.

## Plan, don't do

Wayfinder is planning by default. The Project is ready to leave Wayfinding when nothing remains to be
decided before implementation. The pull to do the work usually means the edge of the map has been
reached and the result should move to the normal planning and delivery workflow. An explicit note in
the Project may extend the map into execution; otherwise produce decisions, not deliverables.

## Refer by name

In everything the human reads, identify every Project and Issue by its title, with its Linear key and
URL attached. A bare wall of identifiers is illegible. The title carries the meaning; the key and URL
preserve exact identity.

## The map

The active Linear Project is the canonical map. Its decision tickets are Issues in that Project.
Use the Project description as a low-resolution index, not a second store of detail:

```markdown
## Destination

<what reaching the end of this map looks like; one or two lines>

## Notes

<domain, relevant skills, and standing preferences>

## Decisions so far

- [<closed Issue title and key>](Linear URL): <one-line gist of the answer>

## Not yet specified

<in-scope fog that is not sharp enough to become an Issue yet>

## Out of scope

<work consciously ruled beyond this destination>
```

Open Issues are discovered from the Project, not copied into its description. A decision lives in one
place: its Issue. The map only gists and links the resolution. Put durable Decisions and evidence in
dydo/Git when they must outlive delivery, then link that artifact from the Issue rather than duplicating
it.

### Decision Issues

Each decision is one Linear Issue, sized for one agent session:

```markdown
## Question

<the decision or investigation this Issue resolves>
```

Classify it as `research`, `prototype`, `grilling`, or `task`. Use the existing HITL or AFK label for
who must participate. A HITL Issue resolves only through a live exchange with the human; the agent
never answers for the human.

Assignment is the claim: assign an Issue before work so concurrent sessions skip it. Use Linear's
native dependency relations. An Issue is unblocked when every blocker is closed; the frontier is the
open, unblocked, unassigned decision Issues at the edge of what is known. Record the answer on
resolution, not in the initial body. Link assets instead of pasting them into the body.

## Decision types

- **Research (AFK):** find a fact from primary sources, project knowledge, or the environment. Bounded
  research Issues may run in parallel through native discovery subagents.
- **Prototype (HITL):** make a cheap concrete artifact that raises the fidelity of a discussion about
  appearance or behaviour. Link it from the Issue.
- **Grilling (HITL):** use the Grilling skill to resolve decisions with the human. This is the default.
- **Task (HITL or AFK):** do bounded manual work required before a decision can be made. It earns its
  place by unblocking a decision, not by delivering the destination.

## Fog of war

The map is deliberately incomplete. Not yet specified holds in-scope questions you can see coming but
cannot yet state precisely because they depend on open decisions. Resolving an Issue clears the fog
ahead of it and graduates newly precise questions into Issues.

The test is precision, not answerability:

- Create an Issue when the question is sharp, even when it is blocked.
- Keep it in Not yet specified when the question itself is still vague.

Fog excludes settled decisions, live Issues, and out-of-scope work.

## Out of scope

The destination fixes scope. Work beyond it is not fog. Record it in Out of scope and never graduate it
unless the human redraws the destination as a fresh effort. When an existing Issue proves out of scope,
close it and add one linked line explaining why; do not present that boundary as a decision on the route.

## Invocation

Wayfinder has two modes. In either mode, resolve at most one non-research decision Issue per session.

### Chart the map

The human invokes Wayfinder with an active Linear Project and a loose destination.

1. Name the destination with the human. Use Grilling where decisions remain.
2. Map the frontier breadth-first. Surface the decisions that are sharp now and sketch the remaining
   fog. If there is no fog and the journey fits one session, stop and ask whether Wayfinder is needed.
3. Write the Project description using the map body above.
4. Create the decision Issues that are sharp now, then wire native blocking relations in a second pass
   after their identifiers exist.
5. Dispatch bounded native discovery subagents for independent Research Issues.
6. Stop. Charting does not resolve a decision Issue.

### Work through the map

The human invokes Wayfinder with a Linear Project; naming an Issue is optional.

1. Load the Project's low-resolution map, not every Issue body.
2. Use the named Issue or take the first frontier Issue. Assign it before working.
3. Resolve that one decision. Load related detail only as needed and use Grilling for human choices.
4. Post the resolution, close the Issue, and append one titled link and gist to Decisions so far.
5. Create and wire newly sharp Issues; remove graduated fog. Close and classify anything now beyond the
   destination. Update or delete Issues invalidated by the decision.

Expect other sessions to work unblocked Issues concurrently. Re-read Linear before mutating the map.
