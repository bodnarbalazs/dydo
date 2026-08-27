---
title: dydo 3.0 Linear PM Migration
status: plan-review
area: project
type: context
linear-project:
---

# dydo 3.0 Linear PM Migration

Reviewed rolling-wave plan for establishing the Linear work graph, freezing the v2 PM corpus safely,
and preparing the bounded implementation Projects that deliver dydo 3.0.

## 1. Specification

### Intent

Move dydo's live project management into the existing Linear `Dydo` team without recreating the
Notion mirror. Establish five bounded, team-scoped `dydo 3.0 / …` Projects, fully plan only the first
Project, and use it to prepare a lossless Notion freeze, a complete v2 record
disposition, and the reference contract every later implementation Issue follows.

The migration must leave the human with one coherent work graph, preserve durable knowledge and
historical evidence, and make subsequent source deletion, methodology changes, corpus cleanup, and
release work independently reviewable and assimilable.

### In scope

- Create the reviewed five-Project graph described below in workspace `LC`, team `Dydo`, without a
  workspace Initiative.
- Keep Linear canonical for volatile PM and the repository canonical for durable knowledge and proof.
- Define exact two-way references without content synchronization.
- Inventory every legacy PM record and every incoming retained-doc reference to it.
- Produce a one-time disposition manifest covering every legacy record.
- Human-ratify all ambiguous live-work dispositions before importing them.
- Perform a final Notion reconcile and prove a clean canonical baseline before deleting sync code.
- Preserve the complete v2 PM corpus at an exact pushed commit, with protected annotated
  `pm-v2-final` as its human-readable alias.
- Create detailed Linear Issues only for the first Project; later Projects remain low-resolution until
  their own plan gate.
- Retain FutureFeature as a repo-native idea record and normalize its schema without promoting ideas.

### Out of scope

- Implementing the later Projects in this plan.
- Creating a permanent Linear client, sync daemon, schema provisioner, or token store in dydo.
- Bulk-importing completed Sprints, Slices, resolved Issues, stale Tasks, or changelog history.
- Deleting or archiving remote Notion data.
- Migrating the main project before the DynaDocs dogfood passes.
- Enabling Cycles before real accepted-increment throughput exists to calibrate them.
- Depending on Linear Releases; Git tags remain release truth.

### Acceptance criteria

1. Linear contains exactly the five bounded `dydo 3.0 / …` Projects specified below, all assigned only
   to the `Dydo` team and ordered by the declared dependencies. No workspace Initiative is created.
2. Only Project 1 has detailed execution Issues at creation time; Projects 2–5 use the
   low-resolution scope, acceptance, dependency, and resource contracts in this plan without a
   speculative issue breakdown.
3. Project 1's repo plan is a Linear Project resource, and its frontmatter carries the created Linear
   Project URL after provisioning.
4. Every Project 1 Issue has a `Governing context` section plus Linear link attachments for its exact
   applicable DRs/docs; implementation contracts also record repository, integration target,
   prerequisites, owned paths, gates, and a governing commit before execution begins.
5. A generated disposition manifest names every non-meta file under the legacy Campaign, Sprint,
   Slice, Task, Issue, backlog, and Release paths exactly once, with one disposition and a resolvable
   target/evidence reference where required.
6. Every reference from a retained `master` document to a record leaving `master` is rewritten to a
   retained durable artifact, a Linear URL, or an exact freeze-commit GitHub permalink.
7. The 61 stale Tasks are not imported. Completed execution history is not imported. Open/ambiguous
   work enters Linear only after a human-approved manifest disposition.
8. All FutureFeature records remain in the repo; none is created in Linear unless the human separately
   promotes it.
9. A final manual `dydo notion sync --docs` is at equilibrium across both the PM spine and docs
   mirror; pending writes and both conflict-shadow directories are empty or explicitly resolved; the
   resulting canonical tree is committed and tagged `pm-v2-final` before any Notion source deletion
   begins.
10. No migration action deletes remote Notion content or local rollback evidence.
11. A fresh reviewer returns PASS on this plan and on the Project 1 Issue contracts before any Linear
    record is created.

### Questions and answers

- **Is Linear or Git canonical for live work?** Linear. Git owns durable knowledge and proof.
- **Do repo PM records mirror Linear?** No. Cross-references are stable links only.
- **Does FutureFeature migrate?** No. It remains an unscheduled repo idea until human promotion creates
  a new Linear Initiative, Project, or Issue.
