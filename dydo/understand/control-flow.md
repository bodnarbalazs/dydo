---
area: understand
type: concept
---

# Control Flow

Every handoff in the dydo 3 operating model, drawn from [DR 045](../project/decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md),
[DR 046](../project/decisions/046-executable-specifications-specifier-and-commit-addressed-hops.md),
[DR 047](../project/decisions/047-supersymmetry-hop-statuses-merge-issues-and-the-release-protocol.md),
[DR 050](../project/decisions/050-officers-crew-and-skills-hats-retired.md),
[DR 051](../project/decisions/051-captains-by-default-admiral-on-invocation.md)
and the [Linear Workspace Standard](../reference/linear-workspace-standard.md): who acts, on which
branch, what leaves them and through which channel, what the other side reads, and what happens when
the happy path breaks. It is the shared truth both sides of every contact must agree with. Section 7
shows a working day with three Projects in flight; section 8 lists what each skill must change to
match.

## How to read it

Node colours name the kind of actor; edge labels name the payload. The payload's channel and the
fields on both sides are in the edge table, section 5.

| Colour | Kind | Meaning |
|---|---|---|
| gold | user | the one person; opens every session that talks to him, acts at the gates |
| blue | officer | holds a Project, a ticket or the board and never switches; the map holder at its level |
| grey | session | a session without an officer role, loading a skill such as co-thinker |
| green | crew | spawned for one bounded job, returns to its spawner, never talks to the user |
| red | reviewer | a crew member whose return is the review block: one rubric, one binding verdict |

Channels: **L** a Linear field, comment or status · **G** a commit, branch or PR · **R** an agent's
return to its spawner, or a message to a returned one · **F** a file in the repository · **C** the
conversation with the user.

A Project's **map holder** is the user, in their own session, unless the user has invoked an
admiral. Where the flows below name the admiral, they draw an invoked one; without it, the user
takes that step, and nobody needs telling.

## 1. Roster

The actors: every officer and every crew role, plus the co-thinker skill any session loads to think
with the user, keeping its role. The ticket-captain is the default officer; an admiral exists only when
the user invokes one, as a right hand for throughput that never takes the frontline. An officer is
top-level when a session is opened with it; a captain is also spawned as an agent, so the user's
session or an invoked admiral can direct one per ticket.

| Role | Kind | Runs as | Invoked by | Works in | Does | Returns to |
|---|---|---|---|---|---|---|
| user | user | the terminal | - | `main`, where his own commits need no ticket, and any session | thinks, files Projects, holds a Project's map by default and charts it with `wayfinder`, approves plans, answers Questions, confirms inquisitions, clicks the landing one Project at a time and an atomic ticket's reviewed PR, walks through; tells an invoked admiral after each of his board moves, and takes the map back at any time | - |
| co-thinker | skill | any session, keeping its role | any session with an unripe idea | no branch; DRs and glossary on the current branch | homework, grilling, domain-modeling, recommendation; a DR when the ADR test passes; an atomic ticket with its five fields, Type and Mode | a DR (F), a Project through `to-project` (L), an atomic ticket (L) |
| admiral | officer, optional | top-level session, explicit-only | the user, only when he wants a right hand for throughput, on a Project at any stage | `feature/<slug>` once the map holder opens one at Open; none before that, or on a Project with no feature branch | holds the map until the user takes it back; wakes on a captain's return or the user's word, reads the Project and acts: charts it with `wayfinder`, writes a plan only for a cross-cutting architecture contract and owns its review, puts approval to the user, commissions the first captain to open the feature, commissions captains, wires the merge order and re-wires it when a later PR is ready first, sets priority on what waits on the user, runs its wayfinding with the user, proposes the inquisition, files the landing and the walkthrough, closes | the user in its own session (C); the board (L) |
| ticket-captain | officer, the default, also agent | top-level for an atomic or HITL ticket; spawned by the user's session or an invoked admiral for an AFK one | the user's session, or an invoked admiral | `DYD-123-<slug>` in an isolated worktree; `inquisition/<slug>` for an inquisition | claims, takes an adjacent ticket only on the user's word, sets the status at every chain spawn, directs [code-writer] → [reviewer] on the parent or each lane and adds a spec review only on a risk its contract records, divides when the writer names lanes, sets `Ready to Merge` when the PR carries its PASS, runs its Merge Sub-tickets or, on an atomic ticket, waits for the user's click, cleans up | the spawner: `done <key>` or `released <key>: <reason>` (R); everything else on the record (L) |
| chief-of-staff | officer | top-level session, explicit-only | the user | none | the bird's-eye view over the Projects in flight: the three lists, grilling open Questions, mediating collisions, sweeping stale state and orphans | the user (C); delivery staged on its Project for the map holder (L) |
| research | crew, delegates, web | agent | co-thinker, the map holder, ticket-captain | reads | one fact a choice waits on, cited; sends scouts | the invoker: one-line answer, destination, unsettled points (R); report as ticket comment (L) or scratch file (F) |
| code-writer | crew | agent | ticket-captain, on the parent first, then per lane | the ticket branch; a Merge Sub-ticket; a proof branch off the brief's pinned source SHA (the audit SHA for an Inquisition) | one ticket's contract on the `implement` hop, test-driven by Type: an unused-member Bug its definition and a reference search, any other Bug red before its fix and red again with the fix reverted; a Feature's acceptance lines as named tests written with the code, proved to fail with the code stashed; the full suite and static gate of each changed stack once, HCRAP and cognitive complexity fixed in changed methods. Posts nothing before code unless it names lanes or an inexact contract. On a Merge: the merge commit and its resolutions. Proof-only: the test that would catch one hypothesis, committed as `<KEY> proof: <hypothesis>` | the captain: the `IMPLEMENTED` line with hop SHA, behaviour, red proof, gates run and blocker (R); for a hypothesis, `confirmed`, `not reproduced` or `inconclusive` |
| docs-writer | crew | agent | ticket-captain, including the separate record Feature's captain for an inquisition | the delivery ticket branch | one documentation change with a witness per claim; the record Feature preserves the pinned inquisition packet | the delivery captain: ending SHA, exact record path/digest, files, witnesses, `dydo check` (R) |
| reviewer | crew, non-authoring | agent | the map holder, ticket-captain | reads a pinned candidate | one rubric: code, docs, project-plan, spec, merge | the invoker: the review block (R), posted on the record and in the PR body (L, G) |
| scout | crew, non-authoring, web | agent | research | reads one source family | passages back, no conclusions | research (R) |
| inquisitor | crew, non-authoring | agent | an inquisition's ticket-captain | reads the inquisition branch | one part or one lens swept, refuting its own catch; hypotheses of what could go wrong | the inquisition captain: findings with proof, hypotheses (R) |

For these three roles, non-authoring names the commission and method. It does not assert filesystem
enforcement by the host: a native sandbox setting is a request until a live host check proves the
restriction. Fresh commission, an isolated pinned candidate and independent review are the portable
floor.

## 2. Skills that are not actors

Methods run inside the caller's thread and leave nothing of their own; some load only when the
user asks, and user commands run only when the user types them.

Agent-invoked methods:

