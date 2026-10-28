---
area: reference
type: reference
---

# Linear Workspace Standard

The canonical Linear vocabulary for Projects and tickets. Status records where work is now and which
role is at work; Type records why a ticket exists and who holds it; Mode records how a captain works
with the user; Priority records which of the user's tickets comes next. The model is
supersymmetric: a captain's ticket is a Project one level down, and the same statuses, Types and
chain hold at both levels. The order of the statuses is part of the standard: Linear draws each
status circle from its position in its category.

## Project statuses

| Status | Meaning |
|---|---|
| `Backlog` | A possible Project retained for later; no one has started charting it. |
| `Planning` | The map holder is charting the first low-resolution map with `wayfinder`; a repository plan, when one is written, runs its review loop. |
| `Planned` | The user approved the charted route, and any repository plan passed independent review; the map holder commissions the first Ticket Captain to open the feature. |
| `In Progress` | The map holder is working the map through Ticket Captains toward the destination. |
| `Completed` | The destination landed and the landing's walkthrough found nothing more; commission [spec retirement](./feature-spec-convention.md#retirement). |
| `Canceled` | The destination was consciously abandoned; record why and commission [spec retirement](./feature-spec-convention.md#retirement). |

There are no canonical Project labels: a Project's status and ticket graph carry the information.

## Ticket statuses

One set for primary tickets and Sub-tickets alike, ten statuses in Linear's categories and in this
order. The captain alone sets a delivery ticket's status; the record that runs the chain flips on
every chain spawn, and nothing else flips it.

| Status | Category | Set when |
|---|---|---|
| `FutureFeature` | backlog | An unscheduled strategic possibility with no Type yet. Only the user promotes it. |
| `Backlog` | backlog | Retained with a Type, unscheduled, waiting to become a Todo: no contract yet, or one awaiting the user's go, as an Inquisition's. |
| `Todo` | unstarted | The incoming list: contracted and to be started soon. An open native blocker still prevents pickup. A `Question` in `Todo` is the user's turn. |
| `In Progress` | started | A record not running the chain itself: a parent while its lanes run, a wayfinding ticket, an Inquisition's sweep, proofs and final retention verification. |
| `Implementing` | started | The author of the change's kind is spawned for one hop - a `code-writer`, or a `docs-writer` for a documentation change - a fix hop after a FAIL included. |
| `In Review` | started | Any reviewer is spawned, the optional spec review of the contract before any code included. A FAIL, one or more blockers, returns the record to `Implementing`; a spec review returns it there on either verdict. |
| `Ready to Merge` | started | The PR carries its PASS block and waits for its merge turn. The record stays here while its own Merge Sub-ticket runs; the landing, one Project at a time, and an atomic ticket wait here for the user's click. A merge review FAIL that reverts sends it back to `Implementing`. |
| `Done` | completed | Merged, or the outcome the Type names reached, with its evidence. |
| `Canceled` | canceled | The ticket will not be completed; the record says why. |
| `Duplicate` | canceled | Another ticket owns the outcome; the record links to it. |

`Ready to Merge` holds for a lane into its parent, a primary into the feature branch, and an atomic
ticket or the landing into main; a Merge Sub-ticket never enters it, since it runs the chain and closes. `In Review` on the
board always means a reviewer is running. A ticket with an open native blocker is blocked in any
status; do not add a `Blocked` status that can drift from Linear's dependency graph. Assignment is
the claim: assigned means taken; unassigned, `Todo` and unblocked means pickable.

A `FutureFeature` is promoted by the user alone: at ticket grain it gains a Type and moves to `Backlog`
or `Todo` on the same record; at Project or Initiative grain that record is created and linked and
the FutureFeature is marked `Done`. A rejected one becomes `Canceled` with the reason recorded.

## Ticket labels

Two label groups, `Type` and `Mode`, because one label per group is the rule Linear enforces. Every
ticket carries exactly one Type. Mode sits on every Type a captain holds.

### Type

| Label | Held by | Level | Meaning | Closes on | Colour |
|---|---|---|---|---|---|
| `Feature` | captain | any | Add, improve, refactor or document: an outcome to build. | the outcome merged | `#BB87FC` |
| `Bug` | captain | any | Restore intended behaviour. The record holds the defect and its fix. | the behaviour restored | `#EB5757` |
| `Merge` | captain | any; the landing is the only primary one | One merge operation: lanes into a parent, a primary into the feature, the feature into main. | the merge review PASS | `#4EA7FC` |
| `Enablement` | captain | any | Access, environment, credentials or material other work needs; `wizard` guides the steps only the user can do. | the condition true, with evidence | `#26B5CE` |
| `Inquisition` | captain | primary only | Many read-only eyes on the integrated feature; hypotheses proved, one in code by a proof-only test, one in prose by its quoted passages; Bugs filed. | Bugs filed, record delivered to the retained feature, exact content and merge reachability verified | `#5E6AD2` |
| `Prototype` | captain | any | A design question raised to fidelity the user can react to; fast sketches, the user is the review. | the user's verdict on the ticket | `#F2994A` |
| `Question` | map holder | any | One prepared, discrete question whose answer blocks named work. | the user's answer on the ticket | `#F2C94C` |
| `Research` | map holder | any | A factual answer whose investigation needs its own owner, status or evidence. | cited findings on the ticket | `#95A2B3` |
| `Grilling` | map holder | any | A tree of intent or specification choices resolved with the user. | shared understanding recorded, with its Decision Records linked | `#D4A017` |
| `Walkthrough` | map holder | primary only | The user walks the whole product as it stands: the feature branch at a Milestone, or main at the landing. The emphasis is on what changed since the last walkthrough and what needs the most careful look. The tour also covers where to look, how to try it, and what reviewers left open. There's one per Milestone, prepared even if he skips it, and the landing's comes last. | his verdict quoted on the ticket, or his skip recorded; findings filed as tickets | `#C69C6D` |

A captain-held ticket normally uses one author - `code-writer`, or `docs-writer` for a documentation
change - and review at the working-tree contract's [Delivery scale](../guides/working-tree-contract.md#delivery-scale-and-evidence). A spec review of the contract before any
code needs one short concrete risk reason, recorded in the contract; persistence, migrations, permissions and uncertain native
interfaces are examples of such a risk. Required G/M, integration and release gates remain. The captain records one compact
acceptance contract and points to its evidence. A map-holder-held ticket is run
directly by the map holder whose map it clears; it receives no captain, branch, PR or review
loop. `Task` names the captain-held ticket's role on a map; it is not a label.

Small lookups, conversations and setup stay on the ticket that needs them. Create a map-holder-held
ticket only when the work needs independent tracking or blocks other work.

### Mode

| Label | Meaning | Colour |
|---|---|---|
| `AFK` | The captain reaches reviewed completion without a live conversation. An unexpected choice for the user becomes a blocking `Question`; the parent stays AFK. | `#30A46C` |
| `HITL` | The user and the captain work the ticket together in a session the user opened. | `#F76B15` |

The user's approval or inspection at a universal gate does not make a ticket HITL.

## Priority

Priority is the user's hint for which of his tickets to take next, so that the one unlocking the
most AFK work goes first. The map holder sets it on every ticket that waits on the user, a
`Question` or a HITL ticket, and re-sets it as blockers change.

| Priority | Set when |
|---|---|
| `Urgent` | An emergency; never set by default. |
| `High` | Clearing it lets a top-level ticket, or a Project's next step, run AFK. |
| `Medium` | Clearing it lets a lane run AFK. |
| `Low` | Nothing runs AFK when it clears; the user must clear it anyway. |

AFK tickets carry no priority: the plan order and the native blockers carry their sequence.

## Who holds what

The map holder is the user, or an admiral the user invoked, for a Project, and the captain for a
ticket; the Project description names the Project's current holder. The map holder writes the
contracts one level down, charts with `wayfinder` before dividing, and runs the map-holder-held
tickets that clear its own fog: it dispatches `research`, runs a Grilling or Walkthrough with the user, and
files a `Question` only when judgment remains.

| Type | Normal status path |
|---|---|
| `Research` | `Todo` → `In Progress` → `Done` |
| `Grilling`, `Walkthrough` | `Todo` → `In Progress` → `Done` |
| `Question` | `Todo` → `Done` |
| `Inquisition` | `Backlog` → `Todo`, the user's confirmation → `In Progress`, the captain writes the contract, then the sweep and proofs → released `Todo` → resumed `In Progress` → `Done`; follow the [working-tree contract](../guides/working-tree-contract.md) |
| captain-held default, subject to the Type exceptions | `Todo` → `Implementing` → `In Review` → `Ready to Merge` → `Done`, with `In Progress` while lanes run |
| Merge Sub-ticket | `Todo` → `Implementing` → `In Review` → `Done`; it merges, it is never merged |

A Project's map holder places its Milestones in Linear while charting: few, each a meaningful chunk
of work where a drift from the user's intent would show, the walking skeleton first and the landing
last. Every delivery ticket belongs to one, and each Milestone has one Walkthrough Ticket. Only the
walking skeleton's verdict holds back the work built on it, and not in overnight mode.

A captain creates Sub-tickets one level deep: lanes for separate work that can run at the same time,
each carrying its parent's Type and Mode,
a Merge Sub-ticket for each merge operation except an atomic ticket's own merge into main, which the
user clicks once its PR carries a reviewer PASS and CI is green, and a map-holder-held Sub-ticket for fog that blocks only
its parent or a lane and stays inside the approved Project destination and ticket outcome. A lane that
needs splitting is replaced by sibling lanes. When the answer can change other tickets, a shared
contract, or the Project's destination, scope, acceptance criteria or governing architecture, the
captain prepares the packet and the Project's map holder creates and wires the Project-level ticket. Native blocker
relations connect every waiting record to what it waits on.

The Bug Type template is a narrow exception to parallel delivery lanes: its captain may retain
ordered reproduce-or-identify and fix Sub-tickets, with fix natively blocked by reproduction and
shared paths transferred only after reproduction closes with its evidence recorded. Each stage
keeps the parent's Type and Mode and has its own contract, chain, branch and worktree; every actual
integration has a Merge Sub-ticket. For a simple Bug, collapse the staged placeholders into parent
hops and close the unused records `Canceled` with the reason. Joining acceptance and final review
stay on the parent; the exception creates no overlapping parallel ownership.

## Question Tickets

A Question is the last step of discovery, not its substitute. Before creating one, search the
governing Decision Records, Project plan, specifications, documentation, standards, code, tests and
other authoritative sources; if they settle it, record the answer where the work lives and continue.

Create the Question only when the user's judgment is still needed, in `Todo`, wired as a blocker of everything
that waits. Its body carries:

1. the one question;
2. the named work it blocks and why;
3. the sources searched and facts found;
4. the credible options, trade-offs, and the recommendation when evidence supports one.

If the answer determines which implementation ticket should exist, resolve the Question first.

A Question a `what-would-balazs-do` tribunal decided while the user was away, or on his own
invocation, stays in `Todo` unwired, and work proceeds; he answers it with `RATIFIED` or `OVERTURNED`.

## Ticket templates

One Linear ticket template per Type, named after it, is a workspace-UI convenience. Create it from
the body below where the connected tools or UI permit. Agents read this standard and the ticket
contract, then use the capabilities actually available; template listing or retrieval is not a
required connector capability.

| Template | Body |
|---|---|
| `Feature` | `## Outcome` · `## Owned paths` · `## Blockers` · `## Exact gates` · `## Base branch`, plus optional `## Spec` linking relevant feature-spec sections: the captain's bounded acceptance contract; `## Exact gates` names the command for each test, suite and static gate. |
| `Bug` | the five fields, then `## Observed`, `## Expected`, `## Reproduction` (a scenario at the boundary, else the red test; a prose Bug quotes both contradicting passages at `file:line`; an unused member, export or field quotes its definition at `file:line` and a reference search showing no caller outside the tests); default Sub-tickets: *reproduce or identify*, *fix*. |
| `Merge` | `## Source` and `## Target` at their SHAs, `## Plan order` (the Merge Sub-ticket this one is blocked by), `## Combined gates`, `## Conflicts expected`. |
| `Enablement` | `## Condition` that must become true, `## Steps only the user can do`, `## Evidence` when done. |
| `Inquisition` | `## Scope`: the feature SHA, the parts and the lenses to sweep, the cost; `## Findings`, `## Hypotheses` with their verdicts, `## Bugs filed`, `## Record` path. |
| `Prototype` | `## Question` the sketch must settle, `## Variants`, `## Verdict` and the winning commit on `prototype/<name>`. |
| `Question` | the four items above. |
| `Research` | `## Question` and `## Destination` for the cited report. |
| `Grilling` | `## Subject` (the plan, decision or idea), `## Tree` of choices with their answers and reasoning, `## Records` linked. |
| `Walkthrough` | `## Checkpoint` (the Milestone or the landing, its branch and SHA), the four-part tour, `## Verdict` (his words, or the skip), `## Findings` as linked tickets. |

`## Exact gates` lists its two scales separately: the cheap checks and change-relevant tests that
prove one hop, and the full suites and whole gate set that run at the ticket's final gates, its Merge
Sub-ticket's combined gates and the landing. A `code-writer` hop runs the relevant tests while
building, then the full suite and static gate of each changed stack once before it returns; other
crew briefed for one hop take the first list alone.

## Communication and evidence

Use metadata first: the current active contract and named evidence only. Keep the stable current
contract and state in the description. Read comments only for a named missing fact or binding review;
comments have no assumed order. Filter tool output before it enters model context. Name an agent
without its `@` unless you mean to delegate: an `@mention` starts that agent's session.

Keep existing historical originals accessible. Put new full raw proof and native gate logs once in a
durable, linked Git or document artifact; do not dump them into PRs or comments. Each gate result
names its candidate, command, environment or session, exit and result location. Before expensive tests,
record cheap proof of repository or snapshot, intended selection and nonzero discovery; a quiet healthy
test continues. Reuse exact-candidate evidence only when the gate and environment permit it, rerunning
after relevant changes or new concerns and at mandatory integration or release boundaries. Ticket-and-PR
binding review stays compact but complete. Ordinary updates expose only counts, exits, failure excerpts,
and an evidence link. Do not repeat contracts, hashes, full returns, passing-test name dumps, unchanged
updates, or token, cost, or latency claims. Review gates stay where their workflows require.

Start each post with the applicable form:

- `PASS - rubric/type: <rubric>; contract <ref>; candidate <SHA>; base <SHA>; reviewer/model: <name>; applicable tests <N/N>, gates <N/N>. Checks: <one substantive sentence>. Deferred (optional): 1. Where: <place>; severity: <should|nit>; wrong: <fact>; why: <impact>; required correction: <action>; owner: <role>. Evidence: <link>.`
- `FAIL - rubric/type: <rubric>; contract <ref>; candidate <SHA>; base <SHA>; reviewer/model: <name>; gates <N/N>. 1. Where: <place>; severity: <blocker|should|nit>; wrong: <fact>; why: <impact>; required correction: <action>; owner: <role>. Evidence: <link>.`
- `IMPLEMENTED - hop/candidate <SHA>; <behavior>; proof: <evidence>; blocker: <none or named blocker>.`
- `DECIDED - result: <result>; immutable contract: <contract>; next: <action>.`
- `DECIDED - by: what-would-balazs-do tribunal, unratified; result: <action>; vote: <n of 3, or split; most reversible>; log: <link>; undo: <steps>; next: <action>.`
- `RATIFIED - by: <the user> <date>: "<his words, verbatim>"; record: <DECIDED or DR link>; next: <action>.`
- `OVERTURNED - by: <the user> <date>: "<his words, verbatim>"; record: <link>; reopened: <tickets>; next: <action>.`
- `STATE - <status>; owner: <role>; candidate or PR: <reference>; next or blocker: <fact>; evidence: <link>.`

A review block's `contract <ref>` is the contract its brief pinned, for a ticket
`<KEY> description as of <Linear updatedAt>`; `base <SHA>` is the brief's base. A FAIL carries at
least one blocker; a PASS carries none and lists its should and nit findings under `Deferred`. The
captain files each should as its own ticket and records each nit as one line on the record.

Brevity is soft: ordinary events aim for at most 800 characters, descriptions and state for at most
1,500 characters or 12 lines. A necessary finding, gap, blocker, or evidence may exceed those aims;
omit narration and repetition either way. A writer's successful delivery is never an independent
`PASS`.

### Truthful comment signatures

Every agent-authored Linear comment identifies its acting role and truthful model identity. State the
effective identity when trustworthy runtime information exposes it. Otherwise qualify a family or a
requested/configured value; never infer an alias resolution, parent model, or effort. State effort
only when it is known and useful. A captain relay preserves the original author or reviewer identity
and distinguishes the captain's posting identity. The reviewer field in a reviewer block is its
signature, so do not add a second or conflicting one. Historical comments stay untouched.

Examples:

- `IMPLEMENTED - hop/candidate <SHA>; docs-writer/model: gpt-5.6-terra; proof: <evidence>; blocker: none.`
- `PASS - rubric/type: docs; contract <ref>; candidate <SHA>; base <SHA>; reviewer/model: gpt-5.6-sol; applicable tests <N/N>, gates <N/N>. Checks: <one substantive sentence>. Evidence: <link>. posted by ticket-captain/model: gpt-5.6-terra.`
- `IMPLEMENTED - hop/candidate <SHA>; docs-writer/model: gpt-5.6-luna (later task); proof: <evidence>; blocker: none.`
- `STATE - Implementing; owner: code-writer; requested model: gpt-5.6-terra (effective identity unavailable); next: <fact>; evidence: <link>.`

## Decision Records

Linear records the decision-making work; dydo records a qualifying decision. Link the Question or
Grilling Ticket to its repository Decision Record and link the record back when it has a Linear
origin. The Decision Record is canonical: keep its rationale in dydo rather than copying it into
Linear.

## Related

- [Working-Tree Contract](../guides/working-tree-contract.md) - Branches, hops, review and merge ownership.
- [dydo Glossary](./dydo-glossary.md) - Locked definitions for the Linear-native work model.
