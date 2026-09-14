---
area: reference
type: reference
---

# Linear Workspace Standard

The canonical Linear vocabulary for Projects and Issues. Status records where work is now and which
role is at work; Type records why an Issue exists and who holds it; Mode records how a captain works
with the human; Priority records which of the human's Issues comes next. The model is
supersymmetric: a captain's Issue is a Project one level down, and the same statuses, Types and
chain hold at both levels. The order of the statuses is part of the standard: Linear draws each
status circle from its position in its category.

## Project statuses

| Status | Meaning |
|---|---|
| `Backlog` | A possible Project retained for later; no admiral has taken it. |
| `Planning` | The admiral's project-planner is charting the first low-resolution map, and the review loop runs. |
| `Planned` | The plan passed independent review and human approval; the admiral opens the feature. |
| `In Progress` | The admiral is working the map through Issue Captains toward the destination. |
| `Completed` | The destination landed and a walkthrough found nothing more. |
| `Canceled` | The destination was consciously abandoned; the Project records why. |

There are no canonical Project labels: a Project's status and Issue graph carry the information.

## Issue statuses

One set for primary Issues and Sub-issues alike, twelve statuses in Linear's categories and in this
order. The captain alone sets a delivery Issue's status; the record that runs the chain flips on
every chain spawn, and nothing else flips it.

| Status | Category | Set when |
|---|---|---|
| `FutureFeature` | backlog | An unscheduled strategic possibility with no Type yet. Only the human promotes it. |
| `Backlog` | backlog | Retained with a Type, unscheduled, waiting to become a Todo: no contract yet, or one awaiting the human's go, as an Inquisition's. |
| `Todo` | unstarted | The incoming list: contracted and to be started soon. An open native blocker still prevents pickup. A `Question` in `Todo` is the human's turn. |
| `Specifying` | started | The specifier is spawned. |
| `In Progress` | started | A record not running the chain itself: a parent while its lanes run, a wayfinding Issue, an Inquisition's sweep and proofs. |
| `Implementing` | started | The implementer is spawned, a fix hop after a FAIL included. |
| `Hardening` | started | The hardener is spawned. |
| `In Review` | started | Any reviewer is spawned, spec review included. A FAIL returns the record to the hop that fixes it. |
| `Ready to Merge` | started | The PR carries its PASS block and waits for its merge turn. The record stays here while its own Merge Sub-issue runs; the landing waits here for the human's click, one Project at a time. A merge review FAIL that reverts sends it back to `Implementing`. |
| `Done` | completed | Merged, or the outcome the Type names reached, with its evidence. |
| `Canceled` | canceled | The Issue will not be completed; the record says why. |
| `Duplicate` | canceled | Another Issue owns the outcome; the record links to it. |

`Ready to Merge` holds for a lane into its parent, a primary into the feature branch and the landing
into main; a Merge Sub-issue never enters it, since it runs the chain and closes. `In Review` on the
board always means a reviewer is running. An Issue with an open native blocker is blocked in any
status; do not add a `Blocked` status that can drift from Linear's dependency graph. Assignment is
the claim: assigned means taken; unassigned, `Todo` and unblocked means pickable.

A `FutureFeature` is promoted by the human alone: at Issue grain it gains a Type and moves to `Backlog`
or `Todo` on the same record; at Project or Initiative grain that record is created and linked and
the FutureFeature is marked `Done`. A rejected one becomes `Canceled` with the reason recorded.

## Issue labels

Two label groups, `Type` and `Mode`, because one label per group is the rule Linear enforces. Every
Issue carries exactly one Type. Mode sits on every Type a captain holds.

### Type

| Label | Held by | Level | Meaning | Closes on | Colour |
|---|---|---|---|---|---|
| `Feature` | captain | any | Add, improve, refactor or document: an outcome to build. | the outcome merged | `#BB87FC` |
| `Bug` | captain | any | Restore intended behaviour. The record holds the defect and its fix. | the behaviour restored | `#EB5757` |
| `Merge` | captain | any; the landing is the only primary one | One merge operation: lanes into a parent, a primary into the feature, the feature into main. | the merge review PASS | `#4EA7FC` |
| `Enablement` | captain | any | Access, environment, credentials or material other work needs; `wizard` guides the steps only the human can do. | the condition true, with evidence | `#26B5CE` |
| `Inquisition` | captain | primary only | Many read-only eyes on the integrated feature; hypotheses turned into tests; Bugs filed. | the Bugs filed and the record written | `#5E6AD2` |
| `Prototype` | captain | any | A design question raised to fidelity the human can react to; fast sketches, the human is the review. | the human's verdict on the Issue | `#F2994A` |
| `Question` | map holder | any | One prepared, discrete question whose answer blocks named work. | the human's answer on the Issue | `#F2C94C` |
| `Research` | map holder | any | A factual answer whose investigation needs its own owner, status or evidence. | cited findings on the Issue | `#95A2B3` |
| `Grilling` | map holder | any | A tree of intent or specification choices resolved with the human. | shared understanding recorded, with its Decision Records linked | `#D4A017` |
| `Walkthrough` | map holder | primary only | The human inspects what landed: what changed, where to look, how to try it, what reviewers flagged. | the human has walked it; findings filed as Issues | `#C69C6D` |

A captain-held Issue runs the chain [specifier] → [implementer] → [hardener] → [reviewer] on its own
record or on its lanes; the captain decides, through its spec, which hops are empty. A map-holder-held Issue is run
directly by the admiral or captain whose map it clears; it receives no captain, branch, PR or review
loop. `Task` names the captain-held Issue's role on a map; it is not a label.

