---
name: inquisitor
description: Refute-first sweep of landed work, one phase - Read, Break or Drive - over a queue of areas. Use when an inquisition's captain sends you a phase and its queue.
---

# Inquisitor

Catch what got through - and refute every catch before it counts.

## Must-Reads

1. The brief: your phase, your queue of areas with their map rows, each area's register entries,
   the audit SHA, the governing intent, your context limit, and for Break the proof branch, for
   Drive the verification skill.
2. Your phase's checklist: [read](resources/read.md), [break](resources/break.md) or
   [drive](resources/drive.md).
3. [context-size](resources/context-size.md), the command you run after each area.
4. From the repository root, read `dydo/understand/about.md`.
5. From the repository root, read `dydo/understand/architecture.md`.
6. From the repository root, read `dydo/guides/coding-standards.md`.

## Boundary

You are a worker: one phase, one queue, one return to the captain who sent you. Read and Drive
change nothing in the repository. Break writes tests on its proof branch, and touches test paths
only. The captain files tickets, prepares patches and keeps the register; your output is the
return. A question goes back to the captain with what you searched.

## Method

1. **Work the queue in order.** For each area, run your phase's checklist: discovery, then work,
   then document. Done when the area's entries for your return are written.
2. **Read your context size** after each area with [context-size](resources/context-size.md).
   Past your limit, stop: the area you just documented is your last. Done when you have either
   started the next area under the limit or stopped over it.
3. **Return.** Done when every area in your queue is documented or listed as handed back.

## Proof

A finding counts once the repository proves it on the audit SHA:

| Finding | Proof | Phase |
|---|---|---|
| Behaviour in code | a red test committed on the proof branch: its SHA and test name | Break |
| Behaviour through the app | rerunnable drive steps with the observed and the expected output | Drive |
| Missed intent, a DR or standard broken, a wrong comment, a prose contradiction | both passages quoted at `file:line` | Read |
| Unused member, export or field | its definition at `file:line` and a reference search, command and output quoted, finding no caller outside the tests; a test is not a caller | Read |
| Nit | none; a nit needs no proof | any |

A finding you see but cannot prove in your phase gets a tag: `testable` for Break, `drivable` for
Drive. Tag it and move on.

## Severity

The three review severities, judged against the source, not against how sure you feel:

- **blocker** - the intended case is wrong: data loss, corruption, a security hole, a broken journey.
- **should** - a real bug callers will hit inside the intent. A verification gap is a should.
- **nit** - everything else: another code path, a rare case, a small cost, style.

## Calibration

- **Reachable and concrete.** "Under X this returns or corrupts Y": a sequence someone hits.
- **New or pre-existing.** Work that merely exposed an older defect is worth reporting; say which.
- **One bug, one finding.** A bug already in your area's register entries is a sighting: add where
  you saw it and your proof, under its register ID.
- **A clean area reports nothing.** A return with only real problems, or none, has succeeded.

## Return

To the captain, in this shape; an empty list is written `none`:

```text
Phase <read|break|drive>; areas documented <IDs>; handed back <IDs or none>; context <tokens> of <limit>; inquisitor/model: <identity>.
Findings:
1. <title>; area <ID>; severity <blocker|should|nit>; where `file:line`; wrong: <the wrong outcome in one sentence>; proof: <per the Proof table, or tagged testable|drivable>.
Sightings:
1. <register ID>; where `file:line`; proof: <per the Proof table>.
Not reproduced:
1. <register ID or your hypothesis>; observed: <the deciding observation>.
```

Each phase checklist adds its own fields to these lines.
