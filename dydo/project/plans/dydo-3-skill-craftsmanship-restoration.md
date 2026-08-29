---
title: Restore skill craftsmanship
status: reviewed
area: project
type: context
linear-project: https://linear.app/bodnar-balazs/project/dydo-30-restore-skill-craftsmanship-eb5c80041c27
---

# Restore skill craftsmanship

Restore dydo's shared Claude/Codex skills to concise, deliberate methodology before 3.0 ships. The
modern pre-accretion sources at `0f06c947` are the structural baseline; Matt Pocock-derived skills are
rebuilt from their upstream originals at `6654f6b60cd9d5be8b54c6fafe44346dabeb3b76`, with only thin
Linear, dydo, and runtime bindings. This plan changes agent-facing prompts and their compiler, not the
dydo documentation tree or live-work ontology.

## 1. Specification

### Intent

Make every skill easy to discover, hard to misinvoke, small enough to understand in one reading, and
faithful to its actual job. Remove task-specific sediment, duplicated doctrine, host-specific plumbing,
and generic professional advice. Linear appears only where a role creates, navigates, executes, or
reviews live work; Git/dydo remains the durable knowledge boundary.

### In scope

- Rename authored `mode-<name>.template.md` sources to `skill-<name>.template.md` without changing
  their bodies as part of that lane.
- Restore the shared entry prompt to project identity, `dydo/index.md`, and the Linear-live/Git-durable
  boundary; update this repository's `AGENTS.md` and `CLAUDE.md` to match.
- Restore the 13 existing skills from the strongest applicable baseline, retaining only current
  behavior that still earns its place.
- Rebuild Wayfinder and Grilling from Matt Pocock's originals. Remove the invented Waypoint ontology;
  add a separate explicit `grill-me` wrapper; keep the reusable `grilling` primitive tracker-neutral.
- Restore Bro's missing context and ubiquitous-language guidance while keeping its dydo name.
- Add `writing-for-agents` as the one new upstream skill in this Project because it directly governs
  prompt and skill craftsmanship.
- Compile explicit-invocation policy into native Claude and Codex metadata instead of relying on prose.
- Preserve upstream attribution and MIT notice for substantially adapted text.
- Regenerate project-local templates, Claude/Codex skills, and native agents from canonical sources.

### Out of scope

- No documentation-tree restructuring, PM ontology change, Notion work, release, tag, or publication.
- No return of Campaign/Sprint/Slice, local Task/Issue, claim/release, queue, watchdog, or roster
  mechanics.
- No new generic doctrine, workflow hierarchy, memory system, or repository mirror of Linear state.
- Do not add `diagnosing-bugs` or `codebase-design` in this Project. They are good candidates, but adding
  more skills before the current set is restored would repeat the accretion failure.
- Do not hand-edit generated artifacts except in the final deterministic regeneration lane.

### Acceptance criteria

- The shared entry prompt contains no memory policy, kaizen mandate, glossary choreography, host-specific
  behavior, or claim that almost every task needs a skill.
- The full kaizen method lives only in `self-improvement`; Chief of Staff and Orchestrator may each carry
  one role-specific routing sentence. Co-thinker carries no separate kaizen doctrine.
- Each authored description says what triggers the skill and distinguishes its nearest neighbor when
  ambiguity is realistic. Delegated workers identify themselves as delegated workers.
- Manager skills state their own boundary once; the repeated Tier-1/Managers Doctrine block is gone.
- Linear is absent from general communication, writing, testing, design, and Grilling methodology except
  at a genuine handoff boundary.
- Wayfinder preserves upstream map, fog, frontier, native blockers, assignment, chart/work modes, and
  human-facing titled-reference semantics, adapted to an active Linear Project and dydo durable evidence.
- `wayfinder`, `bro`, and `grill-me` are explicit-only in both supported runtimes; `grilling` remains
  model-invokable. Invocation policy is verified from generated artifacts.
- Generated descriptions equal authored descriptions; the generic compiler suffix is gone.
- Canonical sources, project-local copies, `.agents/skills`, `.claude/skills`, `.claude/agents`, and
  `.codex/agents` are byte-derived and repeat generation is idempotent.
- No copied upstream text ships without attribution. Tests verify behavior and emitted metadata rather
  than freezing prose fragments.
- Release build, focused compiler/template tests, full isolated suite, coverage, `dydo check`, template
  parity, `git diff --check`, fresh skill-quality review, and integrated audit all pass.

### Questions and answers

- **What is the restoration baseline?** `0f06c947` for the mature dydo roles, selectively—not a blind
  revert. Obsolete runtime mechanics stay deleted.