- **Is this one oversized implementation unit?** No. It is a Git-governed portfolio of five Linear
  Projects. Only the current Project receives detailed Issues.
- **Do we create everything in Linear before planning?** No. After this plan passes, publish its
  governing commit, then bootstrap the five Project shells and only the reviewed Project
  1 Issues as one plan-provisioning action. Execution begins only after exact IDs/URLs are written back
  and read back successfully.
- **Where do specifications live?** An atomic Issue may contain its whole reviewed contract. A
  multi-Issue Project has one reviewed repo plan linked as a Linear Project resource.
- **How does a Linear Issue cite dydo knowledge?** It links the branch-following GitHub URL for current
  human navigation and records the exact governing commit when execution starts. DR number and title
  remain visible in the link label.
- **How does a repo plan link back?** One `linear-project` URL in frontmatter after Project creation.
  This is provenance, not synchronization.
- **What preserves deleted historical records?** A clean pushed pre-deletion commit, protected
  `pm-v2-final` alias, and disposition manifest. Retained docs use exact commit-SHA permalinks when the
  historical artifact remains evidentially relevant.
- **Do we use Cycles now?** No. Add them only after observed accepted-increment throughput makes the
  timebox meaningful.
- **What statuses mean ready for autonomous pickup?** `Todo` + `AFK` + no blocking relation. `Backlog`
  is unreviewed or unscheduled; `In Review` means independent agent review; `Done` requires the Issue's
  acceptance, while Project completion additionally requires integrated audit and assimilation.

## 2. Prior art and evidence

- [DR 044](../decisions/044-linear-canonical-pm-and-dydo-knowledge-boundary.md) owns the canonical
  boundary, retained FutureFeature, reviewed-intent gate, and 3.0 version.
- [Linear PM Pivot Campaign](../campaigns/linear-pm-pivot.md) owns the destination, Fog, and Project
  sequence.
- [DR 041](../decisions/041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md) established
  the still-valid boundary that dydo authors/knows while platforms run/coordinate.
- [DR 042](../decisions/042-plan-first-implementation.md) supplies the fresh plan gate and
  self-contained-contract standard; DR 044 replaces its mandatory Sprint/Slice packaging.
- Existing Notion decisions 025, 029, 030, 033, 035, and 043 document the state that must be frozen
  safely and are superseded by DR 044 rather than silently rewritten.
- Repository discovery found 15 Sprints, 50 Slices, 68 Tasks, 310 Issues, 26 backlog records, 3
  FutureFeatures, 46 Decisions, 670 changelog records, and 22 Inquisition reports. Status vocabulary
  is inconsistent across live records, so status alone is never migration authority.
- The Notion surface spans about 11.5k production lines across provider and generic sync plus 12.6k
  Notion-specific test lines. This supports deletion rather than adapter replacement.
- Linear's official MCP, Project resources, Issue links, parent/sub-issues, dependencies, delegation,
  and Agent Sessions cover the live-work graph. No missing capability currently justifies another
  sync engine.

## 3. Linear graph

### Portfolio boundary

This dogfood deliberately does not create a workspace Initiative. On the Basic plan, the available
Initiative is workspace-scoped and would mix dydo strategy into the main project's workspace layer.
The reviewed Git plan is the durable portfolio umbrella; the `Dydo` team and the common
`dydo 3.0 / …` prefix provide the live Linear grouping.

### Project map

| Order | Linear Project | Outcome | Depends on |
|---|---|---|---|
| 1 | dydo 3.0 / PM foundation and migration contract | Linear graph, link contract, complete disposition manifest, safe Notion freeze | — |
| 2 | dydo 3.0 / Adopt Linear-native work model | Glossary, docs, templates, skills, workflows, planner/reviewer/orchestrator contracts use DR 044 | 1 |
| 3 | dydo 3.0 / Migrate the v2 work corpus | Live work imported by ratified disposition; legacy runtime records removed; retained knowledge and links clean | 1, 2 |
| 4 | dydo 3.0 / Dogfood and accept Linear PM | Real work completes through the new Linear/repo boundary and the human accepts the operating model | 2, 3 |
| 5 | dydo 3.0 / Remove Notion runtime and release | Frozen Notion/generic sync/watchdog/token code and tests are deleted only after pilot acceptance; full gates pass; 3.0 ships with the main-project playbook | 1, 2, 3, 4 |

