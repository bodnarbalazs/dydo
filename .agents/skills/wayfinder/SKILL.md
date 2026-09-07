---
name: wayfinder
description: Fog in a Project map or inside one Issue. Chart the visible route as Wayfinding Issues and resolve them one at a time until the destination is reached.
---

<!-- Adapted from mattpocock/skills wayfinder at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Wayfinder

The destination is known, but the route is wrapped in **fog**. Wayfinding finds that route; it does
not charge at the destination. The map holds the low-resolution view, and its **Wayfinding Issues**
clear one part of the route at a time.

The Project Planner charts the first Project map, the admiral works it during delivery, and an Issue
Captain may chart local fog inside one approved delivery outcome.

## Chart as you go

Wayfinder advances large work one manageable step at a time.
Issue Captains deliver clear Tasks; Wayfinding Issues clear the fog blocking what comes next.
Defer later decisions until delivery reveals the facts they need.
The map is complete at the destination, with no open Tasks or unresolved fog.

## Refer by name

In everything the human reads, name every Project and Issue by its title, with its Linear key and URL
inside that name. Titles scan; walls of bare keys do not.

## The map

At Project scale, the Linear Project description is the whole map at low resolution. It indexes; each
Wayfinding Issue stores its own context and resolution. Open Issues stay out of the description and
are found through Linear queries.

```markdown
## Destination

<what will exist or work differently when this Project is complete; one or two lines>

## Notes

<domain, methods each session should reach for, standing preferences>

## Resolutions so far

- [<closed Issue title and key>](Linear URL): <one-line gist of the resolution>

## Not yet specified

<!-- see "Fog of war": in-scope fog you can't create an issue for yet; graduates as the frontier advances -->

## Out of scope

<!-- see "Out of scope": work ruled beyond the destination; closed, never graduates -->
```

At delivery scale, the parent Task is the local map; the admiral should already have cleared most of
its fog. If delivery reveals more, the Captain creates a direct Wayfinding Sub-issue and reports its
resolution to the admiral. Anything that could affect another Issue, a shared contract, or the
Project map is escalated instead.

## Issues

The map contains both Tasks and Wayfinding Issues. Tasks are the route someone builds; an Issue
Captain owns each one end to end. Wayfinding Issues clear the fog around that route.

Each Issue has a name, Linear key, and URL. Its body carries one outcome sized to one agent session.
Assignment is the claim: assign it first, before any work, so concurrent sessions skip it.

Blocking uses Linear's native dependency relationship: essential because it renders the frontier
visually in Linear, so the human sees what is takeable without opening the map. An Issue is
**unblocked** when every Issue blocking it is closed; the **frontier** is the open, unblocked,
unassigned Issues at the edge of the known.

The resolution is recorded as a comment. Assets created while resolving an Issue are linked from it,
not pasted in.

For the admiral, contracts are Issues under the Project; for a captain they are Sub-issues under
its Issue. The same Types, statuses and chain hold. The captain specifies its parent before naming
disjoint parallel lanes, and each merging lane or Issue gets its own Merge Sub-issue in order.
The standard owns the full Type set and status/priority rules: read
[linear-workspace-standard.md](../../../dydo/reference/linear-workspace-standard.md).

## Issue Types

Every captain-held Issue carries Mode **HITL** (human in the loop, worked _with_ a human who speaks for themselves)
or **AFK**, driven by the agent alone. A HITL Issue only resolves through that live exchange; the
agent never stands in for the human's side of it (a grilling agent that answers its own questions has
broken this).

- **Task** (HITL or AFK): A `Feature` or `Bug` Issue built by an Issue Captain and
  crew through specification, production, review, and its Merge Sub-issue.
- **Research**: Reading documentation, third-party APIs, or local resources like knowledge
  bases to surface a fact a decision waits on. Resolved by a subagent that calls the Skill tool with
  "research". Use when authoritative evidence, inside or outside the repository, can settle the fact.
- **Prototype** (HITL): Raise the fidelity of the discussion by making a cheap, rough, concrete
  artifact to react to (throwaway UI or logic code) by calling the Skill tool with "prototype". Links
  the prototype as an asset. Use when "how should it look" or "how should it behave" is the key
  question.
- **Grilling**: Conversation. The default case. Always call the Skill tool twice, for
  "grilling" and "domain-modeling".
- **Question**: One prepared human choice that authoritative sources and the other Issue types
  cannot settle. The Issue carries the homework, credible options, trade-offs, and recommendation.
- **Enablement** (HITL or AFK): Manual work that must happen before a _decision_ can be made:
  nothing to decide, prototype, or research, but the discussion is blocked until it is done. Signing
  up for a service so its API can be judged, provisioning access, moving data so its shape can be
  seen. This is the one Wayfinding type that _does_ rather than decides, and it earns its place by
  unblocking a decision, not by delivering the destination.

A captain drives Enablement alone where it can (AFK); for human-only steps it uses `wizard` in
a top-level HITL session. Prototype also has a captain; Research, Grilling and Question stay with
the map holder. Resolved when the work is done; the answer records what was done and any resulting
facts (credentials location, new URLs, row counts) later Issues depend on.

## Fog of war

