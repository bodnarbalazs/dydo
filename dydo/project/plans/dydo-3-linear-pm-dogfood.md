---
title: dydo 3.0 Linear PM Dogfood and Acceptance
status: reviewed
area: project
type: context
linear-project: https://linear.app/bodnar-balazs/project/dydo-30-dogfood-and-accept-linear-pm-0a31eceeb1c2
---

# dydo 3.0 Linear PM Dogfood and Acceptance

This Project proves the new operating model through three migrated product Issues and one integrated
audit/assimilation Issue. Linear owns the live Project, Issue dependencies, current review evidence,
and human-attention routing; Git/dydo owns this reviewed contract, the product changes, immutable proof,
and the assimilation brief. No implementation Issue is sharpened or moved into this Project until this
plan is independently reviewed, merged, and available at an exact commit permalink.

## 1. Specification

### Intent

Demonstrate that representative multi-Issue delivery can move from reviewed intent through dependencies,
isolated implementation, independent Issue reviews, GitHub pull requests carrying explicit Issue keys,
an integrated audit, durable assimilation, and an explicit human accept/reject decision without a
repository/Linear mirror or watchdog. The smallest meaningful pilot uses existing unprojected work:
[DYD-42 — enforce read-only capability for Codex worker roles](https://linear.app/bodnar-balazs/issue/DYD-42/enforce-read-only-capability-for-codex-worker-roles),
[DYD-36 — implement the settled agent auto-memory routing policy](https://linear.app/bodnar-balazs/issue/DYD-36/implement-the-settled-agent-auto-memory-routing-policy),
and [DYD-38 — make file nudges apply to workers by audience](https://linear.app/bodnar-balazs/issue/DYD-38/make-file-nudges-apply-to-workers-by-audience).

DYD-42 runs first so the later independent Codex reviews exercise an actually read-only reviewer. DYD-36
and DYD-38 then run in parallel on disjoint files. A new integration Issue runs only after all three
product Issues are reviewed, merged, and read back from Linear.

### Fixed identities and baseline

| Authority | Exact identity |
|---|---|
| Project 4 | `c8ae27c3-5391-453a-8498-e02c064aa6ae` — [dydo 3.0 / Dogfood and accept Linear PM](https://linear.app/bodnar-balazs/project/dydo-30-dogfood-and-accept-linear-pm-0a31eceeb1c2) |
| Dydo team | `caa6ccbf-4f9b-477e-826c-a51ed43b0687` |
| Project-3 completion base | `89d38089fdfd1404e1cdd5127dcf10d3718d7287` — merge of PR 23, with Project-3 Issue review PASS, integrated audit PASS, CI PASS, 474/474 manifest rows applied, and 134/134 modules covered |
| Project 5 | `2ecbc168-2a42-482d-9b1b-a29537985ca5` — [dydo 3.0 / Remove Notion runtime and release](https://linear.app/bodnar-balazs/project/dydo-30-remove-notion-runtime-and-release-54b8939d748e) |
| Product Issues | `DYD-42`, `DYD-36`, and `DYD-38`, all currently unprojected Dydo `Todo` Issues |
| Needs-human label | `a1604e47-3e58-4aee-81c4-ef0e51c63638` — temporary attention marker, not a work type or acceptance state |

Every implementation branch starts from an exact merged predecessor, never from a movable local branch.
The planning baseline and first governing implementation base are the Project-3 merge above.

### In scope

- Publish this independently reviewed Project plan and, only after merge, attach its exact commit
  permalink to Project 4 and every implementation Issue.
- Sharpen and move the three existing migrated Issues into Project 4; create exactly one new integration,
  audit, and assimilation Issue after an exact-name/Project read-back proves it does not already exist.
- Make Codex read-only worker roles emit the supported native read-only sandbox setting and prove both
  generated configuration and live spawned-reviewer behavior.
- Complete DR-038's repository policy: scaffold the memory-routing paragraph and compile the
  chief-of-staff memory-sweep duty into both supported runtimes.
- Add explicit file-nudge audience semantics so worker calls receive applicable nudges while the
  managers-doctrine nudge remains manager-only.
- Exercise one genuine Needs-human route already present in DR-038: the human explicitly authorizes,
  defers, or rejects the one-time local memory sweep before DYD-36 closes. Deferral or rejection is a
  valid disposition and authorizes no external write or deletion.
- Review every implementation Issue independently, link exact review verdicts and gate evidence from
  Linear, integrate only passing work, run a fresh integrated audit, and author a proportionate
  assimilation brief.
- Present the complete evidence packet to the human for an explicit operating-model acceptance or
  rejection. Only that human decision satisfies the terminal gate.

### Out of scope

- DYD-37 and DYD-39 through DYD-41; they remain unprojected `Todo` Issues.
- Any Notion adapter, generic sync, watchdog, token/vault, configuration, command, test, documentation,
  packaging, or release removal owned by Project 5.
- Starting, polling, reconciling, mutating, or deleting remote Notion state. The stopped compatibility
  runtime remains present and unused.
- `v3.0.0`, release work, and the main-project adoption playbook.
- A dydo Linear client, repository-to-Linear synchronization, webhook receiver, cache, polling loop,
  watchdog, repository router, saved-view dependency, team-prefix routing convention, Linear-managed
  coding environment, or Linear-managed-agent instruction surface.
- Treating AFK/HITL as Issue kinds, workflow states, acceptance labels, or saved-view requirements.
  They describe whether producing work needs live human participation. Project 4 uses ordinary Issue
  dependencies and the temporary `Needs human` label only when a concrete decision is actually pending.
- The initial memory sweep unless the human explicitly authorizes its exact external targets and actions.
  Repository implementation never implies permission to inspect, rewrite, or delete a personal store.
- Creating dummy work, duplicating the selected Issues, or importing any additional migrated Issue merely
  to make the pilot look broader.

### Acceptance criteria

1. The Project plan is independently reviewed PASS, merged, and attached to Project 4 and all four
   implementation Issues by exact commit permalink before implementation begins.
2. Project 4 contains exactly the three selected existing product Issues plus one newly created
   integration/audit Issue. All belong to the fixed Dydo team and have the dependency graph in section 4.
3. DYD-42 proves that generated read-only Codex roles include `sandbox_mode = "read-only"`, writable
   roles do not receive a hard-coded sandbox override, the emitted TOML remains parseable, and a live
   spawned read-only reviewer can read but cannot create a canary file in an otherwise writable fixture.
4. DYD-36 proves the generic entry-point template contains DR-038's exact routing paragraph, both authored
   chief-of-staff templates contain the route/retire/keep sweep duty, and isolated compilation emits the
   duty into both runtime skill surfaces. The repository's existing `CLAUDE.md` routing paragraph remains
   byte-identical.
5. DYD-36 records one explicit human decision on the first local memory sweep. While awaiting that one
   decision it alone carries `Needs human`; after the decision the label is removed. No AFK/HITL label or
   saved view is required. If accepted, execution uses the human-approved exact targets; if deferred or
   rejected, no external state changes and the disposition is recorded.
6. DYD-38 accepts `all`, `manager`, and `worker` as the only file-nudge audiences; omission means `all` for
   backward compatibility. Direct manager calls evaluate `all`/`manager`, direct worker calls evaluate
   `all`/`worker`, Bash nudges remain audience-independent, the managers-doctrine default is `manager`, and
   validation rejects an audience on a Bash nudge or any unknown value.
7. Every Issue has an isolated branch, an exact reviewed head, a fresh independent Issue-review PASS, its
   proportional gates, a GitHub pull request whose title or head contains that Issue key, passing CI, a
   human/coordinator merge, and Linear read-back of the resulting state/attachment. No agent self-merges.
8. A second `dydo sync` is byte-idempotent. The actual repository-generated chief-of-staff skills and
   read-only Codex agent files match their authored sources, while generated files unrelated to the three
   product changes remain byte-identical to the integration base.
9. The integrated branch passes `dydo validate`, `dydo check`, the focused tests, the isolated full suite,
   forced coverage, scope checks, and `git diff --check`. A fresh auditor returns PASS against this exact
   plan and combined diff.
10. The assimilation brief records the four Issue identities, exact governing/implementation/review/merge
    evidence, dependency transitions, the Needs-human decision and outcome, observed friction with one
    disposition per item, generated-artifact parity, gate results, Project-5 non-participation, and the
    final human acceptance/rejection evidence.
11. Before the terminal decision, Project 5's DYD-7 through DYD-11 remain in their pre-pilot states and no
    Project-5-owned runtime path changes. Project 5 release/removal work remains stopped regardless of its
    historical Project shell status.
12. The human receives a plain-language evidence packet after implementation and integrated audit and
    explicitly accepts or rejects the operating model. Until that response is recorded, Project 4 cannot
    complete and Project 5 cannot resume. Rejection also keeps Project 5 stopped; remediation requires a
    separately reviewed contract.

### Questions and answers

- **Why these three migrated Issues?** They are real, already human-ratified product work and jointly
  exercise shared Codex/Claude compilation, host-native permissions, universal guard behavior,
  dependencies, parallel file-disjoint implementation, and a natural human-attention route. Adding a
  fourth product Issue would increase surface without proving a new Project-4 property.
- **Why does DYD-42 run first?** Current Codex reviewer prose says read-only but its agent TOML does not
  enforce that capability. Landing DYD-42 first lets DYD-36, DYD-38, and the integrated audit dogfood the
  corrected mechanism.
- **What is the Codex mechanism?** Current official documentation defines project custom-agent TOML files
  as configuration layers, explicitly supports `sandbox_mode`, and demonstrates read-only agents with
  `sandbox_mode = "read-only"`. The implementation emits that field only when authored role frontmatter
  has `read-only: true`; writable roles inherit the parent runtime policy. If the documented schema changes
  before execution, DYD-42 stops for a reviewed plan amendment rather than inventing a fallback.
- **Does a prose instruction count as read-only proof?** No. The unit gate checks emitted TOML and the live
  canary proves host enforcement. Prose remains defense in depth, not the capability boundary.
- **Is the first memory sweep mandatory?** A human decision is mandatory; execution is not. DR-038 already
  makes first-sweep deletion human-gated. `accept`, `defer`, and `reject` are honest outcomes, and only an
  explicit acceptance authorizes the exact external actions named by the human.
- **May the Project use AFK/HITL labels?** No requirement depends on them. The terms remain participation
  descriptors in durable vocabulary. `Needs human` is applied only to a currently blocked Issue and is
  removed after the decision.
- **Where does live review status live?** Linear. Git stores the reviewed code/plan and durable evidence;
  neither side mirrors the other's fields.
- **How are PRs connected?** Explicit `DYD-<number>` keys in branch/PR identity let the installed GitHub
  integration attach review/merge evidence. Team prefix does not choose a repository.
- **What happens on human rejection?** Record the rejection and reasons in Linear and the assimilation
  brief, keep Project 4 incomplete or rejected as the human directs, and keep Project 5 stopped. Do not
  reinterpret rejection as acceptance or generate remediation scope automatically.
- **Who merges?** The human or coordinator. Implementers, reviewers, and the integrated auditor never
  self-merge.

## 2. Prior art

- [DR-044](../decisions/044-linear-canonical-pm-and-dydo-knowledge-boundary.md) is adopted: Linear owns
  volatile work and attention; Git/dydo owns durable knowledge, reviewed contracts, audits, and
  assimilation. Its HITL/AFK rule is retained as a production-mode description, not a PM taxonomy.
- [The reviewed five-Project migration plan](./dydo-3-linear-migration.md) is adopted: Project 4 requires
  representative multi-Issue work, dependencies, explicit Issue participation, independent reviews, an
  integrated Project audit, assimilation, and human acceptance while Notion remains frozen.
- [The Project-2 plan](./dydo-3-linear-native-work-model.md),
  [evidence](../migrations/3.0-linear-work-model.md), and
  [assimilation](../migrations/3.0-linear-work-model-assimilation.md) established the reviewed-intent and
  native Linear/Git boundary. Their lane review and integrated-audit pattern is adopted; their old
  one-time migration mechanics are not copied.
- [The Project-3 plan](./dydo-3-v2-corpus-migration.md) and
  [assimilation](../migrations/3.0-v2-corpus-migration-assimilation.md) prove that DYD-36 through DYD-42
  are the seven exact human-ratified live migrations. Project 3 deliberately created them unprojected
  and did not implement them. Project-3 merge `89d38089fdfd1404e1cdd5127dcf10d3718d7287`
  is therefore the exact pilot base.
- [Corrected DYD-1 — verify Dydo Linear/GitHub PM references](https://linear.app/bodnar-balazs/issue/DYD-1/verify-dydo-lineargithub-pm-references)
  is adopted: explicit Issue keys attach PRs; labels are nonbinding workspace conveniences; saved views,
  team-prefix repository routing, Linear Agent guidance, and managed coding environments are unrelated.
- [DR-038](../decisions/038-auto-memory-policy.md) is adopted exactly. The repository already contains
  the routing paragraph in `CLAUDE.md`; the migrated Issue exists because the generic entry-point template
  and chief-of-staff sweep methodology remain incomplete.
- The frozen source for DYD-38 at Project-3 recovery commit
  `ffffc02dcdf92b9677d0eb4f522d1af57a869990` is adopted only for the settled audience behavior. Its
  obsolete worker-identity language is rejected; current guard doctrine is universal off-limits,
  dangerous-command, and nudge enforcement without dydo-managed agent identity.
- The frozen source for DYD-42 correctly rejected Claude tool-list syntax in Codex TOML but left live
  behavior open. Current official [Codex configuration reference](https://learn.chatgpt.com/docs/config-file/config-reference)
  and [custom-agent documentation](https://learn.chatgpt.com/docs/agent-configuration/subagents) close
  that question: custom-agent files accept ordinary session settings including `sandbox_mode`, and the
  official read-only examples use it. No workflow-dispatch fallback is needed.
- Project 5's reviewed removal plan is not prior-art implementation for this Project. Its paths and
  commands are a negative boundary only.

## 3. Design

### DYD-42 — native read-only Codex roles

`Commands/SyncCommand.cs` already derives Claude tools and both runtime stance text from
`RoleDefinition.ReadOnly`. Extend `BuildCodexAgent` at that same decision point: emit one TOML line
`sandbox_mode = "read-only"` immediately after `model` for read-only roles and emit no sandbox line for
writable roles. Do not emit `workspace-write`, approval policy, permission profiles, writable roots, or
Claude tool names. Parent/session policy continues to own writable capability and approvals.

Focused tests in `DynaDocs.Tests/Commands/SyncCommandTests.cs` must cover a read-only reviewer, a writable
code writer, project-override role discovery, TOML quoting/newline shape, and repeat compilation. The
live proof uses the newly compiled project reviewer in an otherwise writable disposable fixture: it
must read a canary source, attempt to create a uniquely named sibling canary, receive host denial, and
leave no created file. The review evidence records the runtime/client version, role file hash, exact
attempt, and denial; it never weakens the parent sandbox to manufacture a result.

### DYD-36 — memory routing and the genuine human route

Add DR-038's existing paragraph verbatim to `Templates/entry-point.template.md`; do not rewrite the
already-correct root `CLAUDE.md`. Add one concise `Memory sweep` subsection to both authored
chief-of-staff sources, `Templates/mode-chief-of-staff.template.md` and
`dydo/_system/templates/mode-chief-of-staff.template.md`. It says:

1. inspect only a memory store the human has explicitly placed in scope;
2. classify each entry `route`, `retire`, or `keep` using DR-038;
3. ask for human authorization before the first sweep changes or deletes external state;
4. in later authorized sweeps, report dispositions in the status summary;
5. route project facts to the appropriate durable dydo artifact or live Linear Issue, never to a new
   repository PM record.

`DynaDocs.Tests/Services/TemplateGeneratorTests.cs` proves exact paragraph scaffolding and one occurrence.
A new `DynaDocs.Tests/Commands/ChiefOfStaffSyncTests.cs` proves isolated `dydo sync` compilation into
both runtime skill paths without turning chief-of-staff into a spawnable agent.

After repository gates and independent review pass, DYD-36 receives a decision packet naming the exact
known sweep proposal, external targets, reversible/irreversible actions, and recommendation. If no
current proposal or target can be established without reading out-of-scope personal state, recommend
`defer`; do not ask for broad access. Apply `Needs human` only while awaiting the response. Record the
human's exact `accept`, `defer`, or `reject` response and remove the label before closeout.

### DYD-38 — audience-aware file nudges

Add optional JSON property `audience` to `Models/NudgeConfig.cs`. Omission deserializes to `all` and
serialization omits the field only when its effective value is `all`; accepted case-insensitive input
values are `all`, `manager`, and `worker`, while saved output is lowercase. This is execution audience,
not PM metadata.

`Commands/GuardCommand.cs` passes the direct-call audience into `CheckFileNudges`: the manager lane
evaluates `all`/`manager`, and `HandleWorkerCall` evaluates `all`/`worker` after the universal off-limits
check. Native memory remains exempt from off-limits enforcement but not from an explicitly matching
file nudge. Bash-command nudges continue through `CheckNudges` and ignore audience because validation
forbids `audience` when `tools` is absent.

`Services/ConfigFactory.cs` marks only the managers-doctrine file nudge `manager`, preserves/copies the
field in default creation and update, and does not rewrite custom nudges. `Services/ValidationService.cs`
rejects unknown audience values and any audience on a Bash nudge. Tests cover default compatibility,
manager-only suppression for workers, worker-only suppression for managers, `all` on both lanes, block/
warn/notice behavior, memory-path matching, validation, and serialized default configuration.

### Integration, assimilation, and acceptance

The integration Issue starts from the three exact human-merged product commits. It runs `dydo sync`
once to update only the compiled chief-of-staff skills and read-only Codex agents, runs it a second time
to prove a zero diff, updates the active managers-doctrine nudge in `dydo.json`, and refreshes only
framework hashes made stale by the two authored template changes through the supported template-update
workflow. Any unrelated generated delta stops integration.

The integration owner creates
`dydo/project/migrations/3.0-linear-pm-dogfood-assimilation.md` and updates only the migrations index
needed to reach it. The brief uses exact permalinks and hashes for durable evidence but does not copy
Linear workflow fields. Its friction register gives every observed item exactly one outcome:

- fixed inside an already reviewed Issue contract;
- documented as expected behavior/no change with evidence;
- deferred with a named existing Issue; or
- proposed to the human for a separately authorized Issue.

No friction item silently expands the Project. Recurring harness friction invokes self-improvement only
to select the smallest justified route; it does not authorize a same-Project implementation.

After gates and the fresh integrated audit PASS, the owner posts one plain-language decision packet to
Project 4 and applies `Needs human` to the integration Issue. The packet names what the pilot proved,
limitations, friction dispositions, Project-5 consequence, and two explicit outcomes: `accept` or
`reject`. The human's response is copied by stable comment link and plain-language meaning into the
assimilation brief; a narrow fresh documentation review verifies that acceptance seal. The label is then
removed. No agent interprets silence, a merge, CI, or audit PASS as acceptance.

### Hazards and rollback

- **Permission false positive:** prose compliance is not sandbox enforcement. The canary must prove a
  denied write and absence of the target file.
- **Permission overreach:** hard-coding `workspace-write` on writable roles could widen or conflict with
  parent policy. Omission is the required behavior.
- **Generated-file collision:** DYD-36 changes authored methodology while DYD-42 changes the compiler.
  Product Issues test isolated output but do not commit shared generated surfaces; the integration Issue
  is their sole writer.
- **Config hot file:** DYD-38 changes default generation, but only integration edits this repository's
  `dydo.json`. This avoids branch collision and gives the integrated guard proof one owner.
- **Personal-state deletion:** no branch rollback can restore a personal memory store. Nothing external
  changes without exact human authorization and a recovery statement.
- **Linear partial mutation:** post-merge provisioning records every returned/update result immediately,
  then reads back by fixed ID. On uncertainty, stop and query those IDs; never retry creation by title.
- **Rejected operating model:** preserve the rejection evidence, do not complete Project 4, and do not
  start Project 5. Remediation begins only from newly reviewed intent.
- **Repository rollback:** before merge, abandon the isolated branch; after merge, use an ordinary revert
  of that Issue's exact merge commit. Never reset shared history.

## 4. Implementation Issue map

| Issue | Outcome | Files touched (disjoint) | Blockers | Gate |
|---|---|---|---|---|
| DYD-42 — enforce read-only capability for Codex worker roles | Read-only Codex role TOML and live denial proof | `Commands/SyncCommand.cs`; `DynaDocs.Tests/Commands/SyncCommandTests.cs` | reviewed plan merged | focused `SyncCommandTests`; isolated emit/parsing; live read/write canary; independent code review |
| DYD-36 — implement the settled agent auto-memory routing policy | Generic routing paragraph, compiled sweep duty, and one explicit first-sweep human disposition | `Templates/entry-point.template.md`; `Templates/mode-chief-of-staff.template.md`; `dydo/_system/templates/mode-chief-of-staff.template.md`; `DynaDocs.Tests/Services/TemplateGeneratorTests.cs`; new `DynaDocs.Tests/Commands/ChiefOfStaffSyncTests.cs` | DYD-42 merged and read back | focused template/sync tests; source/output occurrence predicates; Needs-human decision evidence; independent code/docs review |
| DYD-38 — make file nudges apply to workers by audience | Backward-compatible audience semantics across direct manager/worker calls | `Models/NudgeConfig.cs`; `Commands/GuardCommand.cs`; `Services/ConfigFactory.cs`; `Services/ValidationService.cs`; `DynaDocs.Tests/Integration/GuardWorkerLaneTests.cs`; `DynaDocs.Tests/Commands/GuardCommandTests.cs`; `DynaDocs.Tests/Services/ConfigFactoryTests.cs`; `DynaDocs.Tests/Services/ValidationServiceTests.cs` | DYD-42 merged and read back | focused guard/config/validation tests; `dydo validate` fixture; independent code/tests review |
| P4-4 — integrate, audit, assimilate, and request human acceptance (new after plan merge) | Exact generated surfaces, combined gates, friction dispositions, assimilation, integrated audit, explicit accept/reject | `.agents/skills/chief-of-staff/SKILL.md`; `.claude/skills/chief-of-staff/SKILL.md`; `.codex/agents/reviewer.toml`; `.codex/agents/inquisitor.toml`; `dydo.json`; new `dydo/project/migrations/3.0-linear-pm-dogfood-assimilation.md`; `dydo/project/migrations/_index.md` | DYD-42, DYD-36, DYD-38 merged, CI green, reviewed, and read back | all section-6 gates; fresh integrated audit; explicit human decision; narrow acceptance-seal review |

The integration Issue may resolve textual conflicts only inside its seven owned paths. A generated delta
outside the four listed compiled artifacts or a required edit outside the table is a plan finding, not
integration discretion.

## 5. Ordering and isolation

### Plan publication and post-merge Linear transaction

1. Independently review this plan, address every finding, run the proportional documentation gates,
   commit and push `codex/P4-plan`, open a PR, wait for CI, and request coordinator/human merge. Do not
   self-merge and do not mutate Project-4 implementation Issues before the merge exists.
2. Resolve the exact plan merge commit from `origin/master`. Read back Project 4 by fixed ID and assert
   the fixed Dydo team, Project URL, zero pre-existing Project Issues, and no exact-name integration Issue.
3. Update Project 4's stale sentence `Exercise AFK/HITL routing and Issue dependencies` to
   `Exercise Issue dependencies plus one real autonomous path and one concrete Needs-human decision;
   AFK/HITL are participation descriptors, not work types or acceptance labels.` Attach the exact plan
   permalink as a Project resource.
4. Update DYD-42, DYD-36, and DYD-38 in that order. Preserve their titles and migration provenance;
   replace their sparse meaning with the exact outcome, owned paths, acceptance criteria, gates, branch/
   PR key rule, evidence requirements, and governing plan permalink from this contract. Assign each to
   Project 4 and the fixed Dydo team. Set DYD-36 and DYD-38 `blockedBy: DYD-42`.
5. Search exact title `P4-4 — Integrate, audit, assimilate, and request human acceptance` in Project 4.
   If absent, create exactly one Issue with the table's contract, set it blocked by all three product
   Issues, and record the returned ID/URL immediately. If any unrecorded match exists, stop; title match
   alone is not adoption authority.
6. Read back all four Issues with relations, attachments, Project/team IDs, and state. Publish one roster
   comment on Project 4 containing titles, meanings, IDs/URLs, dependencies, and exact plan permalink.
   Only then may DYD-42 begin.

### Delivery sequence

1. DYD-42 runs alone in `codex/DYD-42` from the exact plan merge. A fresh reviewer receives the Issue,
   governing plan commit, exact diff, focused gates, and live canary transcript. After PASS, push, open a
   key-bearing PR, wait for CI, and await human/coordinator merge. Read back the PR attachment and Linear
   state before unblocking later work.
2. DYD-36 and DYD-38 branch from the exact DYD-42 merge into separate worktrees and run in parallel.
   Their source/test sets are disjoint. Each receives a separate fresh independent review and separate
   key-bearing PR/CI/human merge cycle.
3. DYD-36's repository branch may be ready while its human decision is pending. At that point apply
   `Needs human`, post the bounded sweep decision packet, and stop only that Issue's closeout. DYD-38 may
   continue. Remove the label after the exact response is recorded; do not equate the response with final
   Project acceptance.
4. Create the P4-4 branch only after all three product merge commits and Linear read-backs are fixed.
   Integrate from `origin/master`; do not cherry-pick unmerged work. Run generated-artifact parity,
   proportional/full gates, assimilation, and the fresh independent integrated audit.
5. After integrated PASS, present the terminal accept/reject packet. The human decision controls Project
   completion and Project-5 readiness. Seal that exact decision in assimilation, obtain the narrow
   documentation review, push the final P4-4 commit, open its key-bearing PR, wait for CI, and await
   human/coordinator merge.
6. On acceptance and final evidence merge, read back the four Issues and Project. The human/coordinator
   may complete Project 4. On rejection, preserve evidence and keep Project 4 and Project 5 non-executing
   until a separately reviewed remedy is accepted.

### Shared hot files

- Only P4-4 writes generated runtime artifacts and `dydo.json`.
- Only the planning branch writes this plan and `dydo/project/plans/_index.md`; P4-4 must not amend the
  governing plan to fit execution.
- Only P4-4 writes the assimilation brief and migrations index.
- No Issue writes any Project-5-owned path. The integration scope predicate compares the combined diff
  to the exact union in section 4 and fails on every extra path.

## 6. Gates and evidence

### Per-Issue gates

Every product Issue runs:

```powershell
dotnet build DynaDocs.sln -c Release --no-restore
git diff --check
```

DYD-42 additionally runs:

```powershell
dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~SyncCommandTests"
```

The isolated emit predicate asserts reviewer/inquisitor TOML contains exactly one read-only sandbox
line, writable role TOML contains none, every emitted file is parseable by the installed runtime, and a
second emit is byte-identical. The live canary protocol in section 3 is required evidence, not a unit-test
substitute.

DYD-36 additionally runs:

```powershell
dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~TemplateGeneratorTests|FullyQualifiedName~ChiefOfStaffSyncTests"
```

Assert the DR-038 paragraph occurs exactly once in the generic entry template and unchanged root
`CLAUDE.md`; the sweep duty occurs exactly once in each authored chief-of-staff source and each isolated
compiled skill; no chief-of-staff agent file is emitted.

DYD-38 additionally runs:

```powershell
dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~GuardWorkerLaneTests|FullyQualifiedName~GuardCommandTests|FullyQualifiedName~ConfigFactoryTests|FullyQualifiedName~ValidationServiceTests"
dydo validate
```

The tests must cover all audience/lane combinations and severities, unknown/misplaced validation,
omitted-field compatibility, case normalization, system-nudge merging, and the native-memory nudge case.

### Integrated repository gates

P4-4 runs from the exact three-merge base:

```powershell
dotnet build DynaDocs.sln -c Release --no-restore
dydo sync
git diff --check
dydo validate
dydo check
py DynaDocs.Tests/coverage/run_tests.py
py DynaDocs.Tests/coverage/gap_check.py --force-run
```

After the first `dydo sync`, record hashes for the four expected generated artifacts, run `dydo sync`
again, and require `git diff --exit-code` relative to the post-first-sync tree. Run the three focused
filters again before the full suite. The exact combined file set must equal the section-4 union plus this
plan/index from the already-merged planning commit; no `Sync/**`, `DynaDocs.Tests/Sync/**`, Notion,
watchdog, token/vault, package, or release path may appear.

### Linear and GitHub read-back

Before integrated audit, read back and attach evidence that:

- all four Issues belong to Project 4 and the fixed Dydo team;
- DYD-42 blocks DYD-36 and DYD-38, and all three block P4-4;
- every product Issue has one exact independent PASS, gate transcript, reviewed head, key-bearing PR,
  passing CI, human merge commit, and expected Linear attachment/state transition;
- `Needs human` appeared only for a concrete DYD-36 decision and the terminal P4-4 decision, and was
  removed after each response;
- AFK/HITL labels and saved views were not workflow dependencies;
- GitHub attachment worked through explicit Issue keys, with no repository routing field or machinery;
- Project 5 DYD-7 through DYD-11 retain their pre-pilot states and no Project-5 product path changed.

### Review and terminal gate

Give a fresh independent auditor the exact reviewed plan commit, Project-3 base, combined diff, four
Issue contracts, all Issue-review verdicts, merge/CI/read-back evidence, generated hashes, Needs-human
transcript, friction register, and gate transcripts. PASS means the demonstrated result is eligible for
human acceptance; it does not accept the model.

The terminal packet asks the human to respond explicitly with acceptance or rejection and why. Silence,
an emoji, Issue completion, PR merge, CI, or agent verdict is not an answer. Project 4 remains incomplete
until that decision is recorded and sealed. Project 5 remains stopped unless the human accepts.

## 7. Watch-outs

- Do not create or edit the four Linear Issue contracts from an unmerged plan branch.
- Do not replace exact commit permalinks with branch-following URLs in governing Issue attachments.
- Do not hand-edit `.agents/`, `.claude/`, or `.codex/` generated artifacts in product Issues.
- Do not add a Codex `tools` list, a hard-coded writable sandbox, an approval override, or a custom
  permission framework.
- Do not let the active managers-doctrine nudge disappear while moving its audience into configuration.
- Do not apply `Needs human` pre-emptively or leave it after the named decision is answered.
- Do not inspect or mutate a personal memory store merely to make the human-intervention route pass.
- Do not create a friction Issue without human authorization; a durable disposition is enough when no
  action is justified.
- Do not run any retained Notion command or watchdog during the pilot.
- Do not mark Project 4 complete because the implementation PRs merged. The integrated audit,
  assimilation, and explicit human accept/reject decision are independent gates.
- Do not resume Project 5 after rejection or before the exact accepted Project-4 evidence is available.