Project 2 establishes the target artifact shape before Project 3 rewrites or removes the old corpus.
Project 4 is the live acceptance boundary. Project 5 alone may delete the frozen Notion runtime and is
the release integration boundary.

Bootstrap status contract: all five Projects are `Planned`. Bootstrap first
reads and records the exact workspace Project status IDs/names for `Planned`, `In Progress`, `Completed`,
and `Canceled`. After verification passes and the human starts execution, Project 1 alone moves to
`In Progress` using the recorded ID. A later Project moves to `In Progress` only when its dependencies
and fresh plan gate have passed; it becomes `Completed` only after its integrated audit and assimilation
gate.

### Low-resolution Project contracts

These contracts are sufficient for shell creation. They are not implementation plans; each Project
receives a fresh reviewed repo plan and detailed Issues only when it becomes the current frontier.

#### Project 2 — Adopt Linear-native work model

Scope: replace Campaign/Sprint/Slice/Task language and contracts in the glossary, architecture/work-model
docs, templates, skills, and planner/reviewer/orchestrator workflows; retain FutureFeature; ensure final
Project audit receives the linked Project plan.

Acceptance: generated and installed framework surfaces agree with DR 044; no active workflow requires a
repo PM mirror; plan/review/audit gates and HITL/AFK conventions are executable; focused tests plus the
full documentation/template consistency gates pass.

Dependencies: Project 1. Resources: DR 044, this plan, and the Project 1 disposition manifest.

#### Project 3 — Migrate the v2 work corpus

Scope: apply the human-ratified manifest; create only approved live Linear work; normalize retained
FutureFeatures; remove retired PM records from the default branch; rewrite every incoming reference.

Acceptance: every manifest row has one proven outcome; created Linear work matches the approved preview;
all retained-default-branch links resolve; removed history remains available at `pm-v2-final`; `dydo
check` and corpus-focused tests pass.

Dependencies: Projects 1 and 2. Resources: DR 044, this plan, both manifest artifacts, and the
`pm-v2-final` tag.

#### Project 4 — Dogfood and accept Linear PM

Scope: execute at least one representative multi-Issue delivery through Linear using reviewed repo
context, dependencies, AFK/HITL routing, independent Issue reviews, integrated Project audit, and an
assimilation brief. The frozen Notion runtime remains present but stopped throughout the pilot.

Acceptance: the work reaches Project completion without a repo/Linear mirror or watchdog; references and
governing commits support implementation and audit; observed friction is dispositioned; the human
explicitly accepts or rejects the operating model.

Dependencies: Projects 2 and 3. Resources: DR 044, the Project 2 work-model plan, the migrated work graph,
and the pilot Project's reviewed plan.

#### Project 5 — Remove Notion runtime and release dydo 3.0

Scope: after recorded pilot acceptance, delete the Notion adapter, generic sync code with no remaining
consumer, watchdog/start paths, token/vault/config/CLI surface, obsolete tests and docs; update packages,
templates, release gates, migration guidance, and the main-project adoption playbook.

Acceptance: no product/runtime reference can start or configure Notion sync; no dead sync abstraction or
dependency remains; unit/integration/docs/template/AOT/package gates and final integrated audit pass;
version 3.0.0 is released from its protected annotated version tag and exact commit; the main-project
playbook is complete.

Dependencies: Projects 1–4, including explicit human acceptance of Project 4. Resources: DR 044, the
`pm-v2-final` tag, the accepted pilot evidence, and Projects 2–4 plans/audits.

### Project 1 — detailed Issue contracts

#### Bootstrap procedure, evidence, and rollback

Bootstrap is an approved plan-provisioning action, not an Issue pretending to create itself. After
PASS, the human first approves publishing the governing artifacts. The operator then:

1. pushes the exact governing commit and records its SHA/permalink;
2. resolves workspace `LC` and team `Dydo` and asserts team ID
   `caa6ccbf-4f9b-477e-826c-a51ed43b0687` through the official Linear connector;
3. searches/lists exact Project, Issue, and label names and stops on every pre-existing
   exact-name match unless that exact object ID is already recorded by this bootstrap's evidence from a
   prior interrupted attempt; an unrecorded singleton match is not adoption authority;
