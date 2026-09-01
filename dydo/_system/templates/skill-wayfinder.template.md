---
mode: wayfinder
description: Fog — the route through a Linear Project is not visible yet. Use when charting a foggy Project as a map of question Issues, when working its frontier one question at a time, or when a question in the way is too big to settle inline.
emit: skill
invocation: automatic
---

<!-- Adapted from mattpocock/skills wayfinder at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Wayfinder

A Linear Project is too large for one agent session and wrapped in **fog**: the way from here to the
**destination** is not visible yet. Wayfinding finds that way; it does not charge at the destination.
The Project is the shared map, and its **question Issues** hold the questions the route waits on. The
planner charts a foggy Project; the admiral works the map afterwards.

Naming the destination is the first act of charting, because it shapes every question Issue: a spec
ready for planning, a Decision Record locked before implementation, or a change made in place.

## Plan, don't do

The map produces answers, not deliverables. It is finished when nothing implementation waits on
remains open. The pull to do the work usually means the edge of the map has been reached and the route
is ripe for planning; a standing line in the Project's Notes may carry the map into execution instead.

## Refer by name

In everything the human reads, name every Project and Issue by its title, with its Linear key and URL
riding inside that name. A wall of bare keys is illegible; titles read at a glance.

## The map

The Project description is the whole map at low resolution, loaded once per session. It indexes; the
Issue stores. An answer lives in exactly one place — its Issue — so the map gists it and links it.
Open question Issues stay out of the description: they are found by query.

```markdown
## Destination

<the spec, Decision Record, or change this Project is finding its way to; one or two lines, and every
session orients to it before choosing a question>

## Notes

<domain, the skills every session should reach for, standing preferences for this Project>

## Answers so far

- [<closed Issue title and key>](Linear URL): <one-line gist of the answer>

## Not yet specified

<in-scope fog that is not sharp enough to file yet>

## Out of scope

<work consciously ruled beyond this destination>
```

### Question Issues

Each open question is one Linear Issue in the Project, labelled `question` and sized for one agent
session:

```markdown
## Question

<the question this Issue resolves, and what was already searched for its answer>
```

Name its type — research, prototype, grilling or task — and carry the HITL or AFK label for who must
participate. A HITL Issue resolves only through a live exchange with the human; the agent never
answers for the human.

Assignment is the claim: assign an Issue before any work so concurrent sessions skip it. Blocking uses
Linear's native relations, so the frontier renders in Linear's own UI. An Issue is unblocked when every
blocker is closed, and the **frontier** is the open, unblocked, unassigned question Issues at the edge
of what is known. Record the answer on resolution, and link assets rather than pasting them.

## Question types

- **Research (AFK)** — find a fact the answer waits on from primary sources, project knowledge, or the
  environment, using **research**. Bounded research Issues run in parallel as sub-agents.
- **Prototype (HITL)** — raise the fidelity of the exchange with a cheap, rough, concrete artifact to
  react to, using **prototype**, linked from the Issue. Use when how it should look or how it should
  behave is the question.
- **Grilling (HITL)** — conversation, and the default case. Use **grilling**.
- **Task (HITL or AFK)** — bounded manual work that must happen before a question can be answered:
  provisioning access, or moving data so its shape can be seen. It earns its place by unblocking an
  answer rather than by delivering the destination, and its answer records what was done plus the facts
  later questions depend on.

## Fog of war

The map is deliberately incomplete: chart what you can see. Beyond the live Issues lies the fog — the
questions you can tell are coming but cannot yet state precisely, because they hang on questions still
open. Answering one clears the fog ahead of it, and whatever is now sharp graduates into a fresh Issue.
**Not yet specified** holds that dim view: in scope, neither answered nor already a live Issue.

The test is precision, not answerability:

- **File an Issue** when the question is already sharp, even when it is blocked.
- **Keep it in Not yet specified** when you cannot yet phrase it that sharply. One patch of fog may
  graduate into several Issues, or into none.

### Fog, then discovery, then a question Issue

An agent in fog runs a bounded discovery first: the Decision Record index, the Project plan, the
Issue's own links, the glossary, then the code. Only when that comes up empty does it file a question
Issue, listing what it searched, wire the blocking relation, and route it — through the admiral when
the Project itself is foggy, the planner when the plan needs refinement, the human only when HITL. The
filing test is grilling's own sentence: facts are the agent's job, choices are the human's. Native
blocking does the pickup.

An answer resolves its Issue and stays there; it graduates to a Decision Record only when it is hard to
reverse, surprising later, and the result of a real trade-off. Issues carry questions, Decision Records
carry decisions, and the two are linked rather than copied.

## Out of scope

Fog gathers only toward the destination, so work beyond it is out of scope rather than fog, and it
returns only if the human redraws the destination, as a fresh effort. When an existing Issue turns out
to sit past the destination, close it and leave one line in Out of scope: the gist, why it is out, and
a link to the closed Issue. It stays out of Answers so far, which records the route actually walked.

## Chart the map

Charting starts from an active Project and a loose destination.

1. Name the destination with the human, using **grilling** where the choice is theirs. The destination
   fixes scope, so it is settled first.
2. Map the frontier breadth-first: fan out across the whole space rather than deep on one thread,
   surfacing the questions that are sharp now and sketching the rest as fog. If no fog surfaces and the
   journey fits one session, say so and ask how the human wants to proceed.
3. Write the map body into the Project description, with Answers so far empty and the fog sketched into
   Not yet specified.
4. Create the question Issues that are sharp now, then wire blocking relations in a second pass, once
   their keys exist.
5. Dispatch one **research** sub-agent per research Issue created, in parallel; its cited findings land
   as a comment on that Issue.
6. Stop. Charting answers nothing itself.

## Work the map

Working the map starts from a Project; naming an Issue is optional. Answer at most one non-research
question Issue per session.

1. Load the map: the Project description at low resolution, not every Issue body.
2. Take the named Issue, or else the first frontier Issue in order. Assign it before any work.
3. Answer it. Zoom as needed — fetch the full body of a related or closed Issue on demand, and reach
   for the skills the Notes name. When in doubt, **grilling**.
4. Post the answer as a comment, close the Issue, and append one titled link and gist to Answers so far.
5. Create and wire newly sharp Issues, clearing each graduated patch from Not yet specified so it lives
   only as its Issue. Rule anything now beyond the destination out of scope, and update or delete the
   Issues this answer invalidates.

Expect other sessions to work unblocked Issues concurrently, so re-read Linear before mutating the map.