| Method | Reached by | When | Leaves behind |
|---|---|---|---|
| wayfinder | the map holder: the user's session, an invoked admiral, or a ticket-captain | charting, writing the contracts one level down, working the map, clearing local fog | the map in the Project description; tickets and Sub-tickets, wired |
| grilling | co-thinker, chief-of-staff, any Grilling Ticket | a plan, decision or idea the user wants stress-tested, one round at a time | answers and reasoning recorded where the work lives |
| domain-modeling | co-thinker, wayfinder | a term keeps sliding, or a choice looks durable enough for a DR | `dydo/glossary.md` entries; a Decision Record |
| codebase-design | code-writer, reviewer | shaping a module or interface, choosing a seam, judging depth | vocabulary applied, nothing written |
| diagnosing-bugs | the code-writer reproducing a Bug, on the parent or its retained reproduce-or-identify Sub-ticket | a Bug other than an unused member without a red reproduction | a tight loop that goes red; the regression test; the cause on the ticket |
| prototype | the code-writer on a Prototype Ticket | how it should look or behave is the open question | `prototype/<name>`, never merged; kept and linked from the ticket until the delivery ticket is `Done`, read by the delivery ticket's code-writer as the template for a fresh rewrite, never a base or a copy (DR 047 §5) |
| wizard | the captain on an Enablement Ticket | steps only the user can perform: credentials, dashboards, cutovers | a bash wizard that walks him through them |
| writing-for-agents | anyone editing a skill or a document an agent reaches by pointer; reviewer(docs) | a prompt file is created, edited, or fires wrong | the edited file |
| retro | any session; chief-of-staff routes recurring friction to it | the same friction or workaround returns a second time | one small, testable harness-change suggestion, with proof and rollback |
| maintain-verification-skill | any agent driving the app through a project's verification skill; the map holder before a landing | a drive contradicts the feature map, a touched area has no map file, or the whole map needs an audit | `clean`, `changed` with one PR of proven map or harness corrections, or `blocked`; product regressions reported as Bugs |
| maintain-formal-verification-skill | any agent or the map holder after merges, refactors or edits touched verified code without rechecking the verification | verified code changed since the verification's sync commit, or a verify skill's check passes on code that has since moved | each check `in sync`, `updated`, `bug` or `blocked`, with drift and code bugs told apart and verification updates kept in a change of their own |

Methods loaded only when the user asks:

| Method | The user asks when | Produces |
|---|---|---|
| to-project | a co-think is ripe and belongs in Linear | a Project in `Backlog`: title, summary, the intent as description, links to the DR, the glossary entries and the source FutureFeature when one exists; no tickets |
| to-ticket | a plan, spec or conversation is ready to become tickets | pickable tracer-bullet tickets in `Todo`, each with one Type, a Mode where a captain holds the Type, and the five fields or its Type's own template, wired with native blocking relations |
| handoff | the session is ending and another agent continues; also loaded by an officer briefing an agent it dispatches | a handoff document in the scratch directory |
| walkthrough | a Walkthrough Ticket is open; also loaded at the map holder's landing step | the four-part tour: what changed, where to look, how to try it, what reviewers flagged |
| teach | the user wants to learn a topic in the workspace | a mission and its learning records |

User commands:

| Command | The user types it when | Produces |
|---|---|---|
| grill-me | a plan or idea of theirs should be pressed | answers and reasoning, recorded by the session in play |
| bro | an agent's pitch did not land | the same thing said plainly, with the two glossaries in hand |
| improve-codebase-architecture | the codebase's architecture should be examined | a grilled candidate with its report |
| create-verification-skill | the project has no scripted way to drive the app as a user | a proven `verify-<app>` skill, its control CLI and a seeded feature map |
| create-formal-verification-skill | code where a bug costs the most and tests reach the least needs more than tests: concurrency, retries, state machines, parsers, unsafe code | a project-local skill that checks a change against one verified property, proven to catch a scratch break |
| update-dydo | the project should move to the latest dydo release | the project on the new release, its tweaks kept, the update proven and adopted |

The explicit-only officers admiral and chief-of-staff are also typed by the user; they are actors and
stand in the roster.

## 3. The happy path

The model is supersymmetric: a captain's ticket is a Project one level down. The same Types, the same
statuses and the same chain hold at both levels; only the map holder changes.

Capability rides with the delegation, not with the role. On every edge below, the delegating role
picks the model - and the effort where the host exposes one - for the crew member it is about to spawn,
weighing what that one task is worth. Nothing dydo compiles makes the choice for it;
[Configuration](../reference/configuration.md) carries each host's resolution order and what its
telemetry can and cannot prove afterwards.

### 3a. One Project

```mermaid
flowchart TD
  classDef human fill:#f6d365,stroke:#8a6d00,color:#000
  classDef session fill:#e9ecef,stroke:#6c757d,color:#000
  classDef officer fill:#cfe2ff,stroke:#2c5aa0,color:#000
  classDef crew fill:#d4edda,stroke:#2e7d32,color:#000
  classDef reviewer fill:#f8d7da,stroke:#a71d2a,color:#000

  H0([user: an idea]):::human --> CT[co-thinker]:::session
  CT -->|ripe| TP([user: to-project, Project in Backlog]):::human
  TP --> AD[map holder: the user, or an admiral the user invoked; reads the Project]:::officer
  AD -->|Project Planning, wayfinder| MAP[the map in the Project description, first tickets, Questions]:::officer
  MAP -.->|only a cross-cutting architecture contract: plan commit| RP{{reviewer: project-plan, two rounds at most}}:::reviewer
  MAP -->|route charted| H1([user approves, Project Planned]):::human
  RP -->|PASS| H1
  H1 --> OP[map holder commissions first captain to open feature; wires merge order]:::officer
  OP <-->|"one captain per pickable AFK ticket, the loop in 3b: commission · done &lt;key&gt;: PR ready · merge, when its turn comes · done &lt;key&gt;: merged"| IC[ticket-captain]:::officer
  OP -->|all landed: Inquisition Ticket in Backlog, scope and cost| H2([user moves it to Todo and tells an invoked admiral, or cancels]):::human
  H2 -->|the map holder commissions| IQ[ticket-captain of the inquisition, 6.6: Bugs filed]:::officer
  IQ --> OP
  OP -->|landing Merge Ticket: main into the feature, gates, merge review| LM[ticket-captain of the landing]:::officer
  LM -->|PR into main with its PASS, Ready to Merge| H3([user clicks the merge, one Project at a time, and tells an invoked admiral]):::human
  H3 -->|map holder commissions landing captain cleanup; the map holder loads walkthrough| WT[Walkthrough Ticket: the map holder with the user]:::officer
  WT -->|findings: tickets, a second lap on the re-cut feature| OP
  WT -->|nothing: Project Completed| END([done]):::human
```

### 3b. One ticket