4. calls the official Linear connector's `save_project`, `create_issue_label`, and `save_issue`
   operations, always using returned IDs for Project membership and Issue `blockedBy` dependencies.
   Each initial Project description includes its exact incoming dependency set: Project 2 names Project
   1; Project 3 names Projects 1 and 2; Project 4 names Projects 2 and 3; Project 5 names Projects 1–4;
5. writes every request summary, returned ID/URL, and governing SHA to
   `dydo/project/migrations/3.0-linear-bootstrap.json` as each call succeeds;
6. calls `get_project(includeResources: true)`, `list_issue_labels`, and
   `get_issue(includeRelations: true)` by returned identity; verifies the
   connector-managed fields at this stage—team, recorded status ID/name, descriptions, Project links,
   labels, Issue Project membership, and Issue dependencies—and records the partial result;
7. compares the read-back `Depends on` sections to the expected incoming counts `1 + 2 + 2 + 4`, records
   all nine exact name/URL edges in the bootstrap evidence, and fails on a missing or extra edge. The
   current MCP surface cannot write native Project dependencies; native edges may be added manually
   later, but they are not bootstrap authority;
8. writes the Project 1 URL to plan frontmatter, pushes that change, and repeats connector Project
   resource read-back so every branch-following link resolves. This is the comprehensive bootstrap
   PASS/FAIL comparison and checklist.

Expected observable result: five Planned Dydo-only Projects, three labels, six Project 1 Issues, no
Initiative/Cycles/Releases, exact description-level Project dependencies and native Issue relations, and
`bootstrapVerification: "pass"` in the JSON plus a human-readable PASS checklist. Saved views are not
available through the connector: Issue 1 creates `Factory ready` and `Needs me` in the Linear UI and
records their URLs/checklist in the Markdown evidence.

On the first failed or mismatched mutation, stop and do not retry by title. Record the failure and all
returned IDs. Roll back by ID in reverse order: set created Issues and Projects to `Canceled` through
the corresponding `save_*` operations; delete only the three new labels through the Linear UI after
matching their recorded IDs. Read back every rollback state and
record `rollbackVerification: "pass"`. Never touch a pre-existing object or remote Notion data.

The plan bootstrap creates these six Issues only after PASS and after the governing artifacts are
available at a published commit. Every description carries this common execution envelope:

- **Repository:** `https://github.com/bodnarbalazs/dydo`.
- **Integration target:** the repository default branch (`master` at plan time).
- **Governing context:** branch-following links and Linear attachments for DR 044, this plan, and the
  Linear PM Pivot context; add the issue-specific authorities named below.
- **Governing commit:** the exact published commit containing the PASS plan, recorded as both SHA and
  commit permalink before an Issue can enter `Todo`.
- **Execution evidence:** branch/worktree/session and review links belong on the Issue; durable decisions
  and audit results return to the repository.

The following ownership and prerequisite map is part of each Issue contract, not optional bootstrap
metadata:

| Issue | Prerequisites | Exact owned paths | Additional governing context |
|---|---|---|---|
| 1 | Plan PASS and published governing commit | `dydo/project/decisions/044-linear-canonical-pm-and-dydo-knowledge-boundary.md`; `dydo/project/plans/dydo-3-linear-migration.md`; `dydo/project/campaigns/linear-pm-pivot.md`; serial first write to `dydo/project/migrations/3.0-linear-bootstrap.md` | DR 041 and DR 042 |
| 2 | Issue 1 accepted | `dydo/project/migrations/3.0-pm-records.json`; `dydo/project/migrations/3.0-pm-records.md`; optional temporary `dydo/project/migrations/build-3.0-pm-manifest.ps1` | `dydo.json`, current folder/schema model, DR 034, DR 040 |
| 3 | Issue 2 accepted | `dydo/project/migrations/3.0-pm-records.json`; `dydo/project/migrations/3.0-pm-records.md` | the generated manifest and its review checklist |
| 4 | Issue 2 accepted; human approves live sync | `dydo/project/migrations/3.0-notion-freeze.md`; manifest commit/tag fields only | DR 025, DR 043, `dydo/reference/notion-sync.md` |
| 5 | Issues 1, 3, and 4 accepted | `dydo/project/migrations/3.0-linear-bootstrap.json`; serial finalization of `dydo/project/migrations/3.0-linear-bootstrap.md` after Issue 1; Project 1 URL frontmatter in `dydo/project/plans/dydo-3-linear-migration.md` | Linear read-back of the bootstrapped Projects, labels, Issues, and links |
| 6 | Issue 5 accepted | `dydo/project/plans/dydo-3-linear-native-work-model.md` | DR 041, DR 042, ratified manifest, `pm-v2-final` freeze evidence |

