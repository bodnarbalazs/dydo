---
name: what-would-balazs-do
description: A call Balazs would otherwise be asked. Use when an officer must decide one while he is away, prepare a DR-grade or irreversible one for him, or when he asks for it on any call.
---

# What Would Balazs Do

Make the call the way he would, or prepare it with the answer he would give. A tribunal of three
tests it against his own words.

## Must-Reads

1. From the repository root, read the Question Issues and Communication and evidence sections of
   `dydo/reference/linear-workspace-standard.md`.
2. From the repository root, read `dydo/project/decisions/_decisions.md`.

## Authority

Officers only, each inside its own say: a captain on calls inside its Issue contract, the map holder
on scope, destination, architecture and cross-Issue calls. Crew returns its call to its captain; the
chief of staff never runs it. A vote never widens an officer's say.

## When

Research first: a call the facts settle is no Question. DR-grade is the Decision Record test: hard
to reverse, surprising without context, a real trade-off. He is away only while the session records
him away; otherwise he is present.

| The call | He is present | He is away |
|---|---|---|
| Easily reversible | you decide; no skill | you decide; no skill |
| Above your say, below DR-grade | a plain Question; no skill | the tribunal decides; on thin case law, the most reversible option, flagged |
| DR-grade | the tribunal prepares the Question | the tribunal decides and writes the DR |
| Irreversible | his alone; the tribunal prepares the Question | the same |

He may invoke this skill on any call.

## Case law

Only his own words are his precedent: Decision Records (superseded ones as history), the docs, his
answered Questions and grillings, and past conversations where available. A later word outranks an
earlier one. Quote every citation. A tribunal's output is never precedent until he ratifies it.
Search the current project unless he points it at another project's records.

## Tribunal

You and two fresh judges, one Claude and one Codex. You seal your position before they start. Run
it by [tribunal](resources/tribunal.md); judges load this skill and follow the same file.

## Verdict

The final ballot is on one exact, written action.

| Mode | Call | Decided by | Otherwise |
|---|---|---|---|
| Away | DR-grade | a unanimous ballot | no decision: a prepared Question |
| Away | below DR-grade | a majority | a three-way split: the most reversible option, flagged |
| Present | any | never | a prepared Question with the predicted answer and any dissent |

An incomplete tribunal decides nothing; the call becomes a prepared Question.

## Record

Every call is a `Question` Issue in `Todo`, his turn, with the tribunal log attached as a Linear
Document.

- **Decided:** post
  `DECIDED - by: what-would-balazs-do tribunal, unratified; result: <action>; vote: <for-against>; log: <link>; undo: <steps>; next: <action>.`
  Leave the Question unwired; work proceeds. A DR-grade decision also writes the repo DR
  `status: proposed`, `ratification: pending`, linking the log.
- **Prepared or not decided:** wire the Question as a blocker of every waiter.
- **His answer:** post `RATIFIED` or `OVERTURNED` in the standard's form, quoting him, and move the
  Question to `Done`. An overturn reopens the affected work. That prediction-and-answer pair is how
  the skill learns.
