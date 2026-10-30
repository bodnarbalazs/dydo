---
name: wayfinder
description: Fog in a Project map or inside one ticket. Chart the visible route as Wayfinding Tickets and resolve them one at a time until the destination is reached.
---

<!-- Adapted from mattpocock/skills wayfinder at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Wayfinder

The destination is known, but the route is wrapped in **fog**. Wayfinding finds that route; it does
not charge at the destination. The map holds the low-resolution view, and its **Wayfinding Tickets**
clear one part of the route at a time.

The **map holder** charts and grills. A Project's map holder is the user, driving the map in his
own session, or an admiral the user invoked, until the user takes the map back; the Project
description names the current holder. A Ticket Captain may chart local fog inside one approved
delivery outcome.

For a communication protocol, from the repository root read only the Communication and evidence section in `dydo/reference/linear-workspace-standard.md`, not the whole workspace standard.

## Chart as you go

Wayfinder advances large work one manageable step at a time.
Ticket Captains deliver clear Tasks; Wayfinding Tickets clear the fog blocking what comes next.
Defer later decisions until delivery reveals the facts they need.
The map is complete at the destination, with no open Tasks or unresolved fog.

## Refer by name

In everything the user reads, name every Project and ticket by its title, with its Linear key and URL
inside that name. Titles scan; walls of bare keys do not.

## The map

At Project scale, the Linear Project description is the whole map at low resolution. It indexes; each
Wayfinding Ticket stores its own context and resolution. Link shared feature anchors following
`dydo/reference/feature-spec-convention.md`, read from the repository root when creating or changing one.
Open tickets are found through Linear queries.

```markdown
**Map holder:** <the user, or the admiral the user invoked; rewritten when the map changes hands>

## Destination

<what will exist or work differently when this Project is complete; one or two lines>

## Notes

<domain, methods each session should reach for, standing preferences>

## Resolutions so far

- [<closed ticket title and key>](Linear URL): <one-line gist of the resolution>

## Not yet specified

<!-- see "Fog of war": in-scope fog you can't create a ticket for yet; graduates as the frontier advances -->

## Out of scope

<!-- see "Out of scope": work ruled beyond the destination; closed, never graduates -->
```

At delivery scale, the parent Task is the local map; the Project's map holder should already have
cleared most of its fog. If delivery reveals more, the Captain creates a direct Wayfinding Sub-ticket
and records its resolution on it. Anything that could affect another ticket, a shared
contract, or the Project map is escalated to the Project's map holder instead.

## Tickets

The map contains both Tasks and Wayfinding Tickets. Tasks are the route someone builds; a Ticket
Captain owns each one end to end. Wayfinding Tickets clear the fog around that route.

Each ticket has a name, Linear key, and URL. Its body carries one outcome sized to one agent session.
Assignment is the claim: assign it first, before any work, so concurrent sessions skip it.

Blocking uses Linear's native dependency relationship: essential because it renders the frontier
visually in Linear, so the user sees what is takeable without opening the map. A ticket is
**unblocked** when every ticket blocking it is closed; the **frontier** is the open, unblocked,
unassigned tickets at the edge of the known.

The resolution is recorded as a named evidence comment using the `DECIDED` form in the communication
protocol. Assets created while resolving a ticket are linked from it, not pasted in.

For the Project's map holder, contracts are tickets under the Project; for a captain they are
Sub-tickets under its ticket. The same Types, statuses and chain hold. The captain specifies its parent before naming
disjoint parallel lanes, and each merging lane or ticket gets its own Merge Sub-ticket in order, except an atomic ticket's own merge into main.
The standard owns the full Type set and status/priority rules: from the repository root, read
`dydo/reference/linear-workspace-standard.md`.

## Ticket Types

Every captain-held ticket carries Mode **HITL** (human in the loop, worked _with_ a human who speaks for themselves)
or **AFK**, driven by the agent alone. A HITL ticket only resolves through that live exchange; the
agent never stands in for the user's side of it (a grilling agent that answers its own questions has
broken this).

- **Task** (HITL or AFK): the role a captain-held ticket plays on the map, as the standard defines
  it. Its Ticket Captain and crew carry it through its contract, production, review, and its Merge
  Sub-ticket.
- **Research**: Reading documentation, third-party APIs, or local resources like knowledge
  bases to surface a fact a decision waits on. Resolved by a subagent that calls the Skill tool with
  "research". Use when authoritative evidence, inside or outside the repository, can settle the fact.
- **Prototype** (HITL): Raise the fidelity of the discussion by making a cheap, rough, concrete
  artifact to react to (throwaway UI or logic code) by calling the Skill tool with "prototype". Links
  the prototype as an asset. Use when "how should it look" or "how should it behave" is the key
  question.
- **Grilling**: Conversation. The default case. Always call the Skill tool twice, for
  "grilling" and "domain-modeling".
- **Question**: One prepared human choice that authoritative sources and the other ticket types
  cannot settle. The ticket carries the homework, credible options, trade-offs, and recommendation.
- **Enablement** (HITL or AFK): the access, environment, credentials or material other work needs,
  as the standard defines it: nothing to decide, prototype, or research, but the work is blocked
  until it is done. Signing up for a service so its API can be judged, provisioning access, moving
  data so its shape can be seen. This is the one Wayfinding type that _does_ rather than decides.

A captain drives Enablement alone where it can (AFK); for human-only steps it uses `wizard` in
a top-level HITL session. Prototype also has a captain; Research, Grilling and Question stay with
the map holder. Resolved when the work is done; the answer records what was done and any resulting
facts (credentials location, new URLs, row counts) later tickets depend on.