#### Issue 1 — Establish the Linear reference and team convention contract

Participation: HITL. Initial state: `Todo`. Type: documentation/design.

Deliverable:

- Ratify the reference rules in §4.
- Retain the default Dydo workflow statuses.
- Verify and use only the bootstrap-created `HITL`, `AFK`, and `Needs human` labels; do not create them
  again and do not recreate Notion's property taxonomy.
- Define the `Factory ready` view as `Todo` + `AFK` + no blocker, and `Needs me` as `Needs human`
  plus all active `HITL` Issues assigned to the human.
- Record the convention in the new 3.0 work-model/glossary plan input, not as workspace-only lore.

Owned paths:

- `dydo/project/decisions/044-linear-canonical-pm-and-dydo-knowledge-boundary.md`
- `dydo/project/plans/dydo-3-linear-migration.md`
- `dydo/project/campaigns/linear-pm-pivot.md`
- `dydo/project/migrations/3.0-linear-bootstrap.md` (serial first write; Issue 5 finalizes it)

Gate procedure: create the two saved views in Linear's UI, record their URLs and exact filters in
`dydo/project/migrations/3.0-linear-bootstrap.md`, and read the three labels back through
`list_issue_labels`. Run `dydo check` and `git diff --check --
dydo/project/decisions/044-linear-canonical-pm-and-dydo-knowledge-boundary.md
dydo/project/plans/dydo-3-linear-migration.md dydo/project/campaigns/linear-pm-pivot.md`; require both
exit 0. Obtain a fresh reviewer PASS on the three owned documents and record all observables in the
bootstrap Markdown evidence.

#### Issue 2 — Inventory legacy PM records and incoming references

Participation: AFK. Initial state: blocked by Issue 1, then `Todo`.

Deliverable:

- Enumerate every non-meta legacy PM record under configured/current Campaign, Sprint, Slice, Task,
  Issue, backlog, and Release locations.
- Find every Markdown link and exact path/identifier reference from retained docs into those records.
- Create `dydo/project/migrations/3.0-pm-records.json` with one entry per record using the complete
  closed row schema below.
- Create `dydo/project/migrations/3.0-pm-records.md` containing counts, ambiguous groups, and a human
  review checklist. JSON is the complete proof; Markdown is the review surface.

No permanent CLI command or general migration abstraction is added. A bounded repository script is
allowed only if its deletion is part of Project 3.

The JSON contract is closed:

- top level: `schemaVersion: 1`, `generatedFromCommit`, `records`, `excludedCandidates`, and
  `unresolvedCandidates`;
- one `excludedCandidates` item: `path`, `matchedSignature`, non-empty `exclusionReason`, and
  `humanRatified: true`;
- one `unresolvedCandidates` item: `path` and `matchedSignature`; verification requires this collection
  to be empty;
- one record row: `path`; `kind` (`campaign|sprint|slice|task|issue|backlog|release|future-feature`);
  `status` (string or null); `outsideCanonicalFolder` (boolean); `incomingReferences`;
  `proposedDisposition`; `finalDisposition` (null until ratified); `humanRatified`; `executionState`
  (`pending|applied`); `target`; `evidence`; and non-empty `reason`;
- one `incomingReferences` item: `sourcePath`, one-based `line`, `rawTarget`, and `resolution`
  (`unchanged|rewrite-linear|rewrite-retained|rewrite-commit-permalink`);
- dispositions are exactly `migrate-initiative`, `migrate-project`, `migrate-issue`, `retain`,
  `retain-normalize`, `extract-then-remove`, `remove-historical`, `cancel-remove`, or `drop-duplicate`;
- `target` is `{ "kind": ..., "value": ... }`, where kind is exactly `linear-preview-key`,
  `linear-url`, `retained-path`, `commit-permalink`, or `none`;
- `evidence` items are `{ "kind": "linear-readback|retained-path|freeze-commit|human-ruling",
  "value": "..." }`.

