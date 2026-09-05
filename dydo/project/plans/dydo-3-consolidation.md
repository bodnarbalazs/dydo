---
title: dydo 3.0 / Consolidate and release
status: draft
area: project
type: context
linear-project: https://linear.app/bodnar-balazs/project/dydo-30-consolidate-and-release-54b8939d748e
---

# dydo 3.0 / Consolidate and release

Make the authored methods, compiled agents, CLI, repository practices and Linear work graph agree,
then deliver a locally installed, independently reviewed 3.0.0 candidate the human can use for his
finishing pass. This repository is building the system it uses; the governing decisions and this
plan take precedence over its stale generated instructions. Linear holds live execution evidence.

## 1. Specification

### Intent

An agent entering a fresh installation or this repository should encounter one operating protocol,
follow links that exist, and know who acts next. The human should wake to usable software and an
accurate board, with remaining work distinguished from claims of completion.

### In scope

- **P — Protocol:** accept DR 047; propagate its contracts through authored prompts and current
  guides, supply missing methods/resources, preserve provenance and craftsmanship.
- **S — Simpler checking:** remove mandatory summary-line validation; preserve broken-link checks,
  guard behavior and nudges. Summaries may still serve navigation without being a requirement.
- **G — Static gates:** fully adopt DR 048 locally, including producers, policy, failure triage and
  fixes. The shipped product carries the policy; this repository owns its runners.
- **C — Compiler and onboarding:** retire workflow emission, repair fresh installations, ship the
  setup guide, configure nesting, and establish actual host behavior.
- **B — Board:** reconcile existing Issues and Projects with the accepted model and evidence.
- **I — Integration:** regenerate both hosts, prove parity and cross-contract behavior, prepare
  test-gated packages and release evidence, install the candidate locally.

### Out of scope

Final landing into main/master, release tags, public publication and the human walkthrough remain
human gates. Compiler deletion is a future design choice, not an implementation shortcut here.
Private downstream transition DYD-95, CodeRabbit DYD-89, reasoning-effort compilation DYD-93 and the
deferred branch-visibility mechanism DYD-97 remain outside 3.0. Historical decisions and rejected
acceptance evidence are preserved; current documents must identify the governing replacement.

### Acceptance criteria

1. DR 047 is accepted. A reviewed contact table connects each spawn, status transition, return,
   release, review and merge at Project, Issue and lane levels to its sending and receiving prompt.
   No current prompt relies on retired roles, statuses or workflow execution.
2. Research can delegate and write its designated findings report; scouts are read-only. Production
   edits remain outside Research's methodology. Both compiled hosts express the intended permissions.
3. An otherwise valid document without an opening summary passes `check`; a broken link fails it.
   Existing guard/nudge tests pass. Fresh `init` for all supported integration selections, subsequent
   `check`, `sync`, and template update pass without consumer-specific Decision Records.
4. Every applicable DR 048 gate runs on maintained code with the universal thresholds. Missing
   coverage or metrics fail closed. Initial failures have recorded dispositions and valid failures
   are fixed. Genuine unavailable stack mechanisms are documented as DR 048 permits; absent setup
   for an available mechanism is unfinished work. No baseline waiver or per-file suppression passes
   as adoption.
5. A second sync changes no compiler-owned output. Both hosts have the new role/resource set and
   retired owned artifacts are removed without deleting user-owned files. Setup claims distinguish
   configured behavior, observed behavior and host limitations.
6. The integrated candidate passes the full tests, documentation checks and static gates at its
   recorded SHA. NuGet, npm wrapper and native version agree at 3.0.0. The local package installs and
   runs; publication jobs depend on successful validation. Five native targets have build evidence,
   with execution smoke tests on native runners where available.
7. Linear's schema and open work reflect DR 047. Every release-relevant outstanding Issue has a
   disposition supported by evidence, and unfinished work remains open. The landing PR carries the
   independent review blocks, acceptance evidence and any host or release limitations.

### Questions and answers

The human accepted DR 047, full local DR 048 adoption, autonomous prompt propagation with independent
and root review, and Research delegation/report writing on 2026-09-05. He authorized simplification,
local installations and removal of summary enforcement. He reserved the final landing and finishing
walkthrough. These answers authorize routine implementation choices without another approval round;
a substantive change of destination or governing design still needs his judgment.