The map is _deliberately_ incomplete: don't chart what you can't yet see. Beyond the live Issues lies
the **fog of war**: the dim view of decisions and investigations you can tell are coming but can't yet
pin down, because they hang on questions still open. Resolving an Issue clears the fog ahead of it,
graduating whatever's now specifiable into fresh Issues, one at a time, until the way to the
destination is clear and no Issues remain.

The map's **Not yet specified** section is where that dim view is written down: the suspected
question, the area to revisit later. It's the undiscovered frontier _toward_ the destination:
everything here is in scope, just not sharp enough to become an Issue. Write as loosely or as fully
as the view allows; it doubles as a signpost for collaborators reading where the effort is headed.

**Fog or Issue?** The test is whether you can state the question precisely now, _not_ whether you can
answer it now.

- **Issue when** the question is already sharp, even if it's blocked and you can't act on it yet.
- **Not yet specified when** you can't yet phrase it that sharply. Don't pre-slice the fog into
  Issue-sized pieces: it's coarser than an Issue, and one patch may graduate into several Issues, or
  none, once the frontier reaches it.

**Not yet specified** excludes what is already resolved (Resolutions so far), what is already a live
Issue, and what is out of scope.

### Discovery before Question

Search the governing Decision Records, Project plan, specifications, documentation, standards, code,
tests, and prior answers first. Use Research, Prototype, Grilling, or Enablement where they can clear
the fog. `Question` is the last rung: create it only when human judgment still remains.

Fog can surface through several hands:

| Found by | Recording path |
|---|---|
| Project Planner | return the prepared Question packet and its waiters to the admiral |
| admiral | create and wire it as delivery clears Project fog |
| Specifier or worker | return a prepared hand-raise to the Issue Captain |
| Issue Captain | create a local Sub-issue, or escalate a Project-level packet to the admiral |

An answer graduates to a Decision Record only when it is hard to reverse, surprising later, and the
result of a real trade-off. The Issue carries the question and its working resolution; the Decision
Record carries the durable decision. Link them rather than copying them.

## Out of scope

Fog only ever gathers _toward_ the destination. The destination fixes the scope, so work beyond it is
**out of scope**: it isn't fog, and it doesn't belong in **Not yet specified**. It gets its own
**Out of scope** section on the map: work you've consciously ruled out of _this_ effort. Scope, not
sharpness, lands it here.

Out-of-scope work never graduates (the frontier stops at the destination), so it returns only if the
destination is redrawn, and then as a fresh effort, not a resumption.

Ruling something out of scope is a scoping act, not a step on the route. When an Issue that already
exists turns out to sit past the destination (mis-scoped while charting, or exposed by a resolution),
mark it `Canceled` and leave one line in the **Out of scope** section: the gist plus why it's out of
scope, linking the Issue. It stays out of **Resolutions so far**, which records the route actually
walked; a scope boundary isn't a step on it.

## Invocation

Two modes. Either way, never resolve more than one Wayfinding Issue per session, with the exception
of Research Issues. Tasks may run concurrently under separate Issue Captains.

### Chart the map

Charting starts from a Linear Project and a loose destination.

1. **Name the destination.** Use `grilling` and `domain-modeling` to pin down what this map is
   finding its way to: the spec, Decision Record, or change. The destination fixes the scope, so it
   is settled first.
2. **Map the frontier.** Grill again, **breadth-first** this time: fan out across the whole space
   rather than deep on any one thread, surfacing the open questions and the first steps takeable now.
   **If this surfaces no fog** (the way to the destination is already clear, the whole journey small
   enough for one session), no map is needed.
3. **Write the map:** Destination and Notes filled in, Resolutions so far empty, the fog sketched into
   **Not yet specified**.
4. **Create the Issues you can specify now**, both Tasks and Wayfinding Issues, then wire blocking
   edges in a **second pass** (Issues need keys before they can reference each other). Wiring sorts
   them into the frontier and the blocked; everything you cannot yet specify stays in the fog.
5. **Fire the Research subagents.** For each Research Issue just created, spin up a subagent that uses
   `research` to resolve it in parallel, recording its cited findings as a comment on that Issue.
6. Stop: charting is one session's work; it resolves nothing itself.

### Work through the map

Working starts from a Project or parent delivery Issue. Naming a frontier Issue is optional: without
one, take the next Issue rather than asking the human to choose.

1. Load the **map**: the low-resolution view, not every Issue body.
2. Choose the Issue. If one was named, use it. Otherwise take the first frontier Issue in order.
   A captain-held Type goes to its Issue Captain, which claims it. For a map-holder-held Type,
   claim it before work; a Question still waits for the human's answer.
3. Resolve a Wayfinding Issue. **Zoom as needed**: fetch the full body of any related or closed Issue
   on demand; use whichever methods the `## Notes` block names. Follow the Issue Type above.
4. Record the resolution when the Type's outcome is reached: post the answer as a **resolution
   comment**, mark the Issue `Done`, and
   append a context pointer to the map's Resolutions so far. For local fog, link the resolution from
   the parent delivery Issue and inform the admiral instead.
5. Add newly surfaced Issues (create then wire); graduate any fog the answer has made specifiable,
   clearing each graduated patch from **Not yet specified** so it lives only as its new Issue. If the
   answer reveals that an Issue sits beyond the destination, rule it out of scope. If the resolution
   invalidates other parts of the map, update or cancel those Issues.

Other sessions may work unblocked Issues in parallel, so re-read Linear before every claim or map
mutation.
