---
area: reference
type: reference
---

# Linear Workspace Standard

The canonical Linear vocabulary for Projects and Issues. Status records where work is now; labels
record what an Issue is and, for implementation work, how its Issue Captain works with the human.

## Project statuses

| Status | Meaning |
|---|---|
| `Backlog` | A possible Project retained for later; planning has not been commissioned. |
| `Planning` | The Project Planner is charting and reviewing the first low-resolution map. |
| `Planned` | The Project plan passed independent review and human approval; an Admiral may start delivery. |
| `In Progress` | The Admiral is working the map and coordinating Issue Captains toward the destination. |
| `Completed` | The destination landed and the Project's required closeout is complete. |
| `Canceled` | The destination was consciously abandoned; the Project records why. |

## Project labels

There are no canonical Project labels. A Project can contain mixed Issue types and operating modes;
its status and Issue graph carry that information without a second taxonomy.

## Issue statuses

| Status | Meaning |
|---|---|
| `Backlog` | The Issue is retained but not yet contracted for execution. |
| `Todo` | The Issue contract is ready and queued; open native blockers still prevent pickup. |
| `Planning` | The Issue Captain has claimed the Issue and its Issue Planner is making the route mechanical. |
| `In Progress` | The Issue Captain is directing production or actively working with the human. |
| `Waiting for Human` | Agents have prepared the next human contribution and cannot advance without it. Question Issues normally enter here. |
| `In Review` | An independent reviewer is gating the current candidate. A FAIL returns the Issue to `In Progress`. |
| `Done` | The Issue outcome, required review, integration, and evidence are complete. |
| `Canceled` | The Issue will not be completed; the record says why. |
| `Duplicate` | Another Issue owns the outcome; the record links to it. |

An Issue with an open native blocker is blocked regardless of status. Do not add a `Blocked` status
that can drift from Linear's dependency graph.

## Issue labels

### Type group

Every Issue carries exactly one Type label. Type records why the Issue exists and normally remains
stable throughout its life.

| Label | Meaning |
|---|---|
| `Feature` | Add a capability. |
| `Improvement` | Improve existing behaviour, structure, documentation, or maintainability. |
| `Bug` | Restore intended behaviour. The same Issue records the defect and owns its fix. |
| `Question` | Ask the human one prepared question whose answer blocks named work. |

### Mode group

Every non-Question Issue carries exactly one Mode label before it becomes pickable. Question Issues
carry neither: their operating mode is always a prepared exchange with the human.

| Label | Meaning |
|---|---|
| `AFK` | The Issue Captain is expected to reach reviewed completion without an ongoing conversation with the human. An unexpected human choice becomes a blocking Question Issue; the parent remains AFK. |
| `HITL` | The human and Issue Captain work the Issue together. The Captain turns the human's direction into crew work and brings the result back for another turn. |

Human approval or inspection at a universal gate does not make an Issue HITL. `Needs human` is not a
canonical label: `HITL` describes the Issue's operating mode, while `Waiting for Human` says that the
human owns the next action now.

## Question Issues

A Question Issue is the last step of discovery, not its substitute. Before creating one, search the
governing Decision Records, Project plan, specifications, documentation, standards, code, tests, and
other authoritative sources. If those sources settle the answer, record it where the work already
lives and continue.

Create the Question only when human judgment remains. Its body carries:

1. the one question;
2. the named work it blocks and why;
3. the sources searched and facts found;
4. the credible options, trade-offs, and recommendation when evidence supports one.

If the answer determines what implementation Issue should exist, resolve the Question first. If the
implementation outcome is already stable, create both records and make the Question a native blocker.
When a new question surfaces during execution, the Issue Captain prepares the hand-raise and the
Admiral creates and wires the Question Issue.

## Related

- [Linear Issue Lifecycle](../understand/task-lifecycle.md) — How Issues move through planning,
  execution, review, and escalation.
- [dydo Glossary](./dydo-glossary.md) — Locked definitions for the Linear-native work model.
- [DR 045](../project/decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md) — The
  governing flow map, question model, and human gates.