## 2. Prior art

- Initial survey at master `1c2d3956752459fbd342ae82368138836031a058`: 1,738 passing tests
  and 12 fresh-install failures; installed CLI 2.2.9 versus source 3.0.0; 97 Issues surveyed.
  These are baseline facts; Linear carries subsequent execution evidence.
- [DR 044](../decisions/044-linear-canonical-pm-and-dydo-knowledge-boundary.md): Linear owns work;
  Git and dydo own durable knowledge. Do not build a Linear runtime client or mirror.
- [DR 045](../decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md) and
  [DR 046](../decisions/046-executable-specifications-specifier-and-commit-addressed-hops.md):
  authored sources, independent review and immutable specify/implement/harden/fix evidence.
- [DR 047](../decisions/047-supersymmetry-hop-statuses-merge-issues-and-the-release-protocol.md),
  [Control Flow](../../understand/control-flow.md) §8 and the authored workspace standard: exact
  propagation inventory, captain status ownership, merge Issues and human landing.
  Acceptance was recorded at commit `2e31b1d0` before this plan.
- [DR 048](../decisions/048-one-level-static-gates-certainly-wrong-no-escape-hatch.md): gate set,
  Sonar cognitive semantics, HCRAP formula, selection rule and measured transition method.
- [Architecture](../../understand/architecture.md), [Glossary](../../reference/dydo-glossary.md)
  and [Writing Good Briefs](../../guides/writing-good-briefs.md): verified component boundaries and
  self-contained Issue contracts. Their obsolete statements are correction targets, not authority.
- `Commands/CheckDocValidator.cs`, `Commands/SyncCommand.cs`, `Services/TemplateGenerator.cs`,
  `DynaDocs.Tests/coverage/`, and `.github/workflows/`: concrete seams for removal and integration.

## 3. Design

Keep one authored source per skill and compile it into native artifacts. Protocol corrections belong
in `Templates/` first; the final integration owner alone regenerates `.agents/`, `.claude/` and
`.codex/`. Framework-owned local documents follow the same source/update direction. Do not regenerate
from the installed 2.2.9 binary or let a partially integrated sync overwrite work in flight.

Remove the summary rule and its dead wiring instead of replacing it with a configurable requirement.
Retain parser/index uses of optional summaries. Workflow retirement removes emission and dead APIs,
but retains bounded migration cleanup for known compiler-owned stale outputs.

For DR 048, use the existing Python gate entry point as the single local command. Add the smallest
project-owned metric producers and architecture checks the maintained stacks need. Test metric
semantics with flat switches, nested flow, boolean runs, local functions, constructors and absent
coverage. Inventory C#, Python and maintained npm JavaScript rather than silently treating this as
C# only. Generated, vendored and minified files may be excluded by ownership; legacy exemptions for
maintained entry points or awkward modules must be reconsidered under the selection rule.

Measure before dividing remediation: record counts by gate, stack and file in DYD-96, then give each
fix lane exclusive files. A gate catching good code requires a reasoned policy correction, not a
suppression. The final reviewer checks both fixes and gate credibility; a green but incomplete runner
does not satisfy this plan.

Rollback is ordinary reviewed Git history plus reinstalling the prior local package. Preserve the
user's unrelated `.obsidian/workspace.json` edit, custom runtime files and host settings. No sweeping
deletion of historical documents or private downstream files is authorized by this plan.

## 4. Implementation Issue map

The labels below are planning handles, not new work-record identities. Reuse the named Linear Issues
and wire actual blockers there; the admiral records newly needed keys before commissioning them.
All first delivery Issues are Type `Feature` (or `Bug` for S where the board contract warrants it),
Mode `AFK`, and begin `Todo`. Each gets the five-field contract and a specify hop before production.

### First pickable Issues