Target/evidence rules are deterministic. Migration dispositions require a unique
`linear-preview-key` plus human-ruling evidence while pending, and a `linear-url` plus Linear read-back
evidence when applied. `retain` and `retain-normalize` require a retained path. `extract-then-remove`
requires both retained-path and freeze-commit evidence. `remove-historical`, `cancel-remove`, and
`drop-duplicate` require the exact freeze-commit permalink; duplicate removal also names its retained or
Linear replacement. `none` is permitted only for a pending non-migration disposition before the freeze
commit exists.

The scan covers every non-meta file in the configured/current Campaign, Sprint, Slice, Task, Issue,
backlog, Release, and FutureFeature locations. It also scans every other non-meta Markdown file under
`dydo/project/**` for legacy type/status/title signatures. The three current root exceptions are
explicitly included: `dydo/project/docs-upgrade-sprint.md`, `dydo/project/v1.3-release.md`, and
`dydo/project/v1.4-release.md`. Any other candidate stops generation and appears in
`unresolvedCandidates`; verification cannot pass until a human moves it into `records` or into
`excludedCandidates` with a ratified non-PM reason.

Gate procedure:

1. Run `pwsh -NoProfile -File dydo/project/migrations/build-3.0-pm-manifest.ps1 -RepoRoot . -Write`;
   require exit 0 and `manifest written: <N> records; 0 duplicates; 0 unresolved candidates`.
2. Run the same script with `-Verify`; require exit 0 and `manifest verified: <N> records; 0 missing;
   0 duplicates; 0 invalid references; 0 unresolved candidates`.
3. Run `dydo check`; require exit 0 and record counts/output in the Markdown review surface.
4. Store the exact governing commit and command transcript/counts in both manifest artifacts.

#### Issue 3 — Human-ratify live-work dispositions

Participation: HITL. Initial state: blocked by Issue 2.

Deliverable:

- Review agent-proposed dispositions in bounded groups: open Issues, backlog, nominally unfinished
  Sprint/Slices, stale Tasks, old Campaign/Release records.
- Mark every manifest row `humanRatified: true` with one `finalDisposition` from the closed enum.
- Do not promote any FutureFeature during this Issue.
- Produce the exact preview of Linear Projects/Issues that Project 3 will create; no record creation in
  this Issue.

Gate procedure: run the manifest script with `-Verify -RequireRatified`; require exit 0 and
`ratification verified: <N>/<N>; 0 missing final dispositions; 0 target/evidence violations`. A human
signs the bounded groups in `3.0-pm-records.md`; the JSON records that ruling and remains the complete
proof.

#### Issue 4 — Freeze Notion and seal the v2 PM baseline

Participation: AFK with human approval for the live run. Initial state: blocked by Issue 2.

Deliverable:

- Stop the Notion watchdog without deleting state.
- Run one final full `dydo notion sync --docs` with the current 2.2.9 binary/code and capture the
  spine and docs-mirror result. Plain `dydo notion sync` is insufficient because it is spine-only.
- Resolve every pending write and both spine/docs conflict-shadow locations before continuing.
- Re-run `dydo notion sync --docs` to equilibrium and prove no local/remote authored changes remain
  outstanding in either projection.
- Commit the canonical PM baseline without absorbing unrelated unreviewed work.
- Create annotated tag `pm-v2-final` at that exact commit and protect the remote alias.
- Record the commit and tag URL in both migration manifest artifacts.

Gate procedure:

1. Run `dydo watchdog stop`; require exit 0, a present `_system/.local/watchdog.hold`, and no
   `_system/.local/watchdog.pid` after the bounded stop wait.
2. Run `dydo notion sync --docs` twice, capturing both transcripts in
   `dydo/project/migrations/3.0-notion-freeze.md`; both must exit 0 and the second must report zero
   create/update/delete operations for every spine type and the docs mirror.
3. Verify the pending-write stores and both `_system/notion_sync_spine/` and `_system/notion_sync/`
   contain no unresolved artifact; list every deliberate exception with a human ruling instead of
   silently ignoring it.
4. Commit only the attributed canonical baseline, push it, create annotated tag `pm-v2-final`, and push
   the tag. Require `git rev-parse pm-v2-final^{}` and `git ls-remote origin
   refs/tags/pm-v2-final^{}` to return the same recorded commit SHA.
5. Configure and record a GitHub tag ruleset/protection targeting `pm-v2-final`, then verify it in the
   remote UI/API. The exact commit-SHA permalink—not the movable tag-name URL—is authoritative evidence.
