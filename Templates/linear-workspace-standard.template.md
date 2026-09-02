---
area: reference
type: reference
---

# Linear Workspace Standard

The canonical Linear vocabulary for Projects and Issues. Status records where work is now; Type
records why an Issue exists; Mode records how an Issue Captain works with the human on delivery.

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
| `Planning` | A delivery Issue is claimed and its Specifier is making the contract exact and the route mechanical. |
| `In Progress` | Production or wayfinding is actively moving. |
| `Waiting for Human` | Agents have prepared the next human contribution and cannot advance without it. |
| `In Review` | An independent reviewer is gating the current candidate. A FAIL returns the Issue to `In Progress`. |
| `Done` | The Issue outcome and evidence are complete, including review and integration when delivery requires them. |
| `Canceled` | The Issue will not be completed; the record says why. |
| `Duplicate` | Another Issue owns the outcome; the record links to it. |

An Issue with an open native blocker is blocked regardless of status. Do not add a `Blocked` status
that can drift from Linear's dependency graph.

## Issue labels

### Type group

Every Issue carries exactly one Type label. Type records why the Issue exists and selects its control
loop.

#### Intake

| Label | Meaning |
|---|---|
| `FutureFeature` | Preserve an unscheduled strategic possibility whose delivery grain and commitment remain open. |

A FutureFeature stays in `Backlog` without a Mode or Issue Captain. Only the human promotes it. At
Issue grain, replace its Type with `Feature` or `Improvement` and enter the delivery loop on the same
record. At Project or Initiative grain, create and link that record, then mark the FutureFeature
`Done`. A rejected FutureFeature becomes `Canceled` with the reason recorded.

#### Delivery

| Label | Meaning |
|---|---|
| `Feature` | Add a capability. |
| `Improvement` | Improve existing behaviour, structure, documentation, or maintainability. |
| `Bug` | Restore intended behaviour. The same Issue records the defect and owns its fix. |

Every delivery Issue is a Task owned by one Issue Captain and follows the planning, branch/worktree,
production, independent-review, and integration loop. `Task` names its role on the map; its Linear
Type remains `Feature`, `Improvement`, or `Bug`.

#### Wayfinding

| Label | Meaning | Resolution |
|---|---|---|
| `Research` | Establish a factual answer whose investigation needs its own owner, status, blocker, or evidence. | Cited findings recorded on the Issue. |
| `Prototype` | Raise a design question to concrete fidelity so the human can react to it. | Throwaway artifact linked with what it proved. |
| `Grilling` | Resolve a tree of related intent or specification choices with the human. | Shared understanding recorded; resulting specification, glossary entries, and Decision Records linked. |
| `Question` | Ask the human one prepared, discrete question whose answer blocks named work. | The human's answer recorded; a qualifying choice graduates to a linked Decision Record. |
| `Enablement` | Create access, environment, credentials, or representative material required before other work can proceed. | The required condition exists and its evidence is recorded. |

Small lookups, conversations, and setup stay on the Issue that needs them. Create a Wayfinding Issue
only when the work needs independent tracking or blocks other work.

### Mode group

Every Task and Wayfinding Issue carries exactly one Mode label before it becomes pickable:
Research is `AFK`; Prototype, Grilling, and Question are `HITL`; Enablement may be either.
FutureFeatures carry neither.

| Label | Meaning |
|---|---|
| `AFK` | The Issue Captain is expected to reach reviewed completion without an ongoing conversation with the human. An unexpected human choice becomes a blocking Question Issue; the parent remains AFK. |
| `HITL` | The human and Issue Captain work the Issue together. The Captain turns the human's direction into crew work and brings the result back for another turn. |

Human approval or inspection at a universal gate does not make an Issue HITL. `Needs human` is not a
canonical label: `HITL` describes the Issue's operating mode, while `Waiting for Human` says that the
human owns the next action now.

## Wayfinding ownership

The current map owner controls Wayfinding Issues directly: the Project Planner while charting the
first approved map, then the Admiral during delivery. They dispatch Research agents, run Prototype or
Grilling work with the human, present Questions, and route Enablement to whoever can satisfy it. A
Wayfinding Issue does not receive an Issue Captain or the delivery branch, worktree, PR, and review
loop. If it reveals production work, create a delivery Issue for that outcome.

| Type | Normal status path |
|---|---|
| `Research` | `Todo` → `In Progress` → `Done` |
| `Prototype` | `Todo` → `In Progress` ↔ `Waiting for Human` → `Done` |
| `Grilling` | `Todo` → `In Progress` ↔ `Waiting for Human` → `Done` |
| `Question` | `Waiting for Human` → `Done` |
| `Enablement` | `Todo` → `In Progress` or `Waiting for Human` → `Done` |

`Planning` and `In Review` belong to the delivery loop; Wayfinding closes when its recorded
resolution is true.

An Issue Captain may create direct Wayfinding Sub-issues when newly visible fog blocks only its parent
delivery Issue or one of its lanes and remains inside the approved Project destination and Issue
outcome. Even when a lane is blocked, create the Wayfinding record directly under the delivery parent.
The Captain owns that local map, persists every result on its Sub-issue, and informs the Admiral.
Escalate the packet instead when the answer can change other Issues, a shared contract, or the
Project's destination, scope, acceptance criteria, or governing architecture; the Admiral then creates
and wires a Project-level Wayfinding Issue.

Wayfinding Sub-issues stay one level deep. New local investigations become siblings under the same
delivery parent; Project-wide discoveries return to the Admiral. Native blocker relations connect
each Wayfinding Issue to everything waiting on its resolution.

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
When a new question surfaces during execution, the Issue Captain creates it locally only under the
scope rule above; otherwise the Captain prepares the hand-raise and the Admiral creates and wires the
Project-level Question Issue.

## Decision Records

Linear records the decision-making work; dydo records a qualifying decision. Link the Question or
Grilling Issue to its repository Decision Record and link the record back when it has a Linear origin.
The Decision Record is canonical: keep its rationale in dydo rather than copying it into Linear.

## Related

- [Linear Issue Lifecycle](../understand/task-lifecycle.md) — How Issues move through planning,
  execution, review, and escalation.
- [dydo Glossary](./dydo-glossary.md) — Locked definitions for the Linear-native work model.
- [DR 045](../project/decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md) — The
  governing flow map, question model, and human gates.