| Handle / existing Issue | Outcome | Exclusive owned paths | Blockers | Gate | Base branch |
|---|---|---|---|---|---|
| P / DYD-90 | Every current protocol contact agrees with DR 047; missing resources exist | `Templates/skill-*.template.md`, `Templates/*-resource-*.template.md`, `Templates/linear-workspace-standard.template.md`, `Templates/dydo-glossary.template.md`, `Templates/working-tree-contract.template.md`, `Templates/types.json.template`, `THIRD-PARTY-NOTICES.md`; current protocol guides/reference docs excluding G/C policy/setup paths; DR 047 metadata | None | A, P | `codex/dydo-3-consolidation` |
| S / DYD-98 | Summary absence is valid; broken links remain errors | `Rules/SummaryRule.cs`, `Rules/RuleBase.cs`, `Commands/CheckDocValidator.cs`, `Commands/FixFileHandler.cs`; matching rule/check/fix tests; `Templates/writing-docs.template.md`, local writing-docs guide | None | A, S | `codex/dydo-3-consolidation` |
| G / DYD-96 | Credible DR 048 mechanisms produce the local failure inventory | `DynaDocs.Tests/coverage/**`, new metric/architecture tests and producer directory explicitly named by specify; `.editorconfig`, `Directory.Build.props`, `DynaDocs.csproj`, test project file; gate dependency/config files; `Templates/coding-standards.template.md`, local coding/testing standards, coverage-tools and project glossary | None | A, G-measure | `codex/dydo-3-consolidation` |

P owns the hardener's policy wording and takes G's exact threshold/command correction as input.
G owns runner/policy files, never edits the hardener prompt. S transfers any additional summary
documentation correction to its owner instead of expanding ownership silently. Before dispatch,
replace P's narrative-doc group and G's new surfaces with explicit path lists on their Issues.

The current scratch rebaseline removing compiler-generated/coverage-attribute and maintained-entry-point
exemptions measured 76 modules, exposing three floor failures: `Program.cs` (0% line/branch),
`Rules/BrokenLinksRule.cs` (66.7% line) and `Commands/ValidateCommand.cs` (57.1% branch).
External-process CLI tests do not instrument the entry point. The run retains the twelve known
scaffold test failures. Cognitive, parameter, clone and cycle gates have not run.
Apparent `Services`↔`Utils` and `Services`↔`Commands` cycles deserve early measurement, especially
`TemplateGenerator`/`FolderScaffolder` references into command classes. This is useful triage input,
not a claim that the new gate set passes. The stack inventory also contains three maintained Python
scripts and four npm JavaScript files.

### Later bearings

| Handle / existing Issues | Outcome and promotion condition | Ownership and blockers |
|---|---|---|
| C / DYD-91, DYD-92, DYD-86, DYD-88 | Setup/compiler repair and fresh-install round trip; contracts ready once P source names and links settle | One serial owner for `Commands/SyncCommand.cs`, `Commands/InitCommand.cs`, `Services/TemplateGenerator.cs`, `Services/SkillTemplateService.cs`, compiler/config helpers and matching tests; getting-started source/template. P transfers link-fix ownership if needed. |
| G-fix / DYD-96 children | Resolve every measured valid failure and complete missing supported mechanisms | Measured inventory chooses exact source/test files. Wait for P/S/C owners before changing their files; no blanket production ownership. |
| B / DYD-94 and overlapping records | Correct status/label/template schema and reconcile work | Admiralty owns Linear mutations; no code. Read back settings; preserve DYD-47 historical non-acceptance. Reconcile DYD-66/92 and DYD-70/94; inspect DYD-37/39/40/41 instead of guessing closure. |
| I / DYD-75, DYD-11 | Generated parity, final protocol review, packages, installed candidate and landing PR | After P/S/C/G-fix: runtime outputs, template hashes, final doc indexes, release/CI workflows, package surfaces and evidence. `DynaDocs.csproj` transfers from G. |

### Exact gates

Run commands from the repository root using the source-built CLI. Store results with the candidate
SHA on the relevant Issue. These commands are required entry points; test names below refer to
existing suites, which the owning Issue extends with its new scenarios.

