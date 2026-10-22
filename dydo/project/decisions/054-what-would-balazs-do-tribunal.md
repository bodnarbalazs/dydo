---
area: project
type: decision
status: accepted
date: 2026-10-22
---

# 054 - What Would Balazs Do: a Tribunal for His Calls

## Context

Officers stall on calls only the human may make, and while he is away the work waits for him.
[DYD-350](https://linear.app/bodnar-balazs/issue/DYD-350) grilled him on 2026-10-21 and 2026-10-22 on
letting agents make or prepare those calls the way he would.

## Decision

A model-invoked skill, `what-would-balazs-do`, personal by design; an adopter renames it. An officer
runs it inside its own say. After research, the call's class decides the route: easily reversible,
the officer decides; above its say but below DR-grade, a plain Question when he is present and a
tribunal decision when he is away; DR-grade, a tribunal prepares the Question when he is present and
decides it when he is away; irreversible, he alone decides and the tribunal only prepares.

The tribunal is always three: the raising officer, sealed before dispatch, and two fresh judges, one
Claude and one Codex, on the strongest model. Rounds run through the officer as a mechanical clerk;
claims are checked by the other two members; at most 4 rounds; one retry for a missing member, then
no decision. Away, a DR-grade call needs a unanimous ballot and a lower one a majority; present, the
result is always a prepared Question carrying the predicted answer.

Only his own words are precedent. Every tribunal output is unratified until he answers it
`RATIFIED` or `OVERTURNED`; a DR-grade decision is written `proposed` with `ratification: pending`.

## Consequences

Work continues while he is away, at the cost of decisions he may overturn; an overturn reopens the
affected work. Each call is a `Question` Issue with a Linear Document log, so the prediction and his
answer form the self-improvement record and no separate ledger exists. A decided Question stays in
`Todo` unwired. Whether he is away is a separate switch, not set here.

## Related

- [Linear Workspace Standard](../../reference/linear-workspace-standard.md)
- [Control Flow](../../understand/control-flow.md)