- **How much Linear coupling is correct?** Native Linear objects and relationships in Wayfinder,
  Planner, Orchestrator, Chief of Staff, and worker/reviewer handoffs; nowhere else without a concrete
  reason.
- **Where does kaizen belong?** The method remains in `self-improvement`; only roles that observe repeated
  cross-workstream or execution friction get one routing sentence.
- **Which upstream additions land now?** Only `writing-for-agents`. `diagnosing-bugs` and
  `codebase-design` remain explicit follow-up candidates.
- **Who invokes Wayfinder?** The human explicitly invokes it for an active Linear Project. Other skills
  may recommend it, not invoke it autonomously.

### Role-by-role restoration contract

| Skill | Restore and retain | Remove or relocate |
|---|---|---|
| Chief of Staff | Triage, status, mediation, board hygiene; Linear graph; one compressed identifier rule; one self-improvement routing sentence | Memory sweep, lifecycle promise, Tier-1 doctrine, autonomous Wayfinder invocation |
| Co-thinker | Curious joint exploration; discover facts before questions; durable conclusions; Linear/FutureFeature handoff only at the end | Tier-1 doctrine, workflow choreography, verbose identifier block, kaizen doctrine |
| Code writer | Delegated worker implementing one reviewed Linear Issue; small coherent change; tests and evidence returned to caller | Sprint/Slice language, commit/merge authority, generic coding tutorial, repeated global gates |
| Docs writer | Delegated worker producing one reviewed documentation change; repository truth, links, examples, generated-source rule | Sprint/Slice language, generic writing tutorial, PM routing beyond the incoming contract |
| Inquisitor | One milestone sweep lens or one adversarial verification; concrete findings only; distinct from loop review | Duplicated review gates, implementation authority, ceremony not required by the lens |
| Orchestrator | Executes reviewed Linear Issues/Project plans through workers; monitors, integrates, audits; one self-improvement routing sentence | Tier-1 block, Codex task-ID/callback mechanics, durable status mirror, generic escalation scripts |
| Planner | Produces reviewed intent for one atomic Linear Issue or one coordinated Project plan; never implements | Retired PM nouns, speculative decomposition, rigid prose that does not close a decision |
| Reviewer | Independent per-change/contract gate with target-specific resources; distinct from Inquisitor | Theatrical filler, repeated coverage tutorial, implementation or dispatch authority |
| Self-improvement | Existing evidence threshold, one-lever method, authority check, rollback, harness-only boundary | Global invocation mandate and duplicated copies in unrelated skills |
| Test writer | Delegated worker proving behavior for one reviewed Issue; seam-first tests; evidence returned to caller | Sprint/Slice language, generic testing tutorial, repeated project-wide gate policy |

The July text is evidence for voice and structure, not text to paste blindly. Every retained line must
fit the table above and the current Linear/Git/runtime boundary.

## 2. Prior art

- Git history identifies `0f06c947` as the strongest modern low-ceremony baseline. `818f87a6` added the
  largest recent Linear-era accretion; `7d1032e8` duplicated identifier doctrine; `6781d209` added global
  memory routing.