6. Record command exits, mutation counts, shadow/pending inventory, commit/tag/ruleset URLs, and human
   live-run approval in the freeze artifact. No `notion reset`, `--allow-mass-delete`, or remote archive
   operation is permitted.

#### Issue 5 — Verify and seal the reviewed Linear 3.0 graph

Participation: AFK. Initial state: blocked by Issues 1, 3, and 4.

Deliverable:

- Verify the five bootstrapped Projects exactly match §3 and correct only differences
  against this plan using their recorded IDs, never title matching.
- Verify the three approved labels, Project 1 Issues, dependency relations, governing commits, and link
  resources.
- Write created Linear URLs back to the campaign/plan frontmatter where specified and attach the
  resulting branch-following resources.
- Do not create Cycles, Releases, or speculative implementation Issues.

Gate procedure: repeat bootstrap steps 6–8 entirely by recorded ID; require all JSON comparison booleans
true, `bootstrapVerification: "pass"`, resolvable plan/campaign frontmatter URLs, and a Markdown PASS
checklist signed by the human for saved views and Project dependencies. Correct a
safe field mismatch only by recorded ID and read it back again. If correction is ambiguous, unsupported,
or fails, set Issue 5 to `Needs human`, record the exact mismatch, and stop. Because accepted Project 1
work now exists, Issue 5 must never invoke whole-graph bootstrap rollback, cancel accepted objects, or
delete labels.

#### Issue 6 — Prepare and review the next delivery Project

Participation: HITL. Initial state: blocked by Issue 5.

Deliverable:

- Use the ratified manifest and final v2 baseline to write the repo plan for Project 2.
- Give it a fresh plan review. Create its Linear implementation Issues only after PASS.
- Keep Projects 3–5 low-resolution until each becomes the dependency-ready frontier.

Gate procedure: run `dydo check` and targeted
`git diff --check -- dydo/project/plans/dydo-3-linear-native-work-model.md`, require both exit 0, then
obtain a fresh reviewer PASS recorded in that plan. No Project 2 Issue is created and no implementation
starts before all three observables exist.

## 4. Reference contract

### Durable artifact → Linear

- Every multi-Issue Project owns one repo plan at `dydo/project/plans/<slug>.md`.
- Accepted DR filenames and numbers remain stable.
- A repo Project plan carries `linear-project: <url>` in frontmatter after creation.
- FutureFeature promotion adds one stable Linear URL/identifier and terminal `promoted` status; no later
  delivery properties are copied back.

### Linear → durable artifact

- Project Resources attach branch-following GitHub URLs for current DRs, plans, and
  governing docs.
- Each Issue description has a `Governing context` section listing only documents that actually govern
  that contract. Use labels like `DR-044 — Linear-canonical PM boundary`, never naked URLs.
- The same URLs are added as Linear link attachments so they remain visible outside the Markdown body.
- An implementation Issue records the exact governing commit SHA before work begins. The branch-following
  link remains the human navigation link; the commit permalink fixes the contract revision for audit.
- Issue descriptions do not paste full DRs/specs. The Issue remains self-contained for its own outcome,
  while linked durable documents provide governing context.

### Historical references

- The exact pushed freeze commit is the authoritative corpus root for legacy records removed from
  `master`; the protected annotated `pm-v2-final` tag is its human-readable alias.
- Retained docs use exact commit-SHA GitHub permalinks when a deleted record is still material evidence.
  Tag-name URLs may be offered only as secondary navigation.
- Live migrated work links to its Linear object instead.
- Reusable knowledge is extracted to a retained Decision, guide, pitfall, or completed Project plan;
  retained docs link there instead of to an ephemeral execution record.
- The disposition manifest is the authoritative old-path → outcome index. It is historical migration
  evidence, not an ongoing synchronization map.

## 5. Record disposition policy