## Fog of war

The map is _deliberately_ incomplete: don't chart what you can't yet see. Beyond the live tickets lies
the **fog of war**: the dim view of decisions and investigations you can tell are coming but can't yet
pin down, because they hang on questions still open. Resolving a ticket clears the fog ahead of it,
graduating whatever's now specifiable into fresh tickets, one at a time, until the way to the
destination is clear and no tickets remain.

The map's **Not yet specified** section is where that dim view is written down: the suspected
question, the area to revisit later. It's the undiscovered frontier _toward_ the destination:
everything here is in scope, just not sharp enough to become a ticket. Write as loosely or as fully
as the view allows; it doubles as a signpost for collaborators reading where the effort is headed.

**Fog or ticket?** The test is whether you can state the question precisely now, _not_ whether you can
answer it now.

- **Ticket when** the question is already sharp, even if it's blocked and you can't act on it yet.
- **Not yet specified when** you can't yet phrase it that sharply. Don't pre-slice the fog into
  ticket-sized pieces: it's coarser than a ticket, and one patch may graduate into several tickets, or
  none, once the frontier reaches it.

**Not yet specified** excludes what is already resolved (Resolutions so far), what is already a live
ticket, and what is out of scope.

### Discovery before Question

Search the governing Decision Records, Project plan, specifications, documentation, standards, code,
tests, and prior answers first. Use Research, Prototype, Grilling, or Enablement where they can clear
the fog. `Question` is the last rung: create it only when human judgment still remains.

Fog can surface through several hands:

| Found by | Recording path |
|---|---|
| Project's map holder | create and wire it while charting, or as delivery clears Project fog |
| Code Writer or other crew | return a prepared hand-raise to the Ticket Captain |
| Ticket Captain | create a local Sub-ticket, or escalate a Project-level packet to the Project's map holder |

An answer graduates to a Decision Record only when it is hard to reverse, surprising later, and the
result of a real trade-off. The ticket carries the question and its working resolution; the Decision
Record carries the durable decision. Link them rather than copying them.

## Out of scope

Fog only ever gathers _toward_ the destination. The destination fixes the scope, so work beyond it is
**out of scope**: it isn't fog, and it doesn't belong in **Not yet specified**. It gets its own
**Out of scope** section on the map: work you've consciously ruled out of _this_ effort. Scope, not
sharpness, lands it here.

Out-of-scope work never graduates (the frontier stops at the destination), so it returns only if the
destination is redrawn, and then as a fresh effort, not a resumption.

Ruling something out of scope is a scoping act, not a step on the route. When a ticket that already
exists turns out to sit past the destination (mis-scoped while charting, or exposed by a resolution),
mark it `Canceled` and leave one line in the **Out of scope** section: the gist plus why it's out of
scope, linking the ticket. It stays out of **Resolutions so far**, which records the route actually
walked; a scope boundary isn't a step on it.

## Invocation

Two modes. Either way, a session the user drives never resolves more than one Wayfinding Ticket,
with the exception of Research Tickets: one ticket is sized to one session. An invoked admiral is
exempt. Tasks may run concurrently under separate Ticket Captains.

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
   **Not yet specified**. For shared behavior or decisions needing one current anchor, choose an
   applicable reviewed architecture plan or feature spec, creating a spec where needed under
   `dydo/reference/feature-spec-convention.md`. Commit a new spec under the map holder's custody and
   make that commit reachable to recipients before relying on its Project-map link; otherwise record
   the anchor as unresolved. Atomic intent stays in its ticket.
4. **Create the tickets you can specify now**, both Tasks and Wayfinding Tickets, then wire blocking
   edges in a **second pass** (tickets need keys before they can reference each other). Derive affected
   Tasks from the anchor's relevant sections and link those sections in their bounded contracts. Wiring sorts
   them into the frontier and the blocked; everything you cannot yet specify stays in the fog. At
   Project scale, group the Tasks into the Project's Milestones in Linear: few and meaningful, the
   walking skeleton first, the landing last.
5. **Fire the Research subagents.** For each Research Ticket just created that is unassigned, in
   `Todo` and unblocked, claim it, then spin up a subagent that uses `research` to resolve it in parallel, recording its cited findings as a comment on that ticket.
6. Stop: charting is one session's work; it resolves nothing itself.

### Work through the map

Working starts from a Project or parent delivery ticket. Naming a frontier ticket is optional: without
one, take the next ticket rather than asking the user to choose.

1. Load the **map**: the low-resolution view, not every ticket body.
2. Choose the ticket. If one was named, use it. Otherwise take the first pickable frontier ticket in `Todo`.
   A captain-held Type goes to its Ticket Captain, which claims it. For a map-holder-held Type,
   claim it before work; a Question still waits for the user's answer.
3. Resolve a Wayfinding Ticket. **Zoom as needed**: fetch the full body of any related or closed ticket
   on demand; use whichever methods the `## Notes` block names. Follow the ticket Type above.
4. Record the resolution when the Type's outcome is reached: post the answer as a **resolution
   comment**, mark the ticket `Done`, and
   append a context pointer to the map's Resolutions so far. For local fog, link the resolution from
   the parent delivery ticket instead; the record is the whole report.
5. Add newly surfaced tickets (create then wire, a Project's Task into its Milestone); graduate any fog the answer has made specifiable,
   clearing each graduated patch from **Not yet specified** so it lives only as its new ticket. If the
   answer reveals that a ticket sits beyond the destination, rule it out of scope. If the resolution
   invalidates other parts of the map, update or cancel those tickets.

Other sessions may work unblocked tickets in parallel, so re-read Linear before every claim or map
mutation.