Small lookups, conversations and setup stay on the Issue that needs them. Create a map-holder-held
Issue only when the work needs independent tracking or blocks other work.

### Mode

| Label | Meaning | Colour |
|---|---|---|
| `AFK` | The captain reaches reviewed completion without a live conversation. An unexpected human choice becomes a blocking `Question`; the parent stays AFK. | `#30A46C` |
| `HITL` | The human and the captain work the Issue together in a session the human opened. | `#F76B15` |

Human approval or inspection at a universal gate does not make an Issue HITL.

## Priority

Priority is the human's hint for which of his Issues to take next, so that the one unlocking the
most AFK work goes first. The map holder sets it on every Issue that waits on the human, a
`Question` or a HITL Issue, and re-sets it as blockers change.

| Priority | Set when |
|---|---|
| `Urgent` | An emergency; never set by default. |
| `High` | Clearing it lets a top-level Issue, or a Project's next step, run AFK. |
| `Medium` | Clearing it lets a lane run AFK. |
| `Low` | Nothing runs AFK when it clears; the human must clear it anyway. |

AFK Issues carry no priority: the plan order and the native blockers carry their sequence.

## Who holds what

The map holder is the admiral for a Project and the captain for an Issue. The map holder writes the
contracts one level down, sends a planner ahead before dividing, and runs the map-holder-held Issues
that clear its own fog: it dispatches `research`, runs a Grilling or Walkthrough with the human, and
files a `Question` only when judgment remains.

| Type | Normal status path |
|---|---|
| `Research` | `Todo` → `In Progress` → `Done` |
| `Grilling`, `Walkthrough` | `Todo` → `In Progress` → `Done` |
| `Question` | `Todo` → `Done` |
| `Inquisition` | `Backlog` → `Todo`, the human's confirmation → `Specifying` → `In Progress`, the sweep and the proofs → `Done` |
| captain-held | `Todo` → `Specifying` → `Implementing` → `Hardening` → `In Review` → `Ready to Merge` → `Done`, with `In Progress` while lanes run |
| Merge Sub-issue | `Todo` → `Specifying` → `Implementing` → `In Review` → `Done`; it merges, it is never merged |

A captain creates Sub-issues one level deep: lanes for separate work that can run at the same time,
each carrying its parent's Type and Mode,
a Merge Sub-issue for each merge operation, and a map-holder-held Sub-issue for fog that blocks only
its parent or a lane and stays inside the approved Project destination and Issue outcome. A lane that
needs splitting is replaced by sibling lanes. When the answer can change other Issues, a shared
contract, or the Project's destination, scope, acceptance criteria or governing architecture, the
captain prepares the packet and the admiral creates and wires the Project-level Issue. Native blocker
relations connect every waiting record to what it waits on.

## Question Issues

A Question is the last step of discovery, not its substitute. Before creating one, search the
governing Decision Records, Project plan, specifications, documentation, standards, code, tests and
other authoritative sources; if they settle it, record the answer where the work lives and continue.

Create the Question only when human judgment remains, in `Todo`, wired as a blocker of everything
that waits. Its body carries:

1. the one question;
2. the named work it blocks and why;
3. the sources searched and facts found;
4. the credible options, trade-offs, and the recommendation when evidence supports one.

If the answer determines which implementation Issue should exist, resolve the Question first.

## Issue templates

One Linear Issue template per Type, named after it. The human creates them from the bodies below; an
agent lists and reads them over MCP and fills them in.

| Template | Body |
|---|---|
| `Feature` | `## Outcome` · `## Owned paths` · `## Blockers` · `## Exact gates` · `## Base branch`; the specifier adds `## Spec` and `## Plan`. |
| `Bug` | the five fields, then `## Observed`, `## Expected`, `## Reproduction` (a scenario at the boundary, else the red test); default Sub-issues: *reproduce or identify*, *fix*. |
| `Merge` | `## Source` and `## Target` at their SHAs, `## Plan order` (the Merge Sub-issue this one is blocked by), `## Combined gates`, `## Conflicts expected`. |
| `Enablement` | `## Condition` that must become true, `## Steps only the human can do`, `## Evidence` when done. |
| `Inquisition` | `## Scope`: the feature SHA, the parts and the lenses to sweep, the cost; `## Findings`, `## Hypotheses` with their verdicts, `## Bugs filed`, `## Record` path. |
| `Prototype` | `## Question` the sketch must settle, `## Variants`, `## Verdict` and the winning commit on `prototype/<name>`. |
| `Question` | the four items above. |
| `Research` | `## Question` and `## Destination` for the cited report. |
| `Grilling` | `## Subject` (the plan, decision or idea), `## Tree` of choices with their answers and reasoning, `## Records` linked. |
| `Walkthrough` | `## What landed` (branch, SHA, final PASS), the four-part tour, `## Findings` as linked Issues. |

## Decision Records

Linear records the decision-making work; dydo records a qualifying decision. Link the Question or
Grilling Issue to its repository Decision Record and link the record back when it has a Linear
origin. The Decision Record is canonical: keep its rationale in dydo rather than copying it into
Linear.

## Related

- [Working-Tree Contract](../guides/working-tree-contract.md) — Branches, hops, review and merge ownership.
- [dydo Glossary](./dydo-glossary.md) — Locked definitions for the Linear-native work model.
