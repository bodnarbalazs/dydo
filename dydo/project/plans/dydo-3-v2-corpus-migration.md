---
title: dydo 3.0 v2 Corpus Migration Execution
status: draft
area: project
type: context
linear-project: https://linear.app/bodnar-balazs/project/dydo-30-migrate-the-v2-work-corpus-51b02b121391
---

# dydo 3.0 v2 Corpus Migration Execution

This plan applies the human-ratified 474-row v2 disposition manifest without recreating the retired
repository work graph. It creates exactly seven approved live Linear Issues, retains and normalizes
exactly three FutureFeatures, deletes the remaining legacy records and temporary corpus hubs from the
default branch, and makes every surviving historical reference recoverable through the exact freeze
commit. The linked Linear Project owns execution state; this repository plan is the reviewed,
commit-pinned contract for eight disjoint implementation Issues.

## 1. Specification

### Intent

Make Project 3 mechanical and auditable. Every manifest row moves from `executionState: pending` to
`executionState: applied`, every approved live-work preview becomes one read-back Linear Issue, every
retained FutureFeature satisfies the dydo 3 contract without promotion, and every other legacy record
is absent from `master` but recoverable at
[`ffffc02dcdf92b9677d0eb4f522d1af57a869990`](https://github.com/bodnarbalazs/dydo/commit/ffffc02dcdf92b9677d0eb4f522d1af57a869990).

### Authorities and fixed identities

| Authority | Fixed identity and role |
|---|---|
| Linear Project 3 | `1d7837c0-ba28-4852-ad0e-5f068bd778bf` — [dydo 3.0 / Migrate the v2 work corpus](https://linear.app/bodnar-balazs/project/dydo-30-migrate-the-v2-work-corpus-51b02b121391) |
| Linear team | `caa6ccbf-4f9b-477e-826c-a51ed43b0687` — `Dydo` (`DYD`) |
| Boundary decision | [DR 044](../decisions/044-linear-canonical-pm-and-dydo-knowledge-boundary.md) |
| Portfolio migration contract | [dydo 3.0 Linear PM Migration](./dydo-3-linear-migration.md), governing bootstrap commit `868eae47fb39540ce0a9f1e14d6ae694a08e94a9` |
| Completed work-model contract | [dydo 3.0 Linear-Native Work Model](./dydo-3-linear-native-work-model.md), reviewed commit `1cf75a219e4a7a30397174e0ab79f4aff1326547` |
| Corrected bootstrap seal | [3.0 Linear Bootstrap](../migrations/3.0-linear-bootstrap.md), correction merge `6dce10aa6a1d3b203795286daa527c3cbe46decf` |
| Ratified manifest | `dydo/project/migrations/3.0-pm-records.json` and its deterministic [review surface](../migrations/3.0-pm-records.md), 474/474 ratified at merge `b7d8cadae54e30fe724cf9c1c5b77ddbcfe44dbd` |
| Frozen corpus | annotated protected tag `pm-v2-final`, peeled SHA `ffffc02dcdf92b9677d0eb4f522d1af57a869990` |

The JSON manifest is the closed set authority. References to a manifest-defined set below mean the
exact ordered rows and tuples in that file at this plan's reviewed commit, never a fresh wildcard scan
or a status-based guess.

### In scope

- Create exactly the seven ratified `migrate-issue` targets in §3 through the official Linear connector,
  read each back by returned identity, and replace its preview target with the stable Linear URL and one
  `linear-readback` evidence item.
- Normalize exactly the three `retain-normalize` files in §3 to `area: project`, `type: concept`,
  `status: idea`, a non-empty `## Rationale`, and a `## Related` section with at least one resolving
  non-Linear durable-knowledge link. They receive no `linear-reference`.
- Delete exactly the 462 `remove-historical` rows and two `cancel-remove` rows, plus the seven migrated
  legacy source records after their Linear read-back succeeds: 471 manifest-record deletions in total.
- Delete the twelve exact temporary legacy hub/meta files in §3 and remove the retired-corpus navigation
  section from `dydo/project/_index.md` while preserving exact freeze recovery links.
- Adjudicate all 1,358 frozen incoming-reference occurrences whose source survives: 1,338 exact
  frozen-file permalinks and sixteen stable Linear URLs are present after rewrite, one exact frozen tuple
  is already absent and satisfied without a write under the chronological DYD-43 amendment, and three
  FutureFeature links remain unchanged. The other 1,487 occurrences disappear because their source is
  one of the 1,062 occurrences inside a deleted record or one of the 425 occurrences inside a deleted
  temporary hub/meta file.
- Apply the Project-3-owned validator/model cleanup assigned by the reviewed work-model plan: strict
  FutureFeature validation, retirement of the task frontmatter/hub/orphan exceptions, removal of the
  `issue` frontmatter type, and conversion of the manifest-backed legacy rule from a temporary pending
  allow-set to an applied-corpus tombstone with exactly three retained normalized paths.
- Mark all 474 rows `executionState: applied`, preserve their identity/order/frozen discovery tuples,
  generate a deterministic final execution summary, verify it, then delete the bounded manifest-builder
  script as required by the portfolio migration contract.

### Out of scope

- No implementation of any of the seven migrated product Issues; Project 3 only creates and reads them
  back. They are unprojected Dydo `Todo` Issues and do not block Project 3 after creation proof is sealed.
- No FutureFeature promotion, Linear object for a FutureFeature, delivery field, or later status mirror.
- No Project 5 work: do not delete or edit Notion provider/runtime, watchdog, generic sync engine,
  token/vault/config, local rollback stores, remote Notion data, sync-model files, Notion tests, packaging,
  release workflow, or release artifacts. The six unavoidable Project-5 file collisions are isolated in
  one serial Issue and ordered in §5; that Issue rewrites references only.
- No Linear client, schema, webhook receiver, poller, cache, Markdown mirror, repository-to-Linear or
  Linear-to-repository synchronization, or managed-agent machinery. In particular, create no Linear Agent
  guidance, Linear-managed coding environment, saved execution view, repository router, or background
  agent delegation.
- No Initiative, Project, Milestone, Cycle, Release, label taxonomy, or speculative Issue creation.
- No deletion or rewriting of accepted Decisions, changelog, inquisitions, migration evidence, or test
  fixtures merely to erase historical terminology. Those retained sources receive durable links.
- No force-push, tag move, remote Notion mutation, destructive Git reset, or self-merge by an executor.

### Acceptance criteria

1. The manifest still contains exactly 474 unique rows and reports exactly `462 remove-historical`,
   `2 cancel-remove`, `7 migrate-issue`, and `3 retain-normalize`; every row is human-ratified and applied.
2. The seven preview identities in §3 map one-to-one to seven newly created Dydo Issues. Each Issue is
   `Todo`, unprojected, unassigned, has no label/cycle/milestone/parent, carries its plain title and meaning,
   records its preview identity and frozen source permalink, and is read back by returned ID. No other
   Linear object is created.
3. Exactly the three FutureFeature paths in §3 remain. Each passes the strict content rule as an
   unpromoted idea, contains no `linear-reference` or delivery field, and resolves at least one durable
   `## Related` link.
4. All 471 non-retained manifest paths and all twelve temporary hub/meta paths are absent from the Git
   index and working tree. The six legacy corpus roots have no tracked file. `dydo/project/_index.md`
   contains no live corpus navigation and names the exact freeze commit as recovery authority.
5. All 1,358 surviving-source reference occurrences meet the closed tuple contract: sixteen contain the
   mapped Linear URL, 1,338 contain
   `https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/<record.path>`,
   the exact DYD-43 tuple is proven already absent without introducing a `project/issues` consumer, and
   the three FutureFeature index links remain unchanged. Counts are checked per source/target pair, so
   one replacement cannot falsely satisfy several original occurrences.
6. The other 1,487 frozen references are accounted for only by deletion of their exact source file;
   no retained source is silently dropped from the rewrite ledger.
7. `LegacyPmManifestService` permits the three applied retained paths, permits no temporary hub/meta path,
   exposes no task-path exception, and keeps every applied deleted/migrated path as a no-resurrection
   tombstone. `LegacyPmRecordRule` rejects a reintroduced applied path and any new legacy record candidate.
8. `FutureFeatureRule` is registered and tests valid idea/promoted forms, missing/extra Linear URLs,
   forbidden delivery fields, missing rationale, and missing/non-resolving related links without calling
   Linear. `issue` is absent from all three frontmatter type vocabularies and task exceptions are gone.
9. The bounded builder is deleted only after its final strict verification has passed and produced JSON/
   Markdown parity plus a deterministic `474 applied` execution summary. The final inline validators in
   §6 pass without the deleted script.
10. Focused tests, full isolated tests, coverage gap, fresh-scaffold shape, retired-surface scans,
    `dydo check`, `git diff --check`, exact-scope checks, and an independent integrated audit all pass.
    A proportionate assimilation brief records the applied counts, seven created Issue identities,
    recovery contract, surviving validator behavior, and remaining Project-5 boundary.

### Questions and answers

- **Where do the seven migrated product Issues live?** On team Dydo as unprojected `Todo` Issues. Putting
  them in Project 3 would incorrectly make product implementation part of corpus migration; putting them
  in another Project would mutate an unreviewed work graph.
- **Are the seven legacy source files retained after Linear creation?** No. A successful read-back first
  changes the manifest target to the stable Linear URL; then the old file is deleted with the other retired
  PM records. The frozen file permalink remains in the Issue and manifest evidence.
- **Does `cancel-remove` mean cancel in Linear?** No. Those two records create no Linear object. It is the
  human-ratified reason for deleting them from `master` while retaining frozen recovery.
- **Do historical references point at the commit root or the exact file?** Manifest row targets remain the
  schema-required commit permalink. Every retained document points at the exact `blob/<SHA>/<path>` file
  permalink so recovery is one click and independent of the movable tag.
- **May similar references be collapsed?** No. The frozen `(target record, sourcePath, line, rawTarget)`
  tuples are the occurrence ledger. Replacement counts are enforced per source/target pair.
- **What if a source changed after the freeze?** Six sources did: `dydo.json`,
  `dydo/guides/orchestration-pitfalls.md`, `dydo/project/_index.md`,
  `dydo/project/plans/dydo-3-linear-migration.md`,
  `dydo/understand/templates-and-customization.md`, and `dydo/understand/work-model.md`. Implementers use
  the frozen tuple for intent and edit the current text semantically; they never restore the frozen blob.
- **What happens to temporary hubs and `issues/resolved/_index.md`?** Delete the twelve exact tracked
  hub/meta files listed below. Empty directories vanish naturally; do not add placeholder files.
- **Does the legacy manifest validator disappear?** No. The bounded generator disappears. The small
  manifest reader/rule remains as a permanent no-resurrection guard, narrowed to the three retained paths.
- **Can Project 5 land concurrently?** Its source-removal lanes may continue on their own reviewed plan,
  but its six hot documentation/config files may not land ahead of Project-3 Issue P3-5. The Project-5
  owner rebases those six paths after P3-5; Project 3 neither adopts nor executes Project-5 deletions.
- **Can any implementation Issue touch the manifest?** Only P3-8. Other Issues return exact evidence to
  Linear; P3-8 serially applies all 474 transitions and is the sole conflict resolver for the manifest.
- **May the integration branch be temporarily red?** Only between landing P3-6 deletions and P3-8's
  execution-state close. It is never pushed to `master` in that state; targeted lane gates and the final
  integrated gate make this interval explicit and bounded.

## 2. Prior art and discovery evidence

- DR 044 fixes the Linear/Git boundary, human-only FutureFeature promotion, reviewed-intent gate, and
  rejection of a new mirror or daemon.
- The portfolio migration plan defines Project 3 as manifest application, approved Linear creation,
  FutureFeature normalization, default-branch corpus removal, and incoming-reference repair. Its closed
  schema requires pending migration previews to become `linear-url` plus `linear-readback` evidence when
  applied, and requires the bounded manifest script to be deleted by Project 3.
- The reviewed work-model plan assigns Project 3 the task frontmatter exception, `issue` type vocabulary,
  strict FutureFeature rule, manifest-backed legacy behavior, and corresponding tests. Current code
  confirms the temporary state in `Rules/FrontmatterRule.cs`, `Models/Frontmatter.cs`,
  `Services/LegacyPmManifestService.cs`, `Rules/LegacyPmRecordRule.cs`, `Rules/HubFilesRule.cs`, and
  `Rules/OrphanDocsRule.cs`.
- The corrected bootstrap seal fixes Project 3 ID/team/resources and explicitly removes saved views,
  labels, Linear Agent guidance, Linear-managed coding environments, and team-prefix routing from the
  product contract.
- The ratified manifest proves 474 records, 2,845 references, zero excluded/unresolved candidates, and
  source commit `ffffc02dcdf92b9677d0eb4f522d1af57a869990`. Local recomputation found 1,062 references
  sourced by records that will be deleted, 425 sourced by twelve temporary hubs/meta files, and 1,358
  sourced by 102 retained files.
- The three retained FutureFeatures already use `type: concept` and `status: idea`, but all require a
  non-empty `## Rationale`; two also require a `## Related` section. Normalization is content work, not
  promotion.
- The Project-5 reviewed plan owns six collision files also present in the reference ledger. It explicitly
  waits for Projects 2–3 before release integration and treats those files as serial hot paths.
- Rejected alternatives: bulk importing all open-looking records violates human ratification; retaining
  the corpus on `master` preserves a dead work graph; title-based Linear idempotency confuses identity;
  deleting historical sources instead of linking them destroys provenance; adding a permanent migration
  command or Linear adapter violates the product boundary.

## 3. Design

### Exact disposition sets

The final-disposition partition is binding:

| Disposition | Rows | Repository outcome | Linear outcome |
|---|---:|---|---|
| `remove-historical` | 462 | delete source after reference repair; retain exact frozen-file recovery | none |
| `cancel-remove` | 2 | delete `dydo/project/backlog/review-tiers-and-attention.md` and `dydo/project/issues/0180-upstream-claude-code-v2-1-114-windows-silent-exit-regression-family-affects-v2-1.md` | none |
| `migrate-issue` | 7 | delete legacy source only after returned-ID read-back | create exactly one Issue per preview |
| `retain-normalize` | 3 | retain and normalize exact path | none |

The three retained paths are:

1. `dydo/project/future-features/agent-graph-metrics.md`
2. `dydo/project/future-features/coverage.py-update.md`
3. `dydo/project/future-features/doc-coverage.md`

The twelve non-row compatibility files deleted with the corpus are:

1. `dydo/project/backlog/_backlog.md`
2. `dydo/project/backlog/_index.md`
3. `dydo/project/campaigns/_campaigns.md`
4. `dydo/project/campaigns/_index.md`
5. `dydo/project/issues/_issues.md`
6. `dydo/project/issues/_index.md`
7. `dydo/project/issues/resolved/_index.md`
8. `dydo/project/slices/_slices.md`
9. `dydo/project/slices/_index.md`
10. `dydo/project/sprints/_sprints.md`
11. `dydo/project/sprints/_index.md`
12. `dydo/project/tasks/_tasks.md`

`dydo/project/tasks/_index.md` is in the temporary service allow-set but is not tracked at the reviewed
plan commit. The gate requires it to remain absent; it is not counted as a deletion.

### Seven approved live Linear Issues

P3-1 creates these identities exactly. Titles are plain-language execution titles, not the opaque preview
keys or stale source filenames. Each description contains the preview key, plain meaning, frozen source
file permalink, human-ruling URL already present in the manifest, this plan's branch-following URL, and
the exact governing plan commit attachment. All seven use team `Dydo`, state `Todo`, and no Project,
assignee, delegate, priority, labels, parent, cycle, milestone, or relations.

| Preview identity | Plain title | Binding meaning |
|---|---|---|
| `DYD-3-PREVIEW-BACKLOG-AUTO-MEMORY-POLICY` | Implement the settled agent auto-memory routing policy | Apply DR 038's small durable-routing policy: generated repository instructions and chief-of-staff methodology route project facts out of agent memory; any one-time local sweep remains separately human-authorized. |
| `DYD-3-PREVIEW-ISSUE-0164-SKIP-PATTERN-BLOCKS-DUPLICATED-ACROSS-RULES-WITH-NO-CENTRAL-SOURCE-OF-TRUTH-SILE` | Centralize documentation rule scope policy | Remove the preventive divergence vector created by duplicated skip/exclusion policy while preserving intentional per-rule differences. |
| `DYD-3-PREVIEW-ISSUE-0212-TOOL-SCOPED-FILE-NUDGES-NEVER-FIRE-FOR-TIER-2-WORKERS-WORKER-LANE-SKIPS-CHECKFIL` | Make file nudges apply to workers by audience | Let worker tool calls receive applicable file nudges while the manager-only doctrine nudge remains manager-only. |
| `DYD-3-PREVIEW-ISSUE-0213-GUARD-DAILY-VALIDATION-USES-RAW-CWD-CREATING-STRAY-NESTED-DYDO-TREE` | Resolve guard state paths from the actual dydo root | Stop guard due-markers from using raw current-directory plus `dydo`, so subdirectory calls cannot create a phantom nested tree. |
| `DYD-3-PREVIEW-ISSUE-0248-DYDO-FIX-HAS-NO-DIRECTORY-SCOPED-MODE-REPO-WIDE-SIDE-EFFECTS-ON-SHARED-DIRTY-TRE` | Add directory-scoped `dydo fix` | Give `dydo fix <path>` the same bounded scope concept as `dydo check <path>` so a local repair does not rewrite unrelated documentation. |
| `DYD-3-PREVIEW-ISSUE-0263-CROSS-REWRITE-DEAD-CODE-DEAD-IN-EFFECT-SURFACES-IN-TERMINALLAUNCHER-AGENTREGISTR` | Surface unknown Notion block IDs to production diagnostics | Carry forward only the human-ratified residual defect: `UnknownBlockIds` is produced by the frozen Notion conversion path but its production consumer never surfaces the condition. Do not restore the four already-retired compound findings. |
| `DYD-3-PREVIEW-ISSUE-0272-CODEX-WORKER-ROLE-READ-ONLY-CAPABILITY-NOT-EXPRESSED-TOOLS-FIELD-IS-CLAUDE-ONLY-US` | Enforce read-only capability for Codex worker roles | Determine and use the supported host-native mechanism that actually narrows spawned read-only roles; do not emit Claude tool-list semantics into Codex configuration. |

Returned Issue keys/URLs are not known until creation. P3-1 records the one-to-one map in its Linear
comment; P3-8 copies only the stable URLs and read-back evidence into the corresponding manifest rows.
Before unblocking P3-3 through P3-5, the orchestrator patches each blocked Issue description with the
seven exact `preview identity -> returned Issue ID -> stable URL` rows and the P3-1 read-back comment URL.
Those reference lanes validate against that immutable read-back map while the manifest still correctly
contains pending preview keys; they do not edit the manifest early.
Creation is not retried by title. On an uncertain mutation, search by the returned ID if available and by
the exact preview identity embedded in the description; zero matches permits one retry after connector
recovery, one match is adopted after full read-back, and multiple matches stop for human cleanup.

### Incoming-reference contract

The manifest's 2,845 frozen tuples partition by source fate:

| Source fate | Occurrences | Required action |
|---|---:|---|
| one of 471 deleted manifest records | 1,062 | source deletion is the proof; no rewrite |
| one of twelve deleted hub/meta files | 425 | source deletion is the proof; no rewrite |
| retained file, `rewrite-commit-permalink` | 1,339 | replace with exact frozen-file blob permalink |
| retained file, `rewrite-linear` | 16 | replace with the returned stable URL for that preview row |
| retained FutureFeature index, `unchanged` | 3 | preserve the existing relative link |

P3-3 owns the exact 58 distinct retained `sourcePath` values under
`dydo/project/changelog/**` (540 occurrences: 535 frozen permalinks and five Linear URLs). P3-4 owns the
exact 36 distinct retained sources outside changelog after excluding the six Project-5 collision files,
`dydo/project/_index.md`, and `dydo/project/future-features/_index.md` (an immutable evidence partition
of 790 occurrences: 779 frozen-permalink resolutions and eleven Linear-URL resolutions). Its amended
delivery arithmetic is 789 actual rewrites (778 frozen permalinks and eleven Linear URLs) plus the one
exact already-absent DYD-43 adjudication, totaling all 790 tuples. P3-5 owns these six exact hot files
(22 frozen-permalink occurrences):

- `dydo.json`
- `dydo/guides/migrating-dydo-1x-to-2x.md`
- `dydo/guides/orchestration-pitfalls.md`
- `dydo/reference/notion-oss-survey.md`
- `dydo/reference/notion-sync.md`
- `dydo/understand/work-model.md`

P3-6 owns `dydo/project/_index.md` (three frozen root-record permalinks) while deleting its live corpus
navigation. `dydo/project/future-features/_index.md` is a verified no-edit path with three unchanged links.
Together the ownership totals are 58 + 36 + 6 + 1 + 1 = 102 retained sources and
540 + 790 + 22 + 3 + 3 = 1,358 ledger occurrences. Two identical `#0263` occurrences share one frozen
source line, so occurrence count—not four-tuple set cardinality—is the authority. The closed executable
partition in §6 rejects source-set overlap. P3-4's derived set expressly excludes all
P3-5/P3-6/no-edit paths.

For a historical row, the replacement URL is exactly:

```text
https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/<record.path>
```

For a migrated row, it is exactly the P3-1 returned URL keyed by the row's pending preview identity; after
P3-8 applies the row, that same value is `record.target.value` with target kind `linear-url`.
Preserve readable labels such as “legacy issue 0164” or the original document title. Do not use a tag URL,
commit-root URL, branch-following deleted path, bare legacy number, or dead relative path as the target.

### Legacy validation end state

- `LegacyPmManifestService` reads `path`, `executionState`, `finalDisposition`, and retained-path target.
  Its allowed paths are pending rows plus applied `retain`/`retain-normalize` paths whose target equals the
  row path. Final Project-3 state therefore allows exactly the three FutureFeatures and no compatibility
  hub/meta file.
- `LegacyPmRecordRule` remains registered. All 471 absent applied paths remain in the manifest-path set,
  so reintroducing any one is an error. A new direct child of a retired corpus directory or a file with
  legacy PM frontmatter remains an error.
- Remove `IsLegacyTaskPath`, its `FrontmatterRule`, `HubFilesRule`, and `OrphanDocsRule` call sites, and
  their obsolete tests. Generic folder/frontmatter behavior handles all surviving documentation.
- Add and register `FutureFeatureRule`. It validates only non-meta Markdown files directly under
  `project/future-features/`; nested or out-of-folder concept docs do not silently become FutureFeatures.
- Remove `issue` from `Frontmatter.ValidTypes`, `Templates/types.json.template`, and
  `dydo/_system/types.json`. `inquisition` remains. Project 5 alone owns the sync-model copy of the retired
  Notion schema and may delete it later; Project 3 does not edit it.

### Manifest transition and deterministic finalization

P3-8 is the only manifest writer. It consumes read-back/comments from P3-1 through P3-7 and applies:

- all 474 rows: `executionState` `pending` → `applied`;
- seven `migrate-issue` rows: target `linear-preview-key` → `linear-url`; add exactly one
  `linear-readback` evidence item while preserving exactly one human ruling;
- three retained rows: preserve `retained-path` target/evidence and human ruling;
- 464 remove/cancel rows: preserve the exact commit-permalink target, freeze evidence, and human ruling;
- preserve every row's path, kind, status, canonical-folder flag, proposed/final disposition, ratification,
  reason, row order, and all 2,845 frozen incoming-reference tuples byte-for-byte.

Before deleting the builder, extend its deterministic Markdown output with this exact final section:

```text
## Execution seal
- execution states: **474 applied / 0 pending**
- final dispositions: **462 remove-historical / 2 cancel-remove / 7 migrate-issue / 3 retain-normalize**
- target kinds: **464 commit-permalink / 7 linear-url / 3 retained-path**
- reference source fate: **1,062 deleted-record / 425 deleted-hub / 1,339 rewrite-commit-permalink / 16 rewrite-linear / 3 unchanged**
```

Run ordinary and `-RequireRatified` verification twice after the final JSON edit. Require the second
run to produce byte-identical JSON and Markdown hashes. Record both hashes in the assimilation brief
as `final manifest JSON SHA-256` and `final manifest Markdown SHA-256`, then delete
`dydo/project/migrations/build-3.0-pm-manifest.ps1`. The post-deletion validator below compares the
immutable envelope and ordered 2,845-tuple ledger with the ratified artifact at the exact governing
plan commit, checks the exact execution/evidence transition, checks the Markdown seal, and re-hashes
both artifacts. No permanent migration command is introduced.

## 4. Implementation Issue map

Implementation Issue keys are assigned by Linear only after this plan's passing commit is merged. The
stable plan IDs and exact plain titles below are used in creation descriptions so the resulting mapping
is unambiguous. Create all eight on team `Dydo` in Project ID
`1d7837c0-ba28-4852-ad0e-5f068bd778bf`, initial state `Todo`, with no labels/cycle/milestone/parent.
Each description contains its exact exclusive ownership, acceptance and gate, the branch-following plan
URL, DR 044, the exact merged governing plan SHA, and a governing-commit link attachment. Native blocker
relations are exactly the §4 `Blockers` column; do not encode dependencies only as prose.

| Plan ID and Issue title | Outcome | Exclusive repository/Linear ownership | Blockers | Gate |
|---|---|---|---|---|
| P3-1 — Create and read back the seven approved live Issues | Seven exact unprojected Dydo `Todo` Issues and a returned-ID map; no product implementation | Linear: exactly the seven new Issue objects in §3; repository: none | reviewed plan merged | exact creation/read-back predicate; zero extra objects |
| P3-2 — Normalize the three retained FutureFeatures | Three unpromoted idea records meet the final content contract | the three exact FutureFeature files in §3 | reviewed plan merged | focused `FutureFeatureRule` fixture precheck after P3-7 integration; links resolve |
| P3-3 — Rewrite frozen references in retained changelog | All 540 manifest tuples in the exact 58 retained changelog sources have their prescribed targets | exact manifest-derived retained `sourcePath` set matching `dydo/project/changelog/**` | P3-1 | source/target occurrence validator for this partition; `git diff --check` on its set |
| P3-4 — Rewrite frozen references in retained knowledge and fixtures | The 36-file non-changelog, non-hot, non-index partition has 789 prescribed-target rewrites plus the one exact already-absent DYD-43 adjudication, totaling its immutable 790 tuples without a `project/issues` consumer | exact derived set in §3, including Decisions, inquisitions, active plan/context, and `DynaDocs.Tests/TestData/link-validator/index.md`; excludes every P3-3/P3-5/P3-6/P3-2 path | P3-1; DYD-43 merged | disjoint-set assertion; 789 rewritten + 1 exact already-absent = 790 adjudicated; source/target occurrence validator; focused link-validator E2E and active-doc no-`project/issues` tests |
| P3-5 — Rewrite the six Project-5 collision files | All 22 frozen tuples in the six exact hot files use durable targets without performing Project-5 removal | the six exact paths in §3, reference edits only | P3-1; Project-5 docs/config lane held | exact hot-file diff allowlist; occurrence validator; Project-5 owner acknowledges rebase point |
| P3-6 — Delete the ratified corpus and retire its navigation | Exact 471 manifest source deletions, twelve hub/meta deletions, and a durable root recovery section | `records[finalDisposition != 'retain-normalize'].path`; twelve exact compatibility paths; `dydo/project/_index.md` | P3-1 read-back; P3-2 ready; P3-3–P3-5 review PASS | exact deletion-set equality; 3 retained rows present; six roots contain no tracked file |
| P3-7 — Close the legacy validators and enforce FutureFeatures | Permanent tombstone and strict FutureFeature/frontmatter contract | `Services/LegacyPmManifestService.cs`; `Rules/LegacyPmRecordRule.cs`; `Rules/FrontmatterRule.cs`; `Rules/HubFilesRule.cs`; `Rules/OrphanDocsRule.cs`; new `Rules/FutureFeatureRule.cs`; `Commands/CheckDocValidator.cs`; `Models/Frontmatter.cs`; `Templates/types.json.template`; `dydo/_system/types.json`; `DynaDocs.Tests/Services/LegacyPmManifestServiceTests.cs`; `DynaDocs.Tests/Rules/LegacyPmRecordRuleTests.cs`; `DynaDocs.Tests/Rules/FrontmatterRuleTests.cs`; `DynaDocs.Tests/Rules/HubFilesRuleTests.cs`; `DynaDocs.Tests/Rules/OrphanDocsRuleTests.cs`; new `DynaDocs.Tests/Rules/FutureFeatureRuleTests.cs`; `DynaDocs.Tests/Services/FrontmatterTypesServiceTests.cs` | P3-2 and P3-6 integrated | build plus exact focused filter; no legacy exception symbols; strict fixtures pass |
| P3-8 — Apply the manifest and run the integrated corpus audit | All rows applied, final deterministic evidence sealed, builder deleted, assimilation recorded, both authored records reachable, integrated PASS | `dydo/project/migrations/3.0-pm-records.json`; `dydo/project/migrations/3.0-pm-records.md`; deletion of `dydo/project/migrations/build-3.0-pm-manifest.ps1`; new `dydo/project/migrations/3.0-v2-corpus-migration-assimilation.md`; generated `dydo/project/migrations/_index.md`; generated `dydo/project/plans/_index.md`; conflict resolution only in prior Issue-owned paths | P3-1–P3-7 reviewed and integrated | all §6 gates, fresh integrated audit PASS |

No implementation Issue may add a path to its ownership set by convenience. A newly discovered source
tuple or collision stops integration and requires a reviewed plan amendment; it is not absorbed by P3-8.

## 5. Ordering, isolation, collisions, and rollback

### Branch and landing order

1. Create all eight detailed Linear Issues only after this reviewed plan is merged. Attach the exact plan
   commit and commit permalink to each, plus DR 044 and the branch-following plan link. Set blocker edges
   exactly as in §4.
2. P3-1 runs first. It creates by preview identity, records every returned ID immediately, reads back all
   fields, and publishes the map on P3-1. It creates no repository branch.
3. P3-2 may run in parallel with P3-1. P3-3 and P3-4 use isolated worktrees after P3-1 returns the seven
   URLs. P3-5 is isolated but serially held against Project 5.
4. Review P3-2 through P3-5 independently. Integrate their passed commits into a Project-3 integration
   branch in plan-ID order. Do not merge those partial commits directly to `master`.
5. P3-6 deletes the exact sets only after P3-1 read-back and reference-lane reviews. Its integration makes
   the current pending-path validator temporarily report missing files; this is the single permitted red
   interval and exists only on the Project integration branch.
6. P3-7 lands after P3-6, so no temporary hub/task exception is needed. P3-8 then applies the manifest,
   resolves only integration conflicts in already reviewed paths, deletes the builder, and runs all gates.
7. Only the final green, independently audited Project branch may open the Project implementation PR to
   `master`. Human/coordinator integration owns merge; the orchestrator does not self-merge.

Every P3-1 through P3-8 Issue receives a fresh independent Issue review against its exact owned set and
gate evidence before integration. P3-8's assimilation brief is reviewed as part of the integrated audit;
it records durable outcomes and invariants, never live Linear status copied back into Git.

### File collisions

- **Manifest hot files:** P3-8 is sole writer. P3-1–P3-7 publish evidence in Linear comments, never by
  editing the manifest.
- **Project root index:** P3-6 is sole writer. Reference lanes exclude it.
- **FutureFeature index:** no writer; its three existing links are the unchanged proof. P3-2 owns only
  the three idea files.
- **Project 5:** P3-5 is the sole Project-3 writer of the six exact collision paths. The Project-5 docs/
  config lane must begin or rebase after P3-5's reviewed commit. If Project 5 has already deleted either
  reference file on its private branch, it keeps that deletion during rebase; Project 3 still lands its
  reference-only commit first and does not adopt the deletion. `dydo.json` unrelated Project-5 edits are
  preserved byte-for-byte during rebase.
- **Migrations hub:** P3-8 is the first writer of `dydo/project/migrations/_index.md` for its assimilation
  brief. Project 5 creates later migration/release evidence and must rebase or regenerate that hub after
  P3-8 so both Projects' durable records remain reachable; Project 3 does not add Project-5 entries early.
- **Plan hub:** this one-file planning PR intentionally has one new orphan warning because its exclusive
  scope forbids editing `dydo/project/plans/_index.md`. P3-8 owns that generated hub and removes the
  warning before the final Project gate.
- **Generated indexes:** no `dydo index` or `dydo fix` runs in P3-2–P3-7. P3-8 never runs either command
  against the live Project tree: parent scope would recursively regenerate unrelated hubs, while passing
  `plans/` or `migrations/` directly skips the desired root hub. After a local checkpoint commit containing
  every P3-8 non-index change, use this bounded disposable-corpus procedure. It snapshots every copied
  file, permits exactly the two intended scratch indexes to change, proves byte-idempotence, copies back
  only those indexes, and then requires the live unstaged path set to equal the same two paths.

  ```powershell
  $scratch = Join-Path ([IO.Path]::GetTempPath()) ('dyd27-index-' + [guid]::NewGuid().ToString('N'))
  $scratchProject = Join-Path $scratch 'project'
  [void](New-Item -ItemType Directory -Path $scratchProject)
  Copy-Item -LiteralPath 'dydo/project/plans' -Destination $scratchProject -Recurse
  Copy-Item -LiteralPath 'dydo/project/migrations' -Destination $scratchProject -Recurse
  function Get-Snapshot([string]$root) {
    $result = @{}
    foreach ($file in @(Get-ChildItem -LiteralPath $root -Recurse -File)) {
      $relative = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
      $result[$relative] = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash
    }
    $result
  }
  function Get-Delta([hashtable]$before, [hashtable]$after) {
    @($before.Keys + $after.Keys | Sort-Object -Unique | Where-Object {
      -not $before.ContainsKey($_) -or -not $after.ContainsKey($_) -or $before[$_] -ne $after[$_]
    })
  }
  $before = Get-Snapshot $scratchProject
  dydo fix $scratchProject
  if ($LASTEXITCODE -ne 0) { throw 'Scratch hub generation failed.' }
  $first = Get-Snapshot $scratchProject
  $expected = @('migrations/_index.md', 'plans/_index.md')
  $firstDelta = @(Get-Delta $before $first)
  if (($firstDelta -join "`n") -ne ($expected -join "`n")) { throw "Unexpected generated path set:`n$($firstDelta -join "`n")" }
  dydo fix $scratchProject
  if ($LASTEXITCODE -ne 0) { throw 'Scratch hub idempotence run failed.' }
  $second = Get-Snapshot $scratchProject
  if (@(Get-Delta $first $second).Count -ne 0) { throw 'Generated hubs are not byte-idempotent.' }
  Copy-Item -LiteralPath (Join-Path $scratchProject 'plans/_index.md') -Destination 'dydo/project/plans/_index.md'
  Copy-Item -LiteralPath (Join-Path $scratchProject 'migrations/_index.md') -Destination 'dydo/project/migrations/_index.md'
  $liveDelta = @(git diff --name-only | Sort-Object)
  $liveExpected = @('dydo/project/migrations/_index.md', 'dydo/project/plans/_index.md')
  if (($liveDelta -join "`n") -ne ($liveExpected -join "`n")) { throw "Unexpected live generated path set:`n$($liveDelta -join "`n")" }
  $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
  $resolvedScratch = [IO.Path]::GetFullPath($scratch)
  if (-not $resolvedScratch.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or
      (Split-Path -Leaf $resolvedScratch) -notlike 'dyd27-index-*') { throw 'Unsafe scratch cleanup target.' }
  Remove-Item -LiteralPath $resolvedScratch -Recurse -Force
  ```

  Review the two generated diffs before committing them. No other generated hub is owned.
- **Project 4:** no file or Linear-object overlap. The seven migrated product Issues remain unprojected and
  are not silently used as the Project-4 dogfood delivery.

### Rollback and failure handling

- Before P3-1 mutation, rollback is no action. On partial/uncertain Linear creation, use returned IDs and
  the preview identity protocol in §3. Cancel only duplicate/unaccepted objects by recorded ID after human
  confirmation; never delete or title-match a pre-existing Issue.
- Before P3-6, abandon or revert an individual isolated lane commit. Do not restore unrelated files.
- After P3-6 on the integration branch, rollback by ordinary revert of P3-6 and later integration commits.
  Never reconstruct 471 records manually; the exact freeze commit is authoritative.
- After final merge, recover one deleted file with
  `git show ffffc02dcdf92b9677d0eb4f522d1af57a869990:<path>` into a reviewed corrective branch, or revert
  the bounded Project commits. Do not move `pm-v2-final`, restart the stopped watchdog, or recreate a repo
  work graph.
- Linear and Git roll back independently. A Git rollback never copies Linear state into Markdown; a
  Linear cancellation never removes frozen Git evidence.
- Any mismatch in row counts, source/target occurrence counts, returned Linear fields, tag peel, or
  Project-5 hot-file ownership stops the lane. P3-8 may resolve textual conflicts but may not waive a gate.

## 6. Gates and deterministic verification

### Preflight

Before implementation starts:

```powershell
$expectedBase = 'ffffc02dcdf92b9677d0eb4f522d1af57a869990'
$tagPeel = (git rev-parse 'pm-v2-final^{}').Trim()
if ($LASTEXITCODE -ne 0 -or $tagPeel -ne $expectedBase) { throw "pm-v2-final mismatch: $tagPeel" }

pwsh -NoProfile -File dydo/project/migrations/build-3.0-pm-manifest.ps1 -RepoRoot . -Verify -RequireRatified
if ($LASTEXITCODE -ne 0) { throw 'Ratified manifest preflight failed.' }

$manifest = Get-Content -Raw dydo/project/migrations/3.0-pm-records.json | ConvertFrom-Json -Depth 100
$counts = @($manifest.records | Group-Object finalDisposition | ForEach-Object { "$($_.Name)=$($_.Count)" } | Sort-Object)
$expectedCounts = @('cancel-remove=2','migrate-issue=7','remove-historical=462','retain-normalize=3')
if (($counts -join "`n") -ne ($expectedCounts -join "`n")) { throw 'Final-disposition counts changed.' }
if (@($manifest.records | Where-Object executionState -ne 'pending').Count -ne 0) { throw 'Preflight contains an already-applied row.' }

$immutableRows = @($manifest.records | ForEach-Object {
  [ordered]@{
    path = $_.path; kind = $_.kind; status = $_.status
    outsideCanonicalFolder = $_.outsideCanonicalFolder
    proposedDisposition = $_.proposedDisposition; finalDisposition = $_.finalDisposition
    humanRatified = $_.humanRatified; incomingReferences = $_.incomingReferences; reason = $_.reason
  }
}) | ConvertTo-Json -Depth 20 -Compress
$immutableRowsHash = [Convert]::ToHexString(
  [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($immutableRows))
).ToLowerInvariant()
if ($immutableRowsHash -ne '30421da6b5c3621a563938ba16a51b35a7f352c630d5ad1986d392e6a05bfb81') {
  throw "Ratified immutable-row hash changed: $immutableRowsHash"
}
```

The Linear connector also reads back Project ID `1d7837c0-ba28-4852-ad0e-5f068bd778bf`, team ID
`caa6ccbf-4f9b-477e-826c-a51ed43b0687`, Project status `In Progress`, and the branch-following migration
resources before P3-1. This is one-time execution proof, not a runtime feature.

### Per-lane tests

- P3-1: list newly created Issues by the seven returned IDs; assert exact team/state/null Project and
  embedded unique preview identity, then search exact preview identities and require one match each.
- P3-2: `dydo check dydo/project/future-features` after P3-7 is integrated; before then, use a bounded
  content predicate for frontmatter, `## Rationale`, `## Related`, no Linear URL, and resolving links.
- P3-3/P3-4/P3-5: run the occurrence validator below filtered to the Issue's exact source set and
  `git diff --check -- <owned paths>`. Before P3-8, its `rewrite-linear` branch resolves
  `$linearUrlsByPreview[$sample.Value]`, where the seven-entry hashtable is copied mechanically from the
  exact P3-1 read-back map attached to that Issue; a missing/extra key fails. P3-4 additionally runs the
  link-validator E2E filter and the
  `CommandDocConsistencyTests.ActiveProductDocs_ExcludeRetiredCommandsAndRepositoryWorkPaths` filter.
  The final integrated run uses the applied manifest URL branch shown below.
- P3-6: run the exact deletion predicate below; do not claim `dydo check` until P3-8.
- P3-7:

```powershell
dotnet build DynaDocs.Tests/DynaDocs.Tests.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "FullyQualifiedName~LegacyPmManifestServiceTests|FullyQualifiedName~LegacyPmRecordRuleTests|FullyQualifiedName~FutureFeatureRuleTests|FullyQualifiedName~FrontmatterRuleTests|FullyQualifiedName~HubFilesRuleTests|FullyQualifiedName~OrphanDocsRuleTests|FullyQualifiedName~FrontmatterTypesServiceTests"
if ($LASTEXITCODE -ne 0) { throw 'Project-3 validator tests failed.' }
```

### Exact deletion and manifest end-state predicate

```powershell
$freezeSha = 'ffffc02dcdf92b9677d0eb4f522d1af57a869990'
$manifestPath = 'dydo/project/migrations/3.0-pm-records.json'
$governingPlanCommit = '<exact 40-character governing plan commit attached to P3-8>'
if ($governingPlanCommit -notmatch '^[0-9a-f]{40}$') { throw 'Populate the exact governing plan commit from the P3-8 attachment.' }
$baselineText = @(git show "${governingPlanCommit}:$manifestPath") -join "`n"
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($baselineText)) { throw 'Cannot read ratified manifest at governing plan commit.' }
$baseline = $baselineText | ConvertFrom-Json -Depth 100
$manifest = Get-Content -Raw $manifestPath | ConvertFrom-Json -Depth 100
$rows = @($manifest.records)
if ($rows.Count -ne 474 -or @($rows.path | Sort-Object -Unique).Count -ne 474) { throw 'Manifest identity set changed.' }
if (@($rows | Where-Object { -not $_.humanRatified -or $_.executionState -ne 'applied' }).Count -ne 0) { throw 'Every row must be ratified and applied.' }

function Get-ImmutableEnvelopeJson([object]$value) {
  $immutableRows = @($value.records | ForEach-Object {
    [ordered]@{
      path = $_.path; kind = $_.kind; status = $_.status
      outsideCanonicalFolder = $_.outsideCanonicalFolder
      proposedDisposition = $_.proposedDisposition; finalDisposition = $_.finalDisposition
      humanRatified = $_.humanRatified; incomingReferences = $_.incomingReferences; reason = $_.reason
    }
  })
  [ordered]@{
    schemaVersion = $value.schemaVersion; generatedFromCommit = $value.generatedFromCommit
    excludedCandidates = $value.excludedCandidates; unresolvedCandidates = $value.unresolvedCandidates
    records = $immutableRows
  } | ConvertTo-Json -Depth 30 -Compress
}
$baselineImmutable = Get-ImmutableEnvelopeJson $baseline
$currentImmutable = Get-ImmutableEnvelopeJson $manifest
if ($currentImmutable -cne $baselineImmutable) { throw 'Immutable envelope, row order, or frozen reference tuple changed.' }
$immutableRowsOnly = @($rows | ForEach-Object {
  [ordered]@{
    path = $_.path; kind = $_.kind; status = $_.status
    outsideCanonicalFolder = $_.outsideCanonicalFolder
    proposedDisposition = $_.proposedDisposition; finalDisposition = $_.finalDisposition
    humanRatified = $_.humanRatified; incomingReferences = $_.incomingReferences; reason = $_.reason
  }
}) | ConvertTo-Json -Depth 20 -Compress
$immutableRowsHash = [Convert]::ToHexString(
  [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($immutableRowsOnly))
).ToLowerInvariant()
if ($immutableRowsHash -ne '30421da6b5c3621a563938ba16a51b35a7f352c630d5ad1986d392e6a05bfb81') {
  throw "Ratified immutable-row hash changed: $immutableRowsHash"
}

$dispositions = @($rows | Group-Object finalDisposition | ForEach-Object { "$($_.Name)=$($_.Count)" } | Sort-Object)
$expectedDispositions = @('cancel-remove=2','migrate-issue=7','remove-historical=462','retain-normalize=3')
if (($dispositions -join "`n") -ne ($expectedDispositions -join "`n")) { throw 'Final-disposition counts changed.' }
$baselineByPath = @{}; foreach ($row in @($baseline.records)) { $baselineByPath[$row.path] = $row }
foreach ($row in $rows) {
  $before = $baselineByPath[$row.path]
  if ($null -eq $before) { throw "No ratified baseline row: $($row.path)" }
  if ($row.finalDisposition -eq 'migrate-issue') {
    if ($before.target.kind -ne 'linear-preview-key' -or $row.target.kind -ne 'linear-url' -or
        $row.target.value -notmatch '^https://linear\.app/bodnar-balazs/issue/DYD-\d+(?:/[^\s]+)?$' -or
        @($row.evidence).Count -ne 2 -or $row.evidence[1].kind -ne 'linear-readback' -or
        $row.evidence[1].value -ne $row.target.value -or
        (ConvertTo-Json -InputObject $row.evidence[0] -Depth 10 -Compress) -cne
        (ConvertTo-Json -InputObject $before.evidence[0] -Depth 10 -Compress)) {
      throw "Invalid migrated target/evidence transition: $($row.path)"
    }
  } elseif ((ConvertTo-Json -InputObject $row.target -Depth 10 -Compress) -cne
            (ConvertTo-Json -InputObject $before.target -Depth 10 -Compress) -or
            (ConvertTo-Json -InputObject @($row.evidence) -Depth 10 -Compress) -cne
            (ConvertTo-Json -InputObject @($before.evidence) -Depth 10 -Compress)) {
    throw "Non-migration target/evidence changed: $($row.path)"
  }
}

$retained = @($rows | Where-Object finalDisposition -eq 'retain-normalize')
$deleted = @($rows | Where-Object finalDisposition -ne 'retain-normalize')
if ($retained.Count -ne 3 -or $deleted.Count -ne 471) { throw 'Retained/deleted partition mismatch.' }
foreach ($row in $retained) { if (-not (Test-Path -LiteralPath $row.path)) { throw "Retained path missing: $($row.path)" } }
foreach ($row in $deleted) { if (Test-Path -LiteralPath $row.path) { throw "Deleted record remains: $($row.path)" } }

$temporary = @(
  'dydo/project/backlog/_backlog.md','dydo/project/backlog/_index.md',
  'dydo/project/campaigns/_campaigns.md','dydo/project/campaigns/_index.md',
  'dydo/project/issues/_issues.md','dydo/project/issues/_index.md','dydo/project/issues/resolved/_index.md',
  'dydo/project/slices/_slices.md','dydo/project/slices/_index.md',
  'dydo/project/sprints/_sprints.md','dydo/project/sprints/_index.md','dydo/project/tasks/_tasks.md'
)
foreach ($path in $temporary) { if (Test-Path -LiteralPath $path) { throw "Temporary corpus file remains: $path" } }
if (Test-Path -LiteralPath 'dydo/project/tasks/_index.md') { throw 'Untracked-at-plan task hub was recreated.' }

$legacyTracked = @(git ls-files -- 'dydo/project/backlog/**' 'dydo/project/campaigns/**' 'dydo/project/issues/**' 'dydo/project/slices/**' 'dydo/project/sprints/**' 'dydo/project/tasks/**')
if ($LASTEXITCODE -ne 0 -or $legacyTracked.Count -ne 0) { throw "Legacy corpus root still tracked:`n$($legacyTracked -join "`n")" }
if (Test-Path -LiteralPath 'dydo/project/migrations/build-3.0-pm-manifest.ps1') { throw 'Bounded builder still exists.' }

$manifestMarkdownPath = 'dydo/project/migrations/3.0-pm-records.md'
$assimilationPath = 'dydo/project/migrations/3.0-v2-corpus-migration-assimilation.md'
$manifestMarkdown = Get-Content -Raw -LiteralPath $manifestMarkdownPath
$sealLines = @(
  '## Execution seal',
  '- execution states: **474 applied / 0 pending**',
  '- final dispositions: **462 remove-historical / 2 cancel-remove / 7 migrate-issue / 3 retain-normalize**',
  '- target kinds: **464 commit-permalink / 7 linear-url / 3 retained-path**',
  '- reference source fate: **1,062 deleted-record / 425 deleted-hub / 1,339 rewrite-commit-permalink / 16 rewrite-linear / 3 unchanged**'
)
foreach ($line in $sealLines) { if (-not $manifestMarkdown.Contains($line)) { throw "Manifest Markdown seal missing: $line" } }
$assimilation = Get-Content -Raw -LiteralPath $assimilationPath
$jsonHashMatch = [regex]::Match($assimilation, '(?m)^- final manifest JSON SHA-256: `(?<hash>[0-9a-f]{64})`\r?$')
$markdownHashMatch = [regex]::Match($assimilation, '(?m)^- final manifest Markdown SHA-256: `(?<hash>[0-9a-f]{64})`\r?$')
if (-not $jsonHashMatch.Success -or -not $markdownHashMatch.Success) { throw 'Assimilation manifest hashes missing or malformed.' }
$actualJsonHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $manifestPath).Hash.ToLowerInvariant()
$actualMarkdownHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $manifestMarkdownPath).Hash.ToLowerInvariant()
if ($actualJsonHash -ne $jsonHashMatch.Groups['hash'].Value -or
    $actualMarkdownHash -ne $markdownHashMatch.Groups['hash'].Value) { throw 'Final manifest hash does not match the stable-run assimilation seal.' }

$targets = @($rows | Group-Object { $_.target.kind } | ForEach-Object { "$($_.Name)=$($_.Count)" } | Sort-Object)
$expectedTargets = @('commit-permalink=464','linear-url=7','retained-path=3')
if (($targets -join "`n") -ne ($expectedTargets -join "`n")) { throw 'Final target-kind counts changed.' }
$provenanceCounts = $manifest.provenance.counts
if ($manifest.generatedFromCommit -ne $freezeSha -or $provenanceCounts.records -ne 474 -or
    $provenanceCounts.incomingReferences -ne 2845 -or $provenanceCounts.excludedCandidates -ne 0 -or
    $provenanceCounts.unresolvedCandidates -ne 0) { throw 'Final provenance counts/source changed.' }
```

### Exact retained-reference occurrence predicate

This validator derives the ledger from the frozen manifest, removes only the exact deleted-source sets,
and checks replacement counts per `(sourcePath,target record)` pair:

```powershell
$freezeSha = 'ffffc02dcdf92b9677d0eb4f522d1af57a869990'
$manifest = Get-Content -Raw dydo/project/migrations/3.0-pm-records.json | ConvertFrom-Json -Depth 100
$deletedRecords = @($manifest.records | Where-Object finalDisposition -ne 'retain-normalize' | ForEach-Object path)
$deletedHubs = @(
  'dydo/project/backlog/_backlog.md','dydo/project/backlog/_index.md',
  'dydo/project/campaigns/_campaigns.md','dydo/project/campaigns/_index.md',
  'dydo/project/issues/_issues.md','dydo/project/issues/_index.md','dydo/project/issues/resolved/_index.md',
  'dydo/project/slices/_slices.md','dydo/project/slices/_index.md',
  'dydo/project/sprints/_sprints.md','dydo/project/sprints/_index.md','dydo/project/tasks/_tasks.md'
)
$ledger = @($manifest.records | ForEach-Object {
  $row = $_
  @($row.incomingReferences) | ForEach-Object {
    [pscustomobject]@{ Source = $_.sourcePath; Target = $row.path; Resolution = $_.resolution; Raw = $_.rawTarget; Value = $row.target.value }
  }
})
$deletedSource = @($ledger | Where-Object { $_.Source -in $deletedRecords -or $_.Source -in $deletedHubs })
$surviving = @($ledger | Where-Object { $_.Source -notin $deletedRecords -and $_.Source -notin $deletedHubs })
if ($ledger.Count -ne 2845 -or $deletedSource.Count -ne 1487 -or $surviving.Count -ne 1358) { throw 'Reference source-fate counts changed.' }
$resolutionCounts = @($surviving | Group-Object Resolution | ForEach-Object { "$($_.Name)=$($_.Count)" } | Sort-Object)
$expectedResolutionCounts = @('rewrite-commit-permalink=1339','rewrite-linear=16','unchanged=3')
if (($resolutionCounts -join "`n") -ne ($expectedResolutionCounts -join "`n")) { throw 'Surviving reference counts changed.' }
if (@($surviving.Source | Sort-Object -Unique).Count -ne 102) { throw 'Retained source-file count changed.' }

$p35Sources = @(
  'dydo.json','dydo/guides/migrating-dydo-1x-to-2x.md','dydo/guides/orchestration-pitfalls.md',
  'dydo/reference/notion-oss-survey.md','dydo/reference/notion-sync.md','dydo/understand/work-model.md'
)
$p34Sources = @($surviving.Source | Where-Object {
  $_ -notlike 'dydo/project/changelog/*' -and $_ -notin $p35Sources -and
  $_ -ne 'dydo/project/_index.md' -and $_ -ne 'dydo/project/future-features/_index.md'
} | Sort-Object -Unique)
$p34Ledger = @($surviving | Where-Object Source -in $p34Sources)
if ($p34Sources.Count -ne 36 -or $p34Ledger.Count -ne 790) { throw 'P3-4 immutable evidence partition changed.' }
$alreadyAbsent = @($p34Ledger | Where-Object {
  $_.Source -eq 'dydo/understand/templates-and-customization.md' -and $_.Raw -eq 'issue 0301' -and
  $_.Target -eq 'dydo/project/issues/resolved/0301-obsolete-pre-dr-041-dydo-diagram-svg-still-shipped-scaffolded-and-hash-tracked.md' -and
  $_.Resolution -eq 'rewrite-commit-permalink'
})
if ($alreadyAbsent.Count -ne 1) { throw 'DYD-43 already-absent tuple is not exact and unique.' }
$alreadyAbsentPermalink = "https://github.com/bodnarbalazs/dydo/blob/$freezeSha/$($alreadyAbsent[0].Target)"
$alreadyAbsentSource = Get-Content -Raw -LiteralPath $alreadyAbsent[0].Source
if ($alreadyAbsentSource.Contains($alreadyAbsent[0].Raw) -or
    $alreadyAbsentSource.Contains('### Binary Assets') -or
    $alreadyAbsentSource.Contains($alreadyAbsentPermalink) -or
    $alreadyAbsentSource.Contains('dydo/project/issues/')) {
  throw 'DYD-43 source is not already absent or a project/issues consumer was introduced.'
}

$p34ActualRewrites = 0
$p34AlreadyAbsentAdjudications = 0
foreach ($group in @($surviving | Group-Object Source,Target,Resolution)) {
  $sample = $group.Group[0]
  if (-not (Test-Path -LiteralPath $sample.Source)) { throw "Retained reference source missing: $($sample.Source)" }
  $content = Get-Content -Raw -LiteralPath $sample.Source
  $expected = switch ($sample.Resolution) {
    'rewrite-commit-permalink' { "https://github.com/bodnarbalazs/dydo/blob/$freezeSha/$($sample.Target)" }
    'rewrite-linear' { $sample.Value }
    'unchanged' { $sample.Raw }
    default { throw "Unknown resolution: $($sample.Resolution)" }
  }
  $actualCount = [regex]::Matches($content, [regex]::Escape($expected)).Count
  $isAlreadyAbsent = $sample.Source -eq $alreadyAbsent[0].Source -and
    $sample.Target -eq $alreadyAbsent[0].Target -and $sample.Resolution -eq $alreadyAbsent[0].Resolution
  $expectedCount = if ($isAlreadyAbsent) { 0 } else { $group.Count }
  if ($actualCount -ne $expectedCount) { throw "Reference occurrence mismatch: $($sample.Source) -> $($sample.Target); expected $expectedCount, found $actualCount" }
  if ($sample.Source -in $p34Sources) {
    if ($isAlreadyAbsent) { $p34AlreadyAbsentAdjudications += $group.Count }
    else { $p34ActualRewrites += $actualCount }
  }
}
if ($p34ActualRewrites -ne 789 -or $p34AlreadyAbsentAdjudications -ne 1 -or
    ($p34ActualRewrites + $p34AlreadyAbsentAdjudications) -ne 790) {
  throw "P3-4 delivery mismatch: $p34ActualRewrites rewritten + $p34AlreadyAbsentAdjudications already absent."
}
```

The implementation may package these exact predicates in a temporary command transcript, but it does
not add a permanent source file or CLI command.

### Retired-surface, scaffold, and full gates

1. Assert `IsLegacyTaskPath` and `GetRetainedNonRecordPaths` have zero production/test hits, `issue` is
   absent from the three active type arrays, and no tracked Markdown under the six retired roots exists.
2. Initialize an isolated fresh fixture with the built Release binary. It must contain
   `project/future-features/` and no `project/backlog`, `campaigns`, `issues`, `slices`, `sprints`, or
   `tasks` directory. Run its `dydo check` and require exit 0.
3. Run `dydo check` in this repository and require exit 0 with no new warning attributable to Project 3.
4. Run `py DynaDocs.Tests/coverage/run_tests.py`; require zero failures.
5. Run `py DynaDocs.Tests/coverage/gap_check.py --force-run`; require every surviving module covered.
6. Run `git diff --check` and the exact scope predicate for each implementation Issue and the integrated
   Project diff.
7. Read back all seven migrated Linear Issues again by ID and compare with the manifest URLs. Read back
   Project 3 by fixed ID and require its team/link identity unchanged.
8. Give a fresh independent auditor this plan at its exact governing commit, the integrated diff, all
   lane review verdicts, seven-Issue read-back, manifest hashes, and gate transcripts. Only `PASS` permits
   the Project implementation PR to enter human merge review.

## 7. Watch-outs

- `pm-v2-final` is an alias; only its peeled commit SHA and exact blob permalinks are recovery authority.
- `build-3.0-pm-manifest.ps1 -Verify` writes refreshed evidence. Run it only in P3-8, review its exact
  two-file diff, seal twice, and delete it after the stable run.
- Do not run `-Write`: it regenerates proposed rows and can erase applied targets/evidence.
- A manifest `linear-preview-key` is not a Linear ID. Never search/create by the preview key as if it were
  an Issue identifier; embed it as unique migration provenance and use the connector's returned ID.
- Do not retain the seven migrated Markdown files as shadow Issue bodies. Their exact frozen copies and
  stable Linear URLs are sufficient.
- Do not convert the three FutureFeatures into “backlog” Issues. Their open questions are idea content,
  not permission to schedule them.
- Do not rewrite historical Decisions or inquisitions to pretend retired mechanisms never existed. Change
  only the reference target and enough surrounding grammar to keep the sentence accurate.
- Do not treat Project-5 deletion of a collision file as Project-3 reference work. P3-5 lands first;
  Project 5 rebases and remains sole owner of its later deletion.
- Do not let `dydo fix` or index regeneration absorb unrelated hub churn. All deletion and retained-link
  paths are explicit.
- Do not mark Project 3 complete merely because its PR merges. Linear completion follows final read-back,
  integrated audit PASS, and the human/coordinator's acceptance workflow.

## 8. Chronological amendment — DYD-43 / P3-4A

This non-retroactive amendment records one execution fact discovered after the original plan was reviewed.
It does not alter the ratified manifest, its immutable 790-tuple P3-4 evidence partition, or any frozen
source. It changes only P3-4 delivery arithmetic and the dependent acceptance and validator contract.

The exact tuple is:

- source: `dydo/understand/templates-and-customization.md`
- raw target: `issue 0301`
- target:
  `dydo/project/issues/resolved/0301-obsolete-pre-dr-041-dydo-diagram-svg-still-shipped-scaffolded-and-hash-tracked.md`
- forbidden replacement:
  `https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0301-obsolete-pre-dr-041-dydo-diagram-svg-still-shipped-scaffolded-and-hash-tracked.md`

At governing commit `6ec7aa9ca4971451e5a883d738365477fa41d215`, the current source already contains
neither `issue 0301` nor the frozen `### Binary Assets` section. Reintroducing the frozen permalink solely
to make the original occurrence predicate report 790/790 makes
`CommandDocConsistencyTests.ActiveProductDocs_ExcludeRetiredCommandsAndRepositoryWorkPaths` fail on the
new `project/issues` URL; omitting it leaves the old predicate at 789/790. Therefore this exact tuple is
classified as already absent and satisfied without a write. P3-4 must prove 789 actual rewrites plus one
exact already-absent adjudication equals all 790 immutable tuples, and must introduce no
`project/issues` consumer.

DYD-43 must merge before DYD-31 resumes. DYD-31 then removes any artificially introduced forbidden
permalink from its preserved worktree and reruns every amended gate. DYD-43 does not authorize resuming
DYD-31, changing the manifest, weakening a test, or editing any source other than this plan.