The captain is the connector: every crew member is briefed by it and returns to it, and it flips the
ticket's status at each chain spawn. The default crew is a `code-writer`, then the fresh
reviewers of [Delivery scale](../guides/working-tree-contract.md#delivery-scale-and-evidence). The dashed edge is the captain's discretion, bought with one short concrete risk reason.

```mermaid
flowchart TD
  classDef officer fill:#cfe2ff,stroke:#2c5aa0,color:#000
  classDef crew fill:#d4edda,stroke:#2e7d32,color:#000
  classDef reviewer fill:#f8d7da,stroke:#a71d2a,color:#000

  AD[map holder]:::officer <-->|"commission, then done &lt;key&gt;: PR ready, then merge, then done &lt;key&gt;: merged"| IC[ticket-captain: claims, sets the status at every chain spawn, posts every SHA]:::officer
  IC <-->|"1 write · Implementing"| CW
  IC <-.->|"1b contract review before any code, at the captain's discretion · In Review, then Implementing"| RS
  IC <-->|"2 review · In Review"| RC
  IC <-->|"3 merge · source parent stays Ready to Merge; Sub-ticket runs its chain"| MG
  subgraph CREW [the crew]
    CW[1 code-writer<br>builds and proves the contract<br>returns implement SHA, red proof, gates]:::crew
    RS{{1b reviewer: spec<br>reads the contract text on the ticket<br>returns review block}}:::reviewer
    RC{{2 reviewer: code or docs<br>returns review block}}:::reviewer
    MG[3 Merge Sub-ticket<br>a code-writer maps conflicts and gates and merges, reviewer: merge judges]:::crew
  end
```

### 3c. The captain divides its ticket, when the writer names lanes

The map already divided the destination into tickets. The captain's first act on its ticket is a
`code-writer` on the parent; before any code it sees whether the ticket holds separate work that can
run at the same time, and if so posts one comment naming the lanes and stops. One dish stays one ticket.

An example with nothing to explain: a dinner for eight on Saturday.

```mermaid
flowchart TD
  classDef admiral fill:#cfe2ff,stroke:#2c5aa0,stroke-width:2px,color:#000
  classDef captain fill:#cfe2ff,stroke:#2c5aa0,color:#000
  classDef lane fill:#d4edda,stroke:#2e7d32,color:#000
  classDef way fill:#fff3cd,stroke:#8a6d00,color:#000
  classDef ghost fill:#f7f7f7,stroke:#aaa,color:#777,stroke-dasharray:4 2

  AD[admiral: works the map, a dinner for eight on Saturday]:::admiral
  AD -.-> G[Grilling, settled with the user: healthy, no refined sugar, one vegetarian, 19:00]:::ghost
  AD -.-> C1[captain: the starter]:::ghost
  AD -.-> C2[captain: the main]:::ghost
  AD ==> C3[captain: the dessert, brownie with vanilla ice cream]:::captain
  AD -.-> C4[captain: the shopping]:::ghost
  C1 -.-> P1[...]:::ghost
  C2 -.-> P2[...]:::ghost
  C4 -.-> P4[...]:::ghost
  C3 --> S[the parent's code-writer, before any code: two lanes named in one comment]:::lane
  S --> L1[Sub-ticket: the brownie<br>its own branch, worktree and crew loop]:::lane
  S --> L2[Sub-ticket: the ice cream<br>its own branch, worktree and crew loop]:::lane
  C3 --> Q[Question, in Todo: which sweetener<br>blocks both Sub-tickets until the user answers]:::way
  C3 --> M[two Merge Sub-tickets, one per lane into the parent, each with its merge review]:::lane
  C3 --> W[the parent: the plating once both are in<br>one review of the whole dish, the PR, then its Merge Sub-ticket into the feature]:::captain
```

Charting settled the theme through one Grilling Ticket with the user: healthy, no refined sugar, one
vegetarian, food on the table at 19:00. The admiral works that map: three courses cooked in parallel
by three captains, and the shopping, blocked by all three menus. The diagram follows one captain.

- **The dessert's captain divides.** Its code-writer finds, before any code, that brownie and ice
  cream are made separately and at the same time, so the captain opens two Sub-tickets, each
  with its own branch, worktree and crew loop, and keeps the plating on the parent for when both are
  in. Each lane comes back through its own Merge Sub-ticket, one at a time; the dish is reviewed
  once, as a whole.
- **The starter's captain does not.** Its code-writer names no lane: one dish, one crew loop on the parent.
  Dividing is the exception the work has to earn.
- **The sweetener is a Question.** Both Sub-tickets need one, the recipes are silent, and the theme
  rules out sugar. The captain's discovery finds "no refined sugar" on the Grilling and no
  replacement, and research cannot settle a matter of taste, so the captain files a Question
  Sub-ticket under the dessert ticket in `Todo`, wired to block both lanes, priority `Medium` since
  two lanes wait. When the user answers, both resume. Had the question been which sweeteners the whole dinner allows, it would have gone to
  the admiral as a Project-level Question, because its answer reaches the other courses.

The rules the captain applies:

| Decision | Rule |
|---|---|
| Lanes first | the parent's code-writer goes first; before any code it names the lanes in one comment, or posts nothing |
| Lane or parent | separate work that can run at the same time becomes a lane; ordinary sequential work, the joining step, the parent's scenarios, the one review and the PR stay on the parent |
| What a lane carries | its parent's Type and Mode, a bounded outcome, a disjoint owned-path subset, exact gates, a child-key branch off the parent branch, an isolated worktree, its own status and evidence, its own [code-writer] → [reviewer] loop |
| What proves a lane | its gates; the parent's scenarios prove the joined result |
| Merges are Sub-tickets | each lane into the parent, in order, then the parent into the feature: one Merge Sub-ticket per merge, with its own merge review; never batched. An atomic ticket's own merge into main has none: the user clicks its reviewed PR |
| Depth | one level: a lane that needs splitting is replaced by sibling lanes; the Bug Type-map exception in §6.11, Merge and map-holder-held Sub-tickets are the other children |
| Local fog | a Question that touches only this ticket is a Sub-ticket here, in `Todo`; one whose answer reaches other tickets goes to the Project's map holder |

Step by step, with the Linear status each step leaves behind:

1. **Think.** The user brings an idea; the co-thinker does the homework, grills, fixes the words,
   recommends. Output: a DR when the ADR test passes, or ripe intent.
2. **File.** The user types `to-project`: a Linear Project in `Backlog` with the intent, its links
   and its answers. An atomic ticket is filed by the co-thinker with its five fields, one Type and one Mode, in `Todo`.
3. **Chart.** The map holder reads the Project, sets it `Planning`, and charts it with `wayfinder`:
   the map in the Project description, the first pickable tickets in `Todo`, which the user can
   write with `to-ticket`, and the Project-level Questions in `Todo`, wired, with priority by the
   standard's guide. The Project and its tickets are the plan. Only a cross-cutting architecture
   contract earns a repository plan: the map holder commits it on `main` and loops a fresh
   `reviewer(project-plan)` to PASS, two rounds at most; a second FAIL goes to the user with the
   findings as the choice.
4. **Approve.** The user approves the charted route, in an invoked admiral's session when one holds
   the map; a reviewed plan's status becomes `reviewed`, and the Project `Planned`.
5. **Open.** The map holder commissions the first Ticket Captain to open `feature/<slug>` from the
   approved `main` SHA before claiming its ticket. The map holder keeps the map in the Project description, gives every ticket its base branch and every ticket that merges its final Merge Sub-ticket, wired in plan order, and sets the
   Project `In Progress`. Tickets in
   `Todo` with no open blocker are pickable.
6. **Claim.** The map holder commissions a captain per pickable AFK ticket, and opens a HITL ticket's
   captain in a new top-level thread the user works in. Assignment is the claim; branch, base SHA and worktree path go
   on the ticket before the first edit.
7. **Write.** The captain spawns one `code-writer` and sets `Implementing`. The captain's contract
   says whether the ticket carries Gherkin. The writer posts nothing before code unless the work
   splits into lanes or the contract is inexact; it builds test-driven by Type and ends on the
   `implement` commit whose SHA is posted. A captain that wants the contract judged before the code
   records the one risk in the contract and buys a fresh `reviewer(spec)` on that text, with the
   ticket `In Review` while it runs.
8. **Shape.** Only when the writer names lanes: the captain writes the lane contracts and opens lane
   Sub-tickets in `Todo`, each with its own branch and worktree off the parent branch, plus one Merge
   Sub-ticket per lane, wired in order; the parent sits `In Progress` while its lanes run. Section 3c.
9. **Prove red.** The writer's `IMPLEMENTED` return carries its red proof: an unused-member Bug's definition and
   reference search, any other Bug's reproduction failing with the fix reverted, a Feature's tests failing with the code stashed.
10. **Review.** `In Review`; a fresh reviewer with one rubric returns the standard's one-line
    block, every finding labelled `blocker`, `should` or `nit`. PASS binds that candidate under that
    contract. FAIL, one or more blockers, returns the record to `Implementing`. From either verdict
    the captain files each should as its own ticket and records each nit as one line.
11. **Offer.** The captain pushes the branch, opens the PR into the feature branch with the block in
    its body, sets `Ready to Merge`, and returns `done <key>: PR ready`.
12. **Merge.** When the Merge Sub-ticket's blocker clears, the previous ticket's merge, the map holder
    resumes the captain with one word; when the next PR in plan order is not ready and a ready one
    does not depend on it, the map holder re-wires the order first. Its code-writer merges with `--no-ff`, a
    resolution that refactored leaving its code no worse than either side, before a fresh `reviewer(merge)` judges the integrated feature; PASS sets the Sub-ticket and the primary `Done`.
    The captain cleans up and returns `done <key>: merged`. PR by PR, in plan order, never batched.
13. **Inquisition (rare).** The map holder files an Inquisition Ticket with scope and cost in `Backlog`;
    the user moves it to `Todo` and tells an invoked admiral, or cancels; the map holder commissions
    a captain who runs it and files Bugs, which land through the normal loop before the landing. Section 6.6.
14. **Land.** The map holder files the landing Merge Ticket, blocked by every open should-ticket; its captain directs a code-writer to merge `main` into
    the feature, a resolution that refactored leaving its code no worse than either side, verifies combined gates, and obtains the merge review that proves the plan's acceptance criteria, opens
    the PR into `main`, sets `Ready to Merge`, and prepares the walkthrough. The user clicks, one
    Project at a time; the PR lands as a merge commit.
15. **Walk through.** The map holder commissions the landing Captain to clean up after the user
    lands, opens the Walkthrough Ticket and loads `walkthrough` itself, then
    facilitates it. Findings become tickets; the map holder commissions the first fix
    Captain to re-cut the feature from `main` under the same name. Nothing found sets the Project
    `Completed`, with the landing Captain's artifact cleanup confirmed.

## 4. States

A delivery ticket's status is the only delivery status, and Linear owns it: ten statuses in the
standard's order, which Linear draws as progress. The captain alone sets it, flipping at every chain
spawn. An open native blocker makes a ticket blocked in any status; there is no `Blocked` status
and no `Waiting for Human`; `Planning` remains a Project status only.

```mermaid
stateDiagram-v2
  state "FutureFeature" as Future
  state "Todo" as Todo
  state "In Progress" as InProgress
  state "Implementing" as Implementing
  state "In Review" as InReview
  state "Ready to Merge" as Ready
  state "Done" as Done
  [*] --> Future: no Type yet
  Future --> Backlog: promoted with a Type
  [*] --> Backlog: retained with a Type
  Backlog --> Todo: the user schedules it, one Type, one Mode
  Backlog --> Canceled: declined
  [*] --> Todo: a map holder, co-thinker or to-ticket creates it contracted
  Todo --> Implementing: captain spawns the author
  Implementing --> InProgress: the writer named lanes
  InProgress --> InReview: lanes merged, review of the whole
  Implementing --> InReview: implement hop posted
  Todo --> InReview: a spec review of the contract, before any code
  InReview --> Implementing: FAIL, a blocker; or a spec review's verdict
  InReview --> Ready: PR ready with its PASS
  Ready --> Done: its Merge Sub-ticket PASS; for an atomic ticket, the user's click
  Ready --> Implementing: merge review FAIL, reverted
  InReview --> Done: a Merge Sub-ticket's own review PASS
  Implementing --> Todo: released
  InReview --> Todo: released
  Ready --> Todo: released
  InProgress --> Todo: released
  Future --> Todo: promoted and contracted
  InReview --> Canceled: a Merge Sub-ticket reverted
  Todo --> Canceled
  Implementing --> Canceled
  Todo --> Duplicate
```

Who sets what: the captain sets every status of its ticket and Sub-tickets; the Project's map holder
sets Project statuses and its own map-holder-held tickets'. A `Question` runs `Todo` → `Done` and `Todo` on it is
the user's turn; `Research`, `Grilling` and `Walkthrough` run `Todo` → `In Progress` → `Done`. A
captain-held ticket normally runs `Todo` → `Implementing` → `In Review` → `Ready to Merge` → `Done`,
with `In Progress` while its lanes run; a spec review the captain buys shows `In Review` before
`Implementing`. A Merge Sub-ticket runs `Todo` → `Implementing` → `In Review` →
`Done` and never waits to be merged: one code-writer maps the conflicts and merges, a resolution
that refactored leaving its code no worse than either side. An atomic ticket has no Merge Sub-ticket:
it waits in `Ready to Merge` for the user's click. An Inquisition
Ticket runs `Backlog` → `Todo`, the user's confirmation → `In Progress`, the captain writes the
contract, then the sweep and proofs → released `Todo` → resumed `In Progress` → `Done`. It is
released while its separate record Feature runs the normal docs delivery chain, and resumed for
retention verification. A Merge Sub-ticket
whose review fails on the landed work is reverted and closes `Canceled` with the reason.

## 5. Edges: the contract table

One row per contact. A sender's return must carry every field the receiver's Must-Reads consume;
a field read that nobody returns, or returned that nobody reads, is a finding.

| # | Edge | Channel | Sender returns or writes | Receiver reads | Status set |
|---|---|---|---|---|---|
| 1 | user → co-thinker | C | the idea | about, architecture, glossary, linear-workspace-standard | - |
| 2 | co-thinker → repository | F | a Decision Record; a glossary entry | - | - |
| 3 | user → `to-project` → Linear | C, L | the Project: title, summary, intent, decisions taken, out of scope, links to the DR, glossary entries, source FutureFeature | - | Project `Backlog` |
| 4 | co-thinker → Linear (atomic ticket) | L | a ticket with one Type, one Mode, outcome, owned paths, blockers, exact gates, base branch | - | `Todo` |
| 5 | user → admiral | C | the Project, at any stage | the Project, its plan at the governing commit when one exists, every ticket contract, working-tree contract | - |
| 6 | map holder → Linear (chart) | L | through `wayfinder`: the map in the Project description; first tickets with one Type, a Mode where a captain holds the Type, the five fields or their Type's own template, and native blocking edges; Project-level Questions naming waiters, with priority | the Project, governing DRs, about, architecture, dydo-glossary, linear-workspace-standard | Project `Planning`; first tickets and Questions `Todo` |
| 7 | map holder → repository, only for a cross-cutting architecture contract | F | the plan commit on `main` | - | - |
| 8 | map holder → reviewer(project-plan) | R (spawn) | the plan path at its commit | the plan, the project-plan rubric, cited DRs and paths | - |
| 9 | reviewer(project-plan) → map holder, Linear | R, L | the review block, as a Project update | - | - |
| 10 | invoked admiral → user | C | the charted route and any passing plan, for approval; after two FAILs, the findings as the choice | - | any plan `reviewed`; Project `Planned` |
| 11 | admiral → first Ticket Captain, Linear | R, L | commission to open `feature/<slug>` from the approved main SHA before claim; the map in the Project description; base branch and blockers on every ticket, priority on every HITL one; the final Merge Sub-ticket of every captain-held ticket that merges, created under it and blocked by the previous one in plan order | - | Project `In Progress` |
| 12 | admiral → ticket-captain (AFK) | R (spawn), L | the ticket key; assignment | the ticket's five fields, the plan at its governing commit, working-tree contract | - |
| 13 | user → ticket-captain (HITL, atomic, or any ticket without an invoked admiral) | C, L | the ticket key; assignment | the same | - |
| 13b | ticket-captain (top-level) → user, admiral | C, L | `done <key>` or `released <key>: <reason>` in its own session; the user tells the admiral | the record | - |
| 14 | ticket-captain → ticket | L, G | branch, base SHA, worktree path | - | - |
| 15 | ticket-captain → code-writer | R (spawn) | the record to write, its kind | the owning ticket, and on a fix hop the FAIL block that sent it; the governing Project plan at its linked commit and the Decision Records it names; coding-standards | `Implementing` |
| 16 | code-writer → ticket-captain | R, L, G | before code, only when needed: one comment naming the lanes or the inexact contract line; after: the `IMPLEMENTED` line with implement SHA, behaviour, red proof, the suites and static gates run with exits, blocker | - | - |
| 17 | ticket-captain → Sub-tickets | L, G | lane Sub-tickets with the parent's Type and Mode, disjoint paths and branches, or retained Bug stages with native ordering and serial path transfer; one Merge Sub-ticket per actual integration; a Question Sub-ticket for local fog | - | lanes `Todo`; parent `In Progress` |
| 18 | ticket-captain → reviewer(spec), optional, for one risk the contract records, before the code | R (spawn) | the record carrying the captain's contract | that text on the ticket, the five fields, base SHA, branch, worktree, owned paths | `In Review` → `Implementing` |
| 19 | ticket-captain → code-writer, or docs-writer for a documentation change (fix hop) | R (spawn) | the ticket, the candidate, the review block that sent it | the ticket, the block, the plan, standards | `Implementing`, on the FAIL's blockers |
| 20 | code-writer or docs-writer (fix hop) → ticket-captain | R, G | ticket key, fix SHA, each blocker with what closed it, any label changed with its evidence, gates rerun | - | - |
| 21 | delivery ticket-captain → docs-writer | R (spawn) | the docs ticket and linked plan; for a record Feature, its exact owned record/navigation paths and the Inquisition's pinned packet | the delivery ticket, packet, about, writing-docs and working-tree retention contract | record Feature or other docs ticket `Implementing` |
| 22 | docs-writer → ticket-captain | R | ending commit SHA, files changed, what each says and why, witnesses, `dydo check` and gate results | - | - |
| 23 | ticket-captain → reviewer(code \| docs) | R (spawn) | rubric name, `Contract: <KEY> description as of <Linear updatedAt>`, Candidate SHA, Base SHA, the writer's `IMPLEMENTED` line | the contract as of that `updatedAt` with outcome, scenarios, owned paths, gates; the rubric; the hops; the `IMPLEMENTED` line, rerun only on a stated doubt | `In Review` |
| 24 | reviewer → ticket-captain, record, PR | R, L, G | the review block in the standard's one-line `PASS` or `FAIL` form, every finding with its severity; observations after it | - | `Implementing` on FAIL, one or more blockers |
| 25 | ticket-captain → Merge Sub-ticket (one per lane) | L, R (spawn), G | the lane branch at its PASS SHA; a code-writer maps conflicts and gates, merges it into the parent, a resolution that refactored leaving its code no worse than either side, then a fresh `reviewer(merge)` over the parent | the Merge template's fields: source, target, combined gates | lane `Ready to Merge` at its PASS, `Done` when merged; after the last, parent `In Review` for the review of the whole |
| 26 | ticket-captain → admiral | G, L, R | the PR into the feature branch with the block; `done <key>: PR ready` | the record | parent `Ready to Merge` |
| 27 | admiral → ticket-captain | R (message) | `merge`, when the Merge Sub-ticket's blocker clears, after re-wiring the order when a later PR was ready first; or a fresh commission from the record | the Merge Sub-ticket, the PR, the feature SHA | - |
| 28 | ticket-captain → Merge Sub-ticket (into the feature) | R (spawn), G | a code-writer maps the conflicts and combined gates, merges `--no-ff` and resolves, a resolution that refactored leaving its code no worse than either side, then a fresh `reviewer(merge)` over the integrated feature | the merge commit, both parents, the landed ticket's gates, the plan at its governing commit | Sub-ticket and primary `Done` on PASS |
| 29 | reviewer(merge) → ticket-captain, Merge Sub-ticket | R, L | the review block naming the merge commit and the gates rerun | - | - |
| 30 | ticket-captain → admiral | R, L | `done <key>: merged`; worktrees and branches cleaned | - | - |
| 31 | map holder → Linear, repository | L, F | new, split, dropped or resequenced tickets and Project-level map-holder-held tickets; dated amendments committed to a repository plan, where one exists | a fresh `reviewer(project-plan)` reads an amendment that moves destination, scope, acceptance or architecture | - |
| 32 | admiral → Linear (inquisition proposal) | L | an Inquisition Ticket under the Project: scope, the parts and lenses, the cost, the feature SHA | - | `Backlog` |
| 33 | user → Linear, admiral (inquisition confirmation) | L, C | the Inquisition Ticket moved to `Todo`, or `Canceled` with the reason; the user tells the admiral | - | `Todo` |
| 34 | admiral → ticket-captain (inquisition) | R (spawn), L | the Inquisition Ticket key; assignment | the ticket, the plan at its governing commit, the integrated feature SHA, working-tree contract | - |
| 35 | inquisition captain → Git | G | `inquisition/<slug>` off the feature SHA, never merged; a child branch per proof | - | - |
| 36 | inquisition captain → inquisitors | R (spawn) | one part or one lens each, the scope, the plan, the ticket review evidence | the assignment with its evidence, about, architecture, coding-standards | `In Progress` |
| 37 | inquisitor → inquisition captain | R | findings with `file:line`, severity and proof; hypotheses of what could go wrong, each with the test that would decide it, or, in prose, docs or a prompt file, both contradicting passages quoted at `file:line`, which are its reproduction with no test and no proof branch; an unused member, export or field is proved by its definition and a reference search, with no test either | - | - |
| 38 | inquisition captain → code-writer (proof-only) | R (spawn) | one code hypothesis, its child branch off the inquisition branch, source read-only | the hypothesis as the ticket, coding-standards | - |
| 39 | code-writer (proof-only) → inquisition captain | R, G | `confirmed` with the red test at its SHA, `not reproduced`, or `inconclusive`, with the observation that decided it | - | - |
| 40 | inquisition captain → Linear, Git, Bug captain | L, G | one Bug per confirmed problem under the Project, feature base, reproduction SHA and pushed independent named ref (a prose Bug: its quoted passages; an unused-member Bug: its definition and search), Inquisition link; retention ownership transfers only on recorded Bug-captain adoption | Bug captain reads the reproduction as normal-chain input and records cleanup responsibility | Bugs `Todo` |
| 41 | inquisition captain → admiral, Linear | R, L, G | completed pinned packet: feature SHA, scope, parts/lenses, findings, hypotheses/verdicts and Bugs; pushed/posted resume state; `released <key>: record delivery` before record Feature/blocker exists | this return wakes admiral to read packet and working-tree retention contract | Inquisition released `Todo`, unassigned, worktree removed; no not-yet-created blocker required |
| 41a | admiral → record Feature captain, Linear | R (spawn), L | on row41 wake, first contract separate primary Feature/AFK on retained feature with exact record and authored navigation paths, packet and gates; wire only Inquisition blocked by Feature before generic pickup, then commission record captain | record captain reads contract/packet and directs docs-writer → docs review → final Merge Sub-ticket | record Feature `Todo`, pickable from packet; audit is not recommissioned from temporary Todo gap |
| 41b | record Feature captain → admiral, Inquisition Ticket | R, L, G | `done <key>: merged`; exact record path/blob or digest, delivery merge SHA and retained feature ref, review/gates on delivery records | normal board loop reads delivery `Done` and resumes released Inquisition captain | record Feature `Done`; Inquisition resumes `In Progress` |
| 42 | inquisition captain → admiral, Linear | R, L | `done <key>` after verifying Bugs, exact record content and delivery merge ancestry/reachability on retained feature; verification on ticket, audit branch deleted, open Bug proof refs retained with named owners | admiral reads durable completion evidence | Inquisition `Done` |
| 43 | map holder → Linear (landing) | L | the landing Merge Ticket: `main` into the feature, gates, merge review with acceptance proof, the PR into `main`, the walkthrough prepared | - | `Todo`, blocked by unresolved prerequisites for this landing: delivery, confirmed Inquisition/record and required fixes; exclude self and later/deferred work |
| 44 | landing captain → Git, admiral | G, R | the PR into `main` with its PASS block; `done <key>: PR ready` | - | landing `Ready to Merge` |
| 45 | admiral → user → Git | C, G | the PR to click and the walkthrough that follows; the feature merged into `main` as a merge commit, one Project at a time; the user tells the admiral, which resumes the landing captain to close and clean up the merged feature: `done <key>: merged` | - | landing `Done`, set by its captain |
| 46 | map holder → Walkthrough Ticket, user | L, C | the ticket; `walkthrough` loaded by the map holder, then the four-part tour; commission first fix Captain to re-cut the feature if findings reopen the lap | - | `In Progress` → `Done`; findings as tickets; Project `Completed` when none |
| 47 | crew → ticket-captain (hand-raise) | R | the question, what was searched, why it blocks, facts or options found | - | - |
| 48 | ticket-captain → research | R (spawn) | the question and where the findings land | the question and destination, about, architecture | - |
| 49 | research → ticket-captain | R, L or F | one-line answer, destination, unsettled points; the report as a ticket comment or scratch file | - | Research Ticket `Done` by the map holder |
| 50 | ticket-captain → Linear (local fog) | L | a Question Sub-ticket under the delivery parent, wired as blocker, with its priority by the standard's guide; the record is the whole report | - | Question `Todo` |
| 51 | ticket-captain → admiral (Project-level fog, or any release) | R, L, G | `released <key>: <reason>`; prepared packet and resume SHA on the record, branch pushed, worktree removed, parent unassigned | - | parent `Todo`, blocker wired |
| 52 | admiral → Linear, user | L | a Project-level Question Ticket with homework, options, recommendation, wired to every waiter, with its priority by the standard's guide | - | `Todo` |
| 53 | user → Linear, repository, admiral | L, F, C | the answer on the ticket; a DR when it qualifies; the user tells the admiral | - | Question `Done` |
| 54 | admiral (every wake: a return or the user's word) → Linear | L, R | commissions every pickable ticket, blocker-cleared ones included; re-wires the merge order when a later PR is ready first; resumes every Merge Sub-ticket whose turn came; re-sets priority on what waits on the user | the board | - |
| 55 | user → chief-of-staff | C | a request for triage | the board: open Questions, the gates, Projects in flight; working-tree contract | - |
| 56 | chief-of-staff → user, Linear | C, L | three lists with recommendations; mechanical fixes; delivery staged on its Project for the admiral | - | - |
| 57 | user → ticket-captain (takeover) | C via the admiral, or the sub-agent's transcript | `release`: the captain releases as in row 51; the user opens a top-level captain on the ticket | the record's resume point | parent `Todo` |

## 6. Exceptions

### 6.1 A crew member raises a hand

Fog inside a ticket. The rule is fog → discovery → question ticket, and the Question is the last rung.

```mermaid
sequenceDiagram
  participant W as crew
  participant C as ticket-captain
  participant R as research
  participant A as admiral
  participant B as Linear
  participant H as user
  W->>C: hand-raise: question, sources searched, why it blocks, options found
  C->>C: bounded discovery: DRs, plan, ticket links, glossary, code, tests
  alt the sources settle it
    C->>W: answer recorded on the ticket, resume
  else a fact outside the tree settles it
    C->>R: the question and the destination
    R-->>C: one-line answer, report on the ticket
    C->>W: resume
  else user judgment, inside this ticket's outcome and the Project destination
    C->>B: Question Sub-ticket under the delivery parent, Todo, wired as blocker, with its priority
    C->>A: released &lt;key&gt;: &lt;reason&gt;
    Note over C,B: packet and resume SHA on the record; parent Todo, Question wired as blocker
    B-->>H: the chief-of-staff or the board surfaces the Question in Todo
    H->>B: answer on the ticket, Question Done, blocker cleared
    H->>A: tells the admiral
    A->>C: next wake, the parent is pickable, re-commission from the record
  else the answer could change other tickets, a shared contract or the destination
    C->>A: released &lt;key&gt;: &lt;reason&gt;
    Note over C,B: prepared packet and resume SHA on the record
    A->>B: Project-level Question Ticket in Todo, wired to every waiter, with its priority
    B-->>H: surfaced
    H->>B: answer, a DR when it qualifies
    H->>A: tells the admiral
    A->>A: wayfind: amend the map, re-review when destination, scope, acceptance or architecture moved
    A->>C: re-commission the ticket
  end
```

The contract: the crew member never fills a gap with an assumption and never creates a ticket; the captain
owns discovery and the local map; the scope rule in the workspace standard decides local Sub-ticket
versus Project-level packet; the Project's map holder alone creates Project-level Questions; the user answers on
the ticket, never in a chat that evaporates; the chief-of-staff surfaces open Questions when the user
asks it to, it is never sent anything.

A call above the officer's say goes through `what-would-balazs-do`
([DR 054](../project/decisions/054-what-would-balazs-do-tribunal.md)) when it is DR-grade or
irreversible, or when the user is away. Only a call the tribunal decides while he is away, or on his
own invocation, stays unwired, for him to ratify or overturn; every other call, irreversible or
undecided, gets a prepared Question wired to its waiters.

### 6.2 Review FAIL

```mermaid
flowchart LR
  classDef crew fill:#d4edda,stroke:#2e7d32,color:#000
  classDef reviewer fill:#f8d7da,stroke:#a71d2a,color:#000
  RV{{reviewer verdict}}:::reviewer -->|PASS block| OK[captain records block, files its shoulds and nits, opens PR, returns done &lt;key&gt;: PR ready to admiral]
  RV -->|"FAIL, fewer than five in a row"| RT{captain takes the findings}
  RT -->|"should · nit"| DF[captain files each should as a ticket, records each nit as a line]
  RT -->|"blockers · Implementing"| IM[fresh author of the change's kind, code-writer or docs-writer: fix hop; it amends a scenario when the FAIL block names it]:::crew
  IM --> FR{{fresh reviewer, new Candidate SHA · In Review}}:::reviewer
  FR --> RV
  RV -->|fifth consecutive FAIL| ESC[escalate: comment on the ticket, Question Ticket as blocker, admiral, then the user]
```

The contract: FAIL, one or more blockers, is binding and returns the record to `Implementing`; the
fix hop closes the blockers only, checks each label against the source, and amends a wrong or
missing scenario; every correction is its own commit and the re-review pins the new SHA; every
finding carries a severity and none is dropped
([DR 058](../project/decisions/058-review-severities-blocker-should-nit.md)); the fifth consecutive FAIL
in one review loop stops the loop rather than softening the verdict. A contract amendment that changes
acceptance is an amendment of the contract and, under a Project, goes to the map holder as a plan
amendment.

### 6.3 The contract or the route is disproved mid-implementation

```mermaid
flowchart TD
  classDef crew fill:#d4edda,stroke:#2e7d32,color:#000
  classDef officer fill:#cfe2ff,stroke:#2c5aa0,color:#000
  IM[the code-writer meets a crossroads the contract left open, or a scenario it cannot satisfy]:::crew --> ST[stops at the choice, reports the mismatch]
  ST --> IC[ticket-captain]:::officer
  IC -->|the route was wrong, the contract stands| SP[fresh code-writer on the corrected route]:::crew
  IC -->|a scenario was wrong| SP2[captain amends the contract on the ticket; fresh code-writer]:::crew
  IC -->|acceptance, scope, destination or architecture would move| AD[map holder: map amendment; a plan's amendment re-reviewed by project-plan]:::officer
  SP --> RES[resume the writing from the amended contract]
  SP2 --> RES
  AD --> RES
```

The contract: the scenario set is the captain's, so the writer wires each scenario and changes one
only when asked; the change is written on the ticket; a contract change above the ticket's authority
climbs the ladder before work resumes.

### 6.4 Plan amendment

The approved route fixes the destination, not every turn. The map holder creates, splits, drops and
resequences tickets and records discoveries; where a repository plan exists, it commits dated
`## Amendment - <date>` sections to it. With the project-planner gone, the map holder commits a
repository plan - the initial plan and its amendments alike - on `main` through an atomic docs ticket
when the Project has no feature branch yet, or none exists at all for a standalone cross-cutting
plan, and on the Project's feature branch once one is open. Route-only amendments need no review. An amendment
that changes destination, scope, acceptance criteria or governing architecture goes back through
`reviewer(project-plan)`, when a plan exists, and user approval before the affected tickets are
commissioned. A unanimous away `what-would-balazs-do` verdict is that approval and work resumes;
only an irreversible change waits for his word. The review
loop is capped at two rounds at any time; the second FAIL is the user's choice.

### 6.5 Merge review FAIL

The merge is a ticket, so the FAIL has an owner.

```mermaid
flowchart LR
  classDef officer fill:#cfe2ff,stroke:#2c5aa0,color:#000
  classDef crew fill:#d4edda,stroke:#2e7d32,color:#000
  RM{{merge review FAIL}} --> RT{the Merge Sub-ticket's captain routes it}:::officer
  RT -->|an integration defect: a resolution, a lost hunk, a seam| FX[fix hop inside the Merge Sub-ticket, fresh merge review]:::crew
  RT -->|a defect in the landed work| RV[revert inside the Merge Sub-ticket, which closes Canceled with the reason]:::crew
  RV --> SRC[the source ticket returns from Ready to Merge to Implementing with the findings, then re-offers]:::officer
```

Revert keeps the feature branch always green and the ticket's own loop intact. Once a later merge
already depends on the failed one, a fix ticket follows it instead of a revert. An atomic ticket has
no Merge Sub-ticket, so a defect found on `main` after its merge is a Bug.

### 6.6 The inquisition

A ticket like any other, with its own captain, run once the feature is integrated and the user has
confirmed the spend. It does what a review does, at two scales a single review cannot reach: many
read-only eyes on the parts and on the whole, and hypotheses of what could go wrong proved: one in
code by a proof-only test, one in prose, docs or a prompt file by its quoted passages. It does not
gate; it files.

```mermaid
flowchart TD
  classDef human fill:#f6d365,stroke:#8a6d00,color:#000
  classDef officer fill:#cfe2ff,stroke:#2c5aa0,color:#000
  classDef crew fill:#d4edda,stroke:#2e7d32,color:#000

  AD[map holder: the user, or an admiral the user invoked; the feature is integrated]:::officer -->|Inquisition Ticket in Backlog: scope, parts, lenses, cost| H([user]):::human
  H -->|moves it to Todo and tells an invoked admiral, or cancels; the map holder commissions| IC[ticket-captain of the inquisition]:::officer
  IC -->|inquisition branch off the feature SHA, never merged| BR[(inquisition/slug)]
  IC -->|contracts it: the parts, the lenses, the shape of a hypothesis| SP[(the Inquisition Ticket)]
  IC --> P1[inquisitor: module A]:::crew
  IC --> P2[inquisitor: module B]:::crew
  IC --> P3[inquisitor: the whole, at the seams]:::crew
  IC --> P4[inquisitor: one lens across everything]:::crew
  P1 & P2 & P3 & P4 -->|findings with proof, and hypotheses of what could go wrong| IC
  IC -->|one code hypothesis each, proof-only, on a child branch| IM[code-writers: write the test that would catch it]:::crew
  IM -->|confirmed with a red test, not reproduced, or inconclusive| IC
  IC -->|dedupe, reproduction SHA on pushed independent ref with retention owner, quoted passages, or the definition and search| BUG[(Bug Tickets in Todo, under the Project)]
  IC -->|completed pinned packet and pushed/posted resume state| REL[Inquisition captain: release Todo, unassign, remove worktree; Feature and blocker do not exist yet]:::officer
  REL -->|released &lt;key&gt;: record delivery wakes the map holder| AD2[map holder]:::officer
  AD2 -->|before generic pickup: contract Feature, wire only audit blocked by it, then commission| RC[record Feature captain: normal delivery chain, exact record and navigation paths]:::officer
  RC --> DW[docs-writer on the record Feature branch]:::crew
  DW --> RV[docs review, then final Merge Sub-ticket into retained feature]
  RV -->|delivery Done, exact record and merge evidence; normal board wake| AD2
  AD2 -->|resume In Progress after delivery Done| VERIFY[Inquisition captain: verify Bugs, record content and merge reachability; preserve open Bug refs]:::officer
  VERIFY -->|Done, audit branch deleted, done &lt;key&gt;| AD2
  BUG -->|the normal loop, a captain each, before the landing| FIX[fixes through PR, review, Merge Sub-ticket]
```

The contract: the map holder proposes by filing the Inquisition Ticket in `Backlog` with the parts and
lenses it wants swept and the cost; the user confirms by moving it to `Todo`, which is the gate DR
045 reserves; the captain claims it like any ticket and works on `inquisition/<slug>`, cut from the
integrated feature SHA and deleted when the ticket is `Done`, so nothing on it can leak into the
product; inquisitors are read-only and refute their own catches, and their second product is the
hypothesis list; each hypothesis in code goes to a proof-only code-writer whose only output is a test,
red if the hypothesis holds, while one in prose, docs or a prompt file is proved by both contradicting
passages quoted at `file:line` on the feature SHA, with no test and no proof branch; an unused
member, export or field is proved by its definition and a reference search, with no test either,
by `skills/roles/officers/ticket-captain/resources/inquisition.md`; a confirmed
hypothesis is no longer a hypothesis and joins the findings; the captain deduplicates and files one
Bug per problem under the Project, with the feature as base branch and the red test's commit, the
quoted passages, or the definition and search, as reproduction, so each is picked up by a captain and fixed through the normal loop. The completed packet goes to the map holder, whose separate record Feature delivers
`dydo/project/inquisitions/` through its own captain, docs-writer, reviews and Merge Sub-ticket.
The [working-tree contract](../guides/working-tree-contract.md#retaining-an-inquisitions-record-and-proofs)
owns the one-way blocker, release/resume, durable-content checks and independent Bug proof refs.
The Inquisition closes only after those checks; its return is `done <key>`. There is no PASS or FAIL:
Project acceptance is proved by
the landing's merge review, and the Bugs the inquisition filed are landed before the landing.

### 6.7 An atomic ticket

No Project, no admiral. The co-thinker reads the workspace standard and writes the ticket with its five fields, one Type
and one Mode in `Todo`; the user opens a
captain on it, or spawns one from any session, and it claims from `main`, runs the same crew and the same
reviewer, and opens the PR into `main` with its PASS block. There is no Merge Sub-ticket (DR 047,
amended 2026-09-23): once CI is green the captain sets `Ready to Merge` and returns
`done <key>: PR ready`; the user clicks the merge; the captain then closes the ticket `Done`, cleans
up and returns `done <key>: merged`. Beyond that click, the user's only gate is the one the captain
chooses to raise. The user's own commits on main are outside the model and need no ticket.

### 6.8 Escalation and precedence

```mermaid
flowchart LR
  W[crew] -->|hand-raise, returned to the captain| C[ticket-captain]
  C -->|released &lt;key&gt;: &lt;reason&gt;| A[map holder]
  C --> R[(ticket record: prepared packet and resume SHA)]
  A -->|a DR conflict, live external state, missing authority| H[user]
```

Agents settle operational conflicts themselves, highest first: the user's live instruction, a
Decision Record, the reviewed architecture plan, the feature spec (both pinned), the ticket contract,
coding standards, existing code. Beside that ladder stands the truth rule for live work: the Linear record is the
default truth and its latest word wins; an agent that finds a conflict raises it, and a live
instruction that overrides the record is written back to it. A crew member raises its hand by returning to its Ticket Captain (row 47); the
captain's rungs are a comment on the ticket and, when blocked, a wired Question Ticket in `Todo`;
never silent waiting. The code-writer's one pre-code comment naming lanes or an inexact contract is
its scoped exception, not a hand-raise.

### 6.9 A prototype

A Prototype Ticket is held by a captain and run for the user: fast sketches that settle a visual or
behavioural choice, co-thinking in code. Its contract names the question and the variants; its
code-writer builds them on `prototype/<name>` in its own worktree; the user is the review, in the
session. The verdict goes on the ticket with the winning commit. The
branch is kept and linked from the ticket until the delivery ticket is `Done`; that ticket's code-writer
reads it as the template for a fresh rewrite, never as a base or a copy, and nothing on it is ever submitted or merged (DR 047 §5).

### 6.10 A release: blocked, taken over, or dead

One mechanism serves three cases.

```mermaid
flowchart LR
  classDef officer fill:#cfe2ff,stroke:#2c5aa0,color:#000
  classDef human fill:#f6d365,stroke:#8a6d00,color:#000
  B[a blocker the captain cannot clear] --> REL
  T([the user wants to drive it]):::human -->|release, through the map holder| REL
  D[the session dies, no return] -.->|the map holder treats it as a release without the push| REL
  REL[the captain releases: branch pushed, worktree removed, parent to Todo, unassigned, blocker wired]:::officer --> REC[(the record: every hop's SHA, the last one is the resume point)]
  REC -->|blocker cleared, next wake| RC[a fresh captain resumes from the branch]:::officer
  REC -->|a top-level captain session| H2([the user drives it]):::human
```

On Claude Code a one-off steer needs no release: the user opens the captain's transcript from the
subagent panel and sends it a message there. Anything longer is a release and a top-level captain.
The steer is a Claude Code convenience; the floor on both hosts is the release.
[DYD-88 - Codex sub-agent lifecycle observations - 2026-09-14](https://linear.app/bodnar-balazs/document/dyd-88-codex-sub-agent-lifecycle-observations-2026-09-14-37f58f170af1) established returned-sub-agent resume,
while passive wake and user-origin steering remain unestablished on Codex.

### 6.11 A Bug

A Bug runs the same chain, mapped by its captain from the template's default. For a simple known
cause, collapse reproduce-or-identify and fix placeholders into parent hops and close unused
records `Canceled` with the reason. DR 047 §5 also permits retaining those ordered Bug Sub-tickets:
fix is natively blocked by reproduction, and shared paths transfer only after reproduction closes
with its evidence recorded. Each stage has its own contract, chain, branch and worktree; every
actual integration has a Merge Sub-ticket. This Type-map exception does not relax disjoint ownership
for parallel lanes; joining acceptance and final review stay on the parent. An elusive defect turns identification into hypotheses and proof tests,
as the inquisition does; a trivial one collapses to one record. The reproduction is a scenario when
the defect shows at the product's boundary, else a red test written through diagnosing-bugs; the
fix proves itself by reverting and watching that reproduction fail again. An unused member needs no
test or revert: its definition and a reference search showing no caller outside the tests are its
reproduction. A Bug the inquisition filed arrives with its red
test at a commit, which the fixing code-writer adopts, or, from prose, with its quoted passages, or with an unused-member finding's definition and quoted reference search. Under a Project it lands like any ticket; outside one it
is atomic.

### 6.12 The second lap

The walkthrough finds something. The Project stays open: the findings become tickets, the feature
branch is re-cut from `main` under the same name, and the lap runs fixes → Merge Sub-tickets → the
landing → another walkthrough, with an inquisition in between only when the user confirms one. The
Project is `Completed` when a walkthrough finds nothing.

## 7. A day with three Projects

The target the model is built for: the user's queue is never empty and never a wall. Every item on
it has its homework done, so the user's minutes go to judgment, taste and direction: an answer, a
reaction to a prototype, an approval, a landing, a walkthrough. Agents never wait on the user
without a `Question` in `Todo`, and the user never waits on agents: when the queue empties, the next
idea goes to a co-thinker. Priority on what waits on him says which comes first: the one that frees
the most AFK work.

This day the user has invoked three admirals for throughput: three top-level sessions in three terminals, each in its feature worktree; their
captains are sub-agents in ticket worktrees. The user's own terminal wears chief-of-staff to read the
queue, and co-thinker or a HITL ticket's captain, a Prototype's among them, to act on it.

A snapshot at 10:40:

| Project | Status | Session | In flight | Waits on the user |
|---|---|---|---|---|
| A: Reqnroll in DynaDocs | `In Progress` | admiral A | DYD-90 `Implementing`; DYD-91 `Todo`, blocked by DYD-90, whose outcome it builds on | nothing |
| B: Notion export | `In Progress` | admiral B | DYD-95 `In Review`; DYD-96 `Implementing`; DYD-97 Prototype, HITL, `Todo`, `High`: its verdict frees DYD-98 | a captain session on the prototype |
| C: Attention taxonomy | `Planning` | admiral C | the draft of its cross-cutting architecture plan, blocked by DYD-99 Question in `Todo`, `High`: the whole plan waits | an answer |

```mermaid
flowchart LR
  classDef human fill:#f6d365,stroke:#8a6d00,color:#000
  subgraph A [Project A, admiral A]
    A1[DYD-90 Implementing]
    A2[DYD-91 Todo, blocked by DYD-90]
  end
  subgraph B [Project B, admiral B]
    B1[DYD-95 In Review]
    B2[DYD-96 Implementing]
    B3[DYD-97 Prototype, HITL, Todo]
  end
  subgraph C [Project C, admiral C]
    C1[DYD-99 Question, Todo]
    C2[plan draft, blocked by DYD-99]
  end
  H([user]):::human -->|1: answer, five minutes| C1
  H -->|2: prototype session, half an hour| B3
  H -.->|3: nothing yet| A1
```

The chief-of-staff's three lists at that moment: *Answer needed*, DYD-99, `High`, a five-minute
answer; *Approval needed*, none; *Landing*, none. DYD-97, also `High`, waits on the user's captain
session rather than an answer. Then, in order:

1. **10:40.** The user answers DYD-99 on the ticket and tells admiral C; the blocker clears; admiral
   C finishes the plan and sends it to `reviewer(project-plan)`.
2. **10:45.** The user opens a captain session on DYD-97, the prototype: a UI question, two
   variants to react to in that session. While it runs, DYD-90's code-writer returns and its reviewer PASSes; its captain
   opens the PR, sets `Ready to Merge` and returns `done DYD-90: PR ready`; admiral A resumes it for the
   Merge Sub-ticket, whose review PASSes; DYD-91's blocker clears and it gets a captain. DYD-95's
   reviewer FAILs on one finding; its captain routes it to a fresh code-writer and the ticket shows
   `Implementing`.
3. **11:15.** The prototype's verdict is on DYD-97 with the winning commit and the user tells
   admiral B, which graduates the answer into DYD-98, a delivery ticket. Project C's plan has a PASS
   and waits for approval: the next item on the queue.
4. **11:20.** The user reads the plan's destination and first tickets in admiral C's terminal and
   approves; the Project is `Planned`; admiral C commissions its first captain to open the feature
   from the approved main SHA before claiming its ticket.
5. **11:30.** The queue is empty. Admiral A has two captains running, admiral B one and a fresh
   review, admiral C one. The user takes the next raw idea to a co-thinker, or reads the walkthrough
   of what landed since morning.

## 8. Prompt-file propagation - DYD-90

The authored contacts now carry DR 047. DYD-90's specification pins the file boundary and proof;
its candidate receives independent docs review and root contact review before integration.
Completed rows below name the source, not a claim that generated runtime output has been refreshed.

| File | Disposition |
|---|---|
| ~~admiral~~ | Authored: optional, only when the user invokes it (DR 051); charts with `wayfinder` as map holder; board wakes, planning/review/user approval, captain commissions and merge ordering; no Git beyond its plan's commits; landing and walkthrough. |
| ~~ticket-captain~~ | Authored: specify first, lanes/empty hops, statuses, four-field reviewer brief, two-step returns, release and Merge FAIL. |
| ~~project-planner~~ | Removed by DR 051: the map holder charts with `wayfinder`, and `to-ticket` carries upstream tracer bullets and blockers. |
| ~~specifier, implementer, hardener~~ | Retired: the single `code-writer` absorbs all three (DYD-222); the `Specifying` and `Hardening` statuses retired on 2026-09-23. |
| ~~reviewer~~ | Authored: four-field brief, pinned block on work judged, same merge rubric at every level. |
| ~~docs-writer~~ | Authored: captain invocation, committed evidence and inquisition record. |
| ~~inquisitor~~ | Authored: read-only captain assignment, findings/hypotheses with proof seam; no workflow verify job. |
| ~~chief-of-staff~~ | Authored: Questions plus user gates, priority and released blockers; prototype retention follows delivery completion. |
| ~~co-thinker~~ | Authored: explicit to-project graduation, atomic five-field contract, FutureFeature status. |
| ~~wayfinder~~ | Authored: map holder at both levels, local/Project Question scope, Merge order. |
| ~~to-project, wizard~~ | Authored imports with pinned MIT provenance; wizard shell example shipped through Markdown resource. |
| compiler workflow emission | Retired by DR 049: the compiler is gone, so there is no workflow discovery, emission, or summary. Roles are native `SKILL.md` folders. |
| ~~types.json~~ | Inspected: document vocabulary already has inquisition and no workflow; Linear's ten Types are a separate standard. |
| dydo init | Deferred to DYD-86: native nesting depth and host setup proof. |
| ~~working-tree contract~~ | Authored/local twin: captain's Merge at each level, branch exceptions, release and merge-commit landing. |
| getting-started | Deferred to DYD-91: framework setup checklist and template registration. |
| ~~dydo.json~~ | Removed in source: no model or effort property remains; the delegating role chooses both at dispatch (DYD-134). |
| ~~work-model, task-lifecycle, dydo-glossary~~ | Authored/local prose: current flow, Types, release, Questions, inquisition and supersymmetry. |
| generated skills, agents, framework hashes and host reflection | Retired by DR 049: roles are native `SKILL.md` folders, edited directly; no compiler, hashes, or generated agent files remain. |

## Related

- [Work Model](./work-model.md) - the flow map, officers and crew, reviews and inquisition this map expands
- [Linear Ticket Lifecycle](./task-lifecycle.md) - what a ticket carries and how it is claimed and merged
- [Working-Tree Contract](../guides/working-tree-contract.md) - branches, worktrees, hops, cleanup
- [Linear Workspace Standard](../reference/linear-workspace-standard.md) - statuses, Types, Mode, templates
- [dydo Glossary](../reference/dydo-glossary.md) - the locked vocabulary
- [DR 047](../project/decisions/047-supersymmetry-hop-statuses-merge-issues-and-the-release-protocol.md) - the decisions this map draws