- Upstream source: [mattpocock/skills at `6654f6b6`](https://github.com/mattpocock/skills/tree/6654f6b60cd9d5be8b54c6fafe44346dabeb3b76).
  The current dydo Wayfinder and Grilling preserve names and intent but not the original operating
  method. Bro is a defensible but incomplete adaptation of `wait-what`.
- The repository already has a neutral source compiler and runtime-specific emitters. Extend that seam;
  do not put Codex or Claude mechanics into shared methodology.

## 3. Design

Canonical `skill-*.template.md` files contain the shared method plus neutral emission metadata. Add
`invocation: explicit|automatic` to that frontmatter. The compiler maps `explicit` to Claude
`disable-model-invocation: true` and Codex `agents/openai.yaml` with
`policy.allow_implicit_invocation: false`; `automatic` omits those restrictions. Authored descriptions
pass through unchanged. Runtime-specific callback, permission, and UI mechanics stay in native emitters
or workflows.

`grill-me` requires no dependency schema. Its explicit-only skill body is a minimal human-facing alias:
load the separately generated `grilling` skill, apply it to the current topic, and do not act until the
Grilling completion confirmation is satisfied. Both native skill directories must contain both skills;
tests prove the pointer target exists.

The entry prompt is deliberately tiny and separately parity-tested because existing root prompts are
not refreshed by `dydo sync`. Project-local template copies remain supported overrides; in this dogfood
repository they are updated mechanically and must not diverge from the shipped sources without an
explicit project customization.

The 3.0 filename migration changes persisted `frameworkHashes` keys in `dydo.json` but not its schema:

- A hash-tracked stock `mode-*` file migrates atomically to `skill-*`: verify the on-disk normalized
  hash against its stored or known built-in hash, create the new file, transfer ownership to the new
  hash key, then delete the old file.
- A hash-tracked but modified `mode-*` file is never deleted. Remove stale framework ownership and warn
  that the user must rename it manually.
- An untracked custom `mode-*` file is preserved, is not compiled in 3.0, and produces the same explicit
  rename warning. It is never silently ignored.
- If both names exist, `skill-*` is the active source; preserve and warn on `mode-*`; overwrite neither.
- Rollback restores shipped names and hash keys through the Project revert. User-owned legacy files were
  never mutated, so rollback does not need to reconstruct them.

Substantially adapted upstream templates carry a short source comment that survives generation. The
full Matt Pocock MIT notice, pinned source URL, and commit live in root `THIRD-PARTY-NOTICES.md`; an
identical `npm/THIRD-PARTY-NOTICES.md` ships in the npm package. `DynaDocs.csproj` packs the root notice
beside the README, and `npm/package.json` includes the npm notice. Packaging tests inspect both archives.

## 4. Implementation Issue map

| Issue | Outcome | Exclusive surface | Blockers | Gate |
|---|---|---|---|---|
| P6-1 | Rename sources with the four-case legacy migration above | `Commands/HelpCommand.cs`, `Commands/SyncCommand.cs`, `Commands/TemplateCommand.cs`, `Models/RoleDefinition.cs`, `Services/RoleDefinitionService.cs`, `Services/TemplateGenerator.cs`, existing filename-contract tests/docs, all 26 source/install filename moves, `dydo.json` hash keys | — | Gate A |
| P6-2 | Restore the minimal entry prompt and current root parity | `Templates/entry-point.template.md`, root `AGENTS.md`, root `CLAUDE.md`, new `DynaDocs.Tests/Integration/EntryPointParityTests.cs` | P6-1 | Gate B |
| P6-3 | Apply the role-by-role table to mature dydo roles | the ten named non-Matt `Templates/skill-*.template.md` files, their ten `_system` copies, `dydo/_system/template-additions/**`, and the three existing semantic contract tests named below | P6-1 | Gate C |
| P6-4 | Restore Matt-derived fidelity and add `writing-for-agents` | five named shipped templates and five `_system` copies; root/npm notices; `DynaDocs.csproj`; `npm/package.json`; new `DynaDocs.Tests/Integration/UpstreamSkillSourceTests.cs` | P6-1 | Gate D |
| P6-5 | Compile exact descriptions and invocation policy | `Models/RoleDefinition.cs`, `Services/RoleDefinitionService.cs`, `Commands/SyncCommand.cs`, `DynaDocs.Tests/Services/RoleDefinitionServiceTests.cs`, `DynaDocs.Tests/Commands/SyncCommandTests.cs`, `DynaDocs.Tests/Integration/CodexSyncArtifactsE2ETests.cs` | P6-1, P6-3 | Gate E |
| P6-6 | Regenerate and audit the combined skill system | `.agents/skills/**`, `.claude/skills/**`, `.claude/agents/**`, `.codex/agents/**`, `dydo.json`, `dydo/project/migrations/3.0-skill-craftsmanship-assimilation.md`, generated migration/plan hubs | P6-2, P6-3, P6-4, P6-5 | Gate F |

P6-1 and P6-5 both touch the compiler/model files and therefore run serially. P6-3 owns
`DynaDocs.Tests/Commands/ChiefOfStaffSyncTests.cs`, `DynaDocs.Tests/Commands/SyncCommandTests.cs`, and
`DynaDocs.Tests/Integration/CodexSyncArtifactsE2ETests.cs` because those tests currently freeze the
Memory Sweep, Managers Doctrine, and Codex callback sediment that P6-3 must remove. P6-5 follows P6-3
before extending the latter two tests for invocation metadata. P6-4 owns only its exclusive new source
test. Every Issue receives one fresh independent review before commit.

P6-1's non-move path set is closed: `Commands/HelpCommand.cs`, `Commands/SyncCommand.cs`,
`Commands/TemplateCommand.cs`, `Models/RoleDefinition.cs`, `Services/RoleDefinitionService.cs`,
`Services/TemplateGenerator.cs`, `dydo.json`, `Templates/dydo-commands.template.md`,
`Templates/writing-docs.template.md`, `dydo/guides/customizing-roles.md`,
`dydo/guides/migrating-dydo-1x-to-2x.md`, `dydo/understand/architecture.md`,
`dydo/understand/templates-and-customization.md`, and the generated
`dydo/guides/_index.md`, `dydo/reference/dydo-commands.md`, and `dydo/reference/writing-docs.md`. Its
test paths are exactly `ChiefOfStaffSyncTests.cs`, `SyncCommandTests.cs`, `CliEndToEndTests.cs`,
`DocumentationTests.cs`, `InitCommandTests.cs`, `TemplateCommandTests.cs`,
`TemplateOverrideTests.cs`, `BrokenLinksRuleTests.cs`, `FrontmatterRuleTests.cs`,
`SummaryRuleTests.cs`, `HubGeneratorTests.cs`, `RoleDefinitionServiceTests.cs`,
`TemplateGeneratorTests.cs`, `TemplateUpdateTests.cs`, and `RuleSkipPathsTests.cs` under their existing
`DynaDocs.Tests` folders. The remaining P6-1 paths are the 13 shipped and 13 installed
`mode-*`→`skill-*` moves. No other path is authorized.

### Exact gates

Run all commands from the repository root in the Issue worktree.

**Gate A — filename migration**

```powershell
dotnet build DynaDocs.sln -c Release --no-restore
py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~TemplateOverrideTests|FullyQualifiedName~TemplateCommandTests|FullyQualifiedName~TemplateGeneratorTests|FullyQualifiedName~TemplateUpdateTests|FullyQualifiedName~RoleDefinitionServiceTests|FullyQualifiedName~SyncCommandTests"
py DynaDocs.Tests/coverage/gap_check.py --force-run
git diff --check
```

The independent review also runs the four migration fixtures verbatim and compares normalized body
hashes for every old/new source pair. Active code/docs/tests may contain `mode-*` only in those four
legacy fixtures and the migration warning.

**Gate B — entry prompt**

```powershell
dotnet build DynaDocs.sln -c Release --no-restore
py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~EntryPointParityTests|FullyQualifiedName~InitCommandTests"
dotnet bin/Release/net10.0/dydo.dll check
git diff --check -- Templates/entry-point.template.md AGENTS.md CLAUDE.md DynaDocs.Tests/Integration/EntryPointParityTests.cs
```

**Gate C — mature role sources**

```powershell
dotnet build DynaDocs.sln -c Release --no-restore
rg -n "Tier-1|Managers Doctrine|visible Codex task|Memory sweep|stay active until dismissed|Campaign|Sprint|Slice" Templates/skill-chief-of-staff.template.md Templates/skill-co-thinker.template.md Templates/skill-code-writer.template.md Templates/skill-docs-writer.template.md Templates/skill-inquisitor.template.md Templates/skill-orchestrator.template.md Templates/skill-planner.template.md Templates/skill-reviewer.template.md Templates/skill-self-improvement.template.md Templates/skill-test-writer.template.md
py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~ChiefOfStaffSyncTests|FullyQualifiedName~SyncCommandTests|FullyQualifiedName~CodexSyncArtifactsE2ETests"
git diff --check -- Templates dydo/_system/templates dydo/_system/template-additions
```

The `rg` command must return no hits. A fresh skill-quality review checks each row of the restoration
table against `0f06c947` and the current boundary; it is the semantic gate and must PASS before commit.

**Gate D — upstream fidelity**

```powershell
dotnet build DynaDocs.sln -c Release --no-restore
py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~UpstreamSkillSourceTests"
dotnet pack DynaDocs.csproj -c Release --no-build --output artifacts/skill-restoration-pack
$npmReceipt = npm pack ./npm --pack-destination artifacts/skill-restoration-pack --json | ConvertFrom-Json
$nugetPackages = @(Get-ChildItem artifacts/skill-restoration-pack -Filter '*.nupkg')
if ($nugetPackages.Count -ne 1) { throw "Expected one NuGet package, found $($nugetPackages.Count)" }
$nugetPackage = $nugetPackages[0]
$nugetArchive = [IO.Compression.ZipFile]::OpenRead($nugetPackage.FullName)
try { if ($nugetArchive.Entries.FullName -notcontains 'THIRD-PARTY-NOTICES.md') { throw 'NuGet notice missing' } } finally { $nugetArchive.Dispose() }
$npmPackage = Join-Path artifacts/skill-restoration-pack $npmReceipt[0].filename
if ((tar -tf $npmPackage) -notcontains 'package/THIRD-PARTY-NOTICES.md') { throw 'npm notice missing' }
git diff --check -- Templates dydo/_system/templates THIRD-PARTY-NOTICES.md npm/THIRD-PARTY-NOTICES.md DynaDocs.csproj npm/package.json DynaDocs.Tests/Integration/UpstreamSkillSourceTests.cs
```

The source test checks template existence, neutral invocation metadata, the `grill-me` pointer target,
source comments, notice identity, and package inclusion—not prose equality. A fresh reviewer compares
Wayfinder, Grilling, Grill Me, Bro, and Writing for Agents against pinned upstream `6654f6b6`, accounting
only for the adaptations authorized here.

**Gate E — compiler and invocation metadata**

```powershell
dotnet build DynaDocs.sln -c Release --no-restore
py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~RoleDefinitionServiceTests|FullyQualifiedName~SyncCommandTests|FullyQualifiedName~CodexSyncArtifactsE2ETests"
py DynaDocs.Tests/coverage/gap_check.py --force-run
git diff --check -- Models/RoleDefinition.cs Services/RoleDefinitionService.cs Commands/SyncCommand.cs DynaDocs.Tests/Services/RoleDefinitionServiceTests.cs DynaDocs.Tests/Commands/SyncCommandTests.cs DynaDocs.Tests/Integration/CodexSyncArtifactsE2ETests.cs
```

Fixtures cover `explicit` and `automatic`, invalid values, exact description passthrough, Claude
frontmatter, Codex `agents/openai.yaml`, absent restrictions for automatic skills, and repeat emission.

**Gate F — integrated delivery**

```powershell
dotnet build DynaDocs.sln -c Release --no-restore
$generatedRoots = '.agents/skills','.claude/skills','.claude/agents','.codex/agents'
dotnet bin/Release/net10.0/dydo.dll sync
$firstHashes = Get-ChildItem $generatedRoots -File -Recurse | Sort-Object FullName | ForEach-Object { [pscustomobject]@{ Path = (Resolve-Path -Relative $_.FullName); Hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash } }
dotnet bin/Release/net10.0/dydo.dll sync
$secondHashes = Get-ChildItem $generatedRoots -File -Recurse | Sort-Object FullName | ForEach-Object { [pscustomobject]@{ Path = (Resolve-Path -Relative $_.FullName); Hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash } }
if (($firstHashes | ConvertTo-Json -Compress) -ne ($secondHashes | ConvertTo-Json -Compress)) { throw 'Generated output changed on second sync' }
dotnet bin/Release/net10.0/dydo.dll validate
dotnet bin/Release/net10.0/dydo.dll check
dotnet bin/Release/net10.0/dydo.dll template update
dotnet bin/Release/net10.0/dydo.dll template update --diff
py DynaDocs.Tests/coverage/run_tests.py
py DynaDocs.Tests/coverage/gap_check.py --force-run
git diff --check
```

The two hash snapshots must contain the same closed path set and identical SHA-256 values. The first
template-update command is the only authorized reconciliation of `dydo.json`: it must change only the
15 `skill-*.template.md` framework-hash keys so they equal the normalized built-in contents. The
following `--diff` command must report zero pending framework updates. P6-6 then repeats Gate D's exact
NuGet/npm creation and archive-entry assertions, removes the disposable
`artifacts/skill-restoration-pack` directory, and receives a fresh integrated audit against this exact
plan plus a narrow final documentation review of the assimilation record.

## 5. Ordering and isolation

P6-1 is already isolated in its own worktree and lands first, but its reviewed commit must satisfy the
amended four-case migration contract before merge. P6-2, P6-3, and P6-4 may then run in parallel: their
exact source and test paths are disjoint. P6-5 starts after P6-3 because both legitimately update two
compiler contract tests. None commits generated output. P6-6 integrates P6-2, P6-3, P6-4, then P6-5,
resolves only generated-output collisions, regenerates once, proves idempotence, and runs the combined
audit.

## 6. Watch-outs

- Do not mistake synchronized slop for a healthy source pipeline.
- Do not perform a blind revert: pre-3.0 PM and runtime ceremony must remain deleted.
- Do not summarize upstream skills from memory. Start from pinned source text and document each
  adaptation.
- Do not use Linear nouns as decoration. Every occurrence must identify a real object or handoff.
- Do not encode explicit invocation only in prose.
- Do not let exact-wording tests freeze another bad draft; test emitted structure and behavior.
- Do not complete or ship dydo 3.0 from this Project.