| Legacy record | Default disposition | Exception |
|---|---|---|
| Decision, guide, reference, architecture doc | retain in Git | superseded docs keep explicit supersession links |
| FutureFeature | retain and normalize | create Linear object only after separate human promotion |
| Changelog, audit/inquisition, assimilation evidence | retain in Git | consolidate exact duplicates only with evidence |
| Active Campaign | map to Initiative/Project, then remove legacy record | retain content only if it contains unique durable Wayfinding/decision material |
| Planned/active Sprint root | map to Project or cancel | extract unique durable specification into a Project plan |
| Completed Sprint root | preserve at `pm-v2-final`; delete from `master` | retain/move as completed plan if still governing or uniquely explanatory |
| Ready/in-progress Slice | map to Issue only if intent remains ratified | otherwise cancel/delete with reason |
| Completed Slice | preserve at tag; delete from `master` | extract reusable invariant before deletion |
| Open observed Issue | map to Linear Issue only after human ratification | consolidate duplicate/moot findings or extract durable pitfall |
| Resolved Issue | preserve at tag; delete from `master` | retain as pitfall/decision evidence only when actively referenced |
| Backlog record | map to Backlog Issue/Project only after human ratification | drop stale or duplicate work with reason |
| Stale/done Task | preserve at tag; delete from `master`; never import | retain no runtime/session residue |
| Release PM record | preserve at tag or changelog; delete PM record | Git tag/changelog remains canonical release evidence |

No deletion occurs from status alone. The manifest reason and incoming-reference disposition are part of
the gate.

## 6. Ordering and isolation

1. Review and ratify this plan.
2. Publish the PASS plan commit, then bootstrap only the five Project shells and Project 1 Issues;
   write back and read back their exact IDs/URLs before execution.
3. Issue 1 settles conventions; Issue 2 follows it so the inventory uses the ratified contract.
4. Issue 3 is the human disposition gate. Issue 4 may prepare offline evidence in parallel but performs
   the final live sync only after inventory completeness is proven.
5. Issue 5 verifies and seals the bootstrapped Linear graph after dispositions and freeze evidence are
   complete.
6. Issue 6 plans Project 2. Later Projects receive detailed plans and Issues only when they become the
   dependency-ready frontier.
7. Project 4 must be accepted before Project 5 deletes any Notion/runtime surface. Project 5 owns final
   deletion, integrated audit, packaging, and release.

The current dirty worktree is not migration input until each existing change is attributed to its owner
and committed, discarded by that owner, or explicitly included in a reviewed Project. No migration Issue
may reset, overwrite, or opportunistically absorb it.

## 7. Watch-outs and rollback

- Do not make Linear creation idempotent by title matching alone. Read back exact returned IDs/URLs and
  record them immediately; duplicate names are not identity.
- Do not delete generic sync code before the final live Notion equilibrium and tag.
- Do not let the guard auto-start the watchdog after the freeze; Project 5 must remove both the daemon and
  every start path.
- Do not import 61 stale Tasks or 243 resolved Issues merely because migration is automatable.
- Do not mistake Linear Project shells for reviewed implementation plans.
- Do not turn `Needs human` into a garbage chute. Escalation must cite searched authorities, researched
  options, impact, and the smallest unresolved decision.
- Do not use a Linear comment as the only home of a new invariant or decision.
- Do not create a custom Issue subtype hierarchy before real work demonstrates a missing native field.
- Do not move or delete retained files until the incoming-reference graph is complete.
- Rollback before source deletion: cancel unaccepted Linear objects by recorded ID and resume from the
  canonical v2 tree. Rollback after source deletion: restore code/files from the recorded exact freeze
  commit; Linear objects remain independently removable because no sync contract exists.

## Plan review

**PASS — 2026-08-27.** A fresh reviewer found no remaining material correctness, migration-safety,
Linear-semantics, reference-durability, boundary, or Project 1 executability blocker after four repair
rounds. The reviewed plan explicitly closes connector/UI capability boundaries, safe late
reconciliation, exact dependency/status semantics, a closed disposition schema, commit-SHA historical
authority, and serial artifact ownership.

Evidence: `dydo check` exits 0 with 0 errors; targeted `git diff --check` is clean; the full repository
test run passed 2,758/2,758 tests with 25 skipped; coverage gap verification passed 141/141 modules.

Human ratification and approval to publish the governing commit and mutate Linear remain pending.

### Human amendment — 2026-08-27

Ratified after the PASS review: the DynaDocs dogfood creates no workspace Initiative. The Basic-plan
workspace is shared with the human's main project, so dydo is confined to five `Dydo`-owned Projects;
the Git plan is their durable umbrella. The bootstrap remains MCP-only. Because the current MCP surface
cannot write native Project dependencies, their nine edges are authoritative in this plan and repeated
in the initial Project-description payloads and read back before PASS; native UI edges are optional
convenience only. Browser/UI fallback is prohibited during provisioning.