```powershell
# A: build and entire behavior suite; both exit 0
dotnet build DynaDocs.sln --warnaserror
python DynaDocs.Tests/coverage/run_tests.py -- --verbosity minimal

# S: summary-free valid document passes; broken link still fails; fix preserves optional prose
python DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~CheckDocValidatorTests|FullyQualifiedName~FixFileHandlerTests|FullyQualifiedName~BrokenLinksRuleTests"

# C: all integration selections and init/check/sync/update, including preserved custom output
python DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~InitCheckIntegrationTests|FullyQualifiedName~TemplateScaffoldingTests|FullyQualifiedName~CodexSyncArtifactsE2ETests|FullyQualifiedName~SyncCommandTests|FullyQualifiedName~TemplateCommandTests"

# G: preserve this public runner interface; final exit 0 requires every applicable gate
python DynaDocs.Tests/coverage/gap_check.py --force-run

# I: validate the repository, package and build the local native candidate
dotnet run --project DynaDocs.csproj --no-build -- check
dotnet pack DynaDocs.csproj -c Release -o artifacts/consolidation/nupkg
dotnet publish DynaDocs.csproj -c Release -r win-x64 --self-contained -o artifacts/consolidation/win-x64
npm pack ./npm --dry-run
```

G-measure is a successful, tested measurement mechanism plus a complete failure inventory; it may
exit 1 because it exposed legitimate defects. It is an intermediate gate only. G-final is the same
command returning 0 with measured line ≥80% and branch ≥60% per module, HCRAP ≤20 and cognitive ≤20
per method, at most seven non-constructor parameters, no forbidden dead code/nested ternaries, no
15-line/100-token clones and no dependency cycles. Producer/checker tests include failing fixtures
for each supported gate. Every non-trivial maintained module has a test file.

P is an independent `docs` review plus root review against the contact table and DR 047, including
captain two-step returns, released takeover, review FAIL, Merge FAIL, blocked resume, Questions and
human gates. These are protocol proofs, not string-presence tests. Final I repeats this review on
generated artifacts and linked consumer documentation as a newly entering agent would read them.

I's specify hop supplies a repeatable scratch-project script using the built executable for
`init all`, `check`, `sync`, and template update; it snapshots compiler-owned bytes before the second
sync and asserts no changes. Install the packaged tool into an isolated local tool path, run
`--version`, `--help` and the scratch flow through that installed command, then update the user's
local installation and verify which executable PATH resolves. Retain prior-package rollback detail.
Do not represent cross-platform AOT configuration inspection as a successful native build: collect
non-publishing CI matrix evidence before claiming all five targets validated.

## 5. Ordering and isolation

The root records the approved decision and plan, owns its independent project-plan review, and fixes
the feature branch/governing SHA before dispatch. The user's approval already supplies the human
gate; this planner does not ask again. First commission P, S and G within available agent capacity.
They may proceed independently, with one writer per worktree. Review each candidate independently.

Merge through captain-owned Merge Issues, serialize operations on the feature branch, and preserve
hop SHAs with merge commits. C follows P; measured G-fix lanes can run beside C only on disjoint
files. B proceeds alongside code. I starts after all required source changes and valid gate failures
are resolved. A later independent lane may merge first when blockers permit; Linear records the
actual order. The admiral directs; captains and their workers perform Git operations.

Only I updates compiler outputs and hashes in the integration checkout. Test builds are serialized
per worktree to avoid Windows DLL locks. A production defect found in integration goes back to its
owning Issue or a new bounded fix contract; do not hide it in regeneration or release packaging.

## 6. Watch-outs

- The repository check passed while fresh initialization failed twelve tests. Both contexts are
  mandatory evidence; a link to a DynaDocs-only DR is not a portable consumer dependency.
- Host settings are not runtime proof. Validate available nested spawning, write boundaries and
  parent wake/resume behavior; document version and actual observations. The portable fallback is
  fresh commission from the Linear record, not an invented polling service.
- A metric producer that silently loses uncovered or compiler-generated method bodies can make
  every score look good. Match coverage to source methods explicitly and account for omissions.
- DR 048 triage is the largest schedule uncertainty. Do not weaken gates or rename unresolved work
  as finishing polish to declare an overnight success.
- Preserve recent prompt prose unless a contract or readability defect warrants changing it.
  Shared terms should have one definition; deleting repetition is often the better correction.
- Work is ready for the human when its integrated evidence is ready. A locally installed 3.0.0
  binary alone does not mean release acceptance, and no open human gate becomes `Done` overnight.
