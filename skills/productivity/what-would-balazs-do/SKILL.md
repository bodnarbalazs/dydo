---
name: what-would-balazs-do
description: A call Balazs would otherwise be asked. Use when an officer must decide one while he is away, prepare a DR-grade or irreversible one for him, or when he asks for it on any call.
---

# What Would Balazs Do

Make the call the way he would, or prepare it with the answer he would give. A tribunal of three
tests it against his own words.

## Must-Reads

1. From the repository root, read the Question Tickets and Communication and evidence sections of
   `dydo/reference/linear-workspace-standard.md`.
2. From the repository root, read `dydo/project/decisions/_decisions.md`.

## Authority

A judge goes straight to the Tribunal section.

Officers only, each inside its own remit: a captain on calls inside its ticket contract, the map
holder on scope, destination, architecture and cross-ticket calls. Crew returns its call to its
captain; the chief of staff never runs it. A vote never widens an officer's remit.

## When

Research first: a call the facts settle is no Question. DR-grade is the Decision Record test: hard
to reverse, surprising without context, a real trade-off. Away means
`dydo mode show` reports `overnight` `on`; off or unreadable, he is present.

| The call | He is present | He is away |
|---|---|---|
| Easily reversible | you decide; no skill | you decide; no skill |
| Above your say, below DR-grade | a plain Question; no skill | the tribunal decides; on thin case law, the most reversible option, flagged |
| DR-grade | the tribunal prepares the Question | the tribunal decides if unanimous, and writes the DR |
| Irreversible | his alone; the tribunal prepares the Question | the same |

He may invoke this skill on any call.

## Case law

Only his own words are his precedent: Decision Records (superseded ones as history), the docs, his
answered Questions and grillings, and past conversations where available. A later word outranks an
earlier one. Quote every citation. A tribunal's output is never precedent until he ratifies it.
Search the current project unless he points it at another project's records.

## Tribunal

Run it by [tribunal](resources/tribunal.md); judges load this skill and follow the same file.

## Verdict

Each member's ballot names one exact, written action.

| Away, the call is | Decided by | Otherwise |
|---|---|---|
| DR-grade | all three naming the same text | no decision: a prepared Question |
| below DR-grade | two naming the same text | three different actions: the most reversible option, flagged |

While he is present, the result is always a prepared Question carrying the predicted answer and any
dissent. An incomplete tribunal decides nothing; the call becomes a prepared Question.

## Record

Every call is a `Question` ticket in `Todo`, his turn, with the tribunal log attached as a Linear
Document.

- **Decided:** post
  `DECIDED - by: what-would-balazs-do tribunal, unratified; result: <action>; vote: <n of 3, or split; most reversible>; log: <link>; undo: <steps>; next: <action>.`
  Leave the Question unwired; work proceeds. A DR-grade decision also writes the repo DR
  `status: proposed`, `ratification: pending`, linking the log.
- **Prepared or not decided:** no `DECIDED` post. The Question's body carries the prediction, any
  dissent, the stop reason and the log link; wire it as a blocker of every waiter.
- **His answer:** post `RATIFIED` or `OVERTURNED` in the standard's form, quoting him, and move the
  Question to `Done`. Ratified, the DR becomes `accepted`. Overturned, the DR becomes `deprecated`,
  or `superseded` by the record his answer calls for, quotes his answer, and the affected work
  reopens. Either way, drop `ratification`.
