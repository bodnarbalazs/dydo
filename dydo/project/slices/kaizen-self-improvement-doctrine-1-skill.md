---
title: Ship the Self-Improvement Skill
sprint: kaizen-self-improvement-doctrine
seq: 1
status: ready
area: general
type: context
---

# Slice 1 — Ship the Self-Improvement Skill

Author and compile the bounded kaizen method without touching unrelated generated skills.

## Spec fragment

Add one concise, generic, skill-only mode template that turns recurring agent-harness friction
into one small, durable, correctly routed harness improvement. Product features and product code
are never eligible. Compile the template for both skill surfaces without creating an agent
definition and without touching any unrelated dirty generated skill.

Accepted means the role is discovered as `self-improvement` with `EmitAgent == false`; both new
compiled skill files exist and are byte-identical; neither agent-definition file exists; the
skill contains every locked trigger, method, routing, and boundary below; and every gate passes.
Both regenerated outputs must contain the exact product-exclusion sentence locked below.

## Implementation detail

Touch only:

- `Templates/mode-self-improvement.template.md`
- `.claude/skills/self-improvement/SKILL.md`
- `.agents/skills/self-improvement/SKILL.md`
- `DynaDocs.Tests/Services/TemplateGeneratorTests.cs`
- `DynaDocs.Tests/Services/RoleDefinitionServiceTests.cs`
- `DynaDocs.Tests/Commands/SyncCommandTests.cs`
- `DynaDocs.Tests/Integration/CodexSyncArtifactsE2ETests.cs`
- `DynaDocs.Tests/Integration/TemplateOverrideTests.cs`

### Canonical template

Create `Templates/mode-self-improvement.template.md` with the following complete content
verbatim, apart from the repository's normal line-ending convention:

```markdown
---
mode: self-improvement
description: Turns recurring friction into one small, durable harness improvement without expanding task scope.
emit: skill
---

# Self-Improvement

Your job: turn recurring friction into one small, durable improvement to the harness.

Here, the harness means agent prompts, skills, nudges, hooks, agent-workflow documentation and
process surfaces, and harness implementation code. It excludes the product being built.

## Mindset

Kaizen is continuous improvement through small changes. `1.01^365 ≈ 37.8` illustrates
compounding; it is not a promise, a metric, or a reason to manufacture changes.

## Trigger

Use this skill when the same agent-harness failure, correction, workaround, or avoidable friction
appears at least twice in the available evidence, or an existing canonical harness record already
identifies it as recurring. Product behavior never triggers this skill. A one-off harness
inconvenience is not enough. A single severe harness defect follows the ordinary issue path only
when the current task authorizes that record; otherwise report it to the human.

## Method

1. **Establish evidence** — Name the repeated symptom, occurrences, affected workflow, and likely
   root cause. If recurrence is unsupported, stop.
2. **Deduplicate** — Search existing issues, backlog, decisions, guides, pitfalls, prompts,
   skills, nudges, and hooks. Prefer an existing canonical surface.
3. **Choose one lever** — Select exactly one smallest durable change in this order: canonical
   prompt or skill wording; a warn-level nudge for a recognizable risky action; a hook only when
   action-time guidance or enforcement is demonstrably required; harness implementation code
   only when the earlier layers cannot express the behavior.
4. **Classify, then check authority** — Choose the narrowest destination below. Create or modify
   it only when the current task explicitly includes that edit and the current role, slice, and
   normal reviewed workflow permit it. Otherwise create or modify nothing: report the evidence
   and suggest exactly one destination/change to the human.
5. **Define verification and rollback** — State what recurrence should stop, how to test that
   outcome, and how to remove the change if it creates noise or unintended constraints.

## Destinations

These are harness classifications, not standing authorization. None routes product work.

- Observed defect → issue.
- Schedulable improvement not yet accepted → backlog record.
- Accepted, non-obvious policy → decision.
- Stable operational guidance → the narrowest existing guide or pitfall.
- Authorized trivial prompt or nudge repair → its canonical source, then its normal compiler or
  sync gate.
- Project facts, incident state, and temporary workarounds → never memory; route or retire them
  only when authorized.

## Boundaries

- This skill grants no authority. The current role, user request, slice, and reviewed workflow
  still govern every edit, including record creation or modification.
- Do not propose or perform product-feature or product-code changes, including benevolent or
  otherwise authorized adjacent product work; kaizen here applies only to the agent harness and
  its documentation and process surfaces.
- Do not widen the current task, fix adjacent problems, create a generic doctrine record, or make
  more than one proposal for the same pattern.
- Do not edit generated artifacts; edit their canonical template and compile normally.
- Do not change global or user-level prompts, hooks, settings, memories, or policy without
  explicit task authority and the normal review or approval path.
- Do not escalate a reminder into enforcement, or a warning into a block, without recurrence
  evidence, proportionality, and the normal review or approval path.
- Do not create a recursive self-improvement loop. Apply this method once to the observed pattern,
  then return to the task.
- If no small credible improvement survives these checks, report the pattern and stop rather than
  inventing machinery.
```

Do not add a must-read section, external quotation, platform name, include/resource file, or any
other prose.

### Discovery and compiler regressions

- In `TemplateGeneratorTests.ReadBuiltInTemplate_AllListedTemplates_AreAccessible`, add
  `mode-self-improvement.template.md` to the theory data. Extend the embedded-resource assertion
  to prove that template is embedded.
- In `RoleDefinitionServiceTests.DiscoverRoles_FindsAllShippedRoles`, assert
  `self-improvement` is present. In `DiscoverRoles_EmitShapes_MatchTheNativePivot`, assert its
  `EmitAgent` value is false. The existing all-descriptions test proves its description.
- Add a focused `SyncCommandTests` fact that discovers the role, calls both
  `SyncSkillOnlyRole` and `SyncCodexSkill` against `_testDir`, then asserts both skill files exist,
  have identical content, contain these exact strings from the locked body — `Kaizen is continuous
  improvement through small changes.`, `1.01^365 ≈ 37.8`, `Product behavior never triggers this
  skill.`, `These are harness classifications, not standing authorization. None routes product
  work.`, `Otherwise create or modify nothing`, `including record creation or
  modification.`, `harness implementation code only when the earlier layers cannot express the
  behavior.`, `Do not propose or perform product-feature or product-code changes, including
  benevolent or otherwise authorized adjacent product work; kaizen here applies only to the agent
  harness and its documentation and process surfaces.`, `Define verification and rollback`, and
  `never memory` — and contain no carriage returns. Assert both agent-definition paths are absent.
- Extend `CodexSyncArtifactsE2ETests.Sync_Tier1Modes_EmitSkillOnly_NoCodexAgentRoleFiles` by
  renaming it to describe all shipped skill-only modes and adding `self-improvement` to its role
  array. Preserve the worker-role contrast.
- In `TemplateOverrideTests.GetAllTemplateNames_ReturnsExpectedTemplates`, add
  `Assert.Contains("mode-self-improvement.template.md", templateNames)`, update the adjacent
  inventory comment from `9 mode templates` to `10 mode templates`, preserve the five reviewer
  resource templates, and change `Assert.Equal(14, templateNames.Count)` to
  `Assert.Equal(15, templateNames.Count)`. Make no other change in that file.

### Shared-tree protection manifest

The orchestrator, not the slice implementer, performs this before creating the lane. From the
shared dirty worktree, capture its absolute root and one entry for each exact path below:

```text
.agents/skills/chief-of-staff/SKILL.md
.agents/skills/co-thinker/SKILL.md
.agents/skills/orchestrator/SKILL.md
.agents/skills/planner/SKILL.md
.agents/skills/reviewer/SKILL.md
.agents/skills/test-writer/SKILL.md
.claude/agents/reviewer.md
.claude/agents/test-writer.md
.claude/skills/chief-of-staff/SKILL.md
.claude/skills/co-thinker/SKILL.md
.claude/skills/orchestrator/SKILL.md
.claude/skills/planner/SKILL.md
.claude/skills/reviewer/SKILL.md
.claude/skills/test-writer/SKILL.md
.codex/agents/reviewer.toml
.codex/agents/test-writer.toml
```

Each immutable manifest entry contains `SharedRoot`, `RelativePath`, the exact scoped output of
`git -C <SharedRoot> status --short -- <RelativePath>`, the exact scoped output of
`git -C <SharedRoot> diff --cached --name-status -- <RelativePath>`, and the file's SHA-256. Pass
that complete manifest verbatim in the lane dispatch brief. The implementer confirms it was
received but does not recalculate it in the clean lane: clean-lane bytes are not evidence about
the shared dirty tree.

The orchestrator uses this exact snapshot algorithm before lane creation and again after merge:

```powershell
$sharedRoot = (git rev-parse --show-toplevel).Trim()
if ([string]::IsNullOrWhiteSpace($sharedRoot)) { throw 'shared worktree root was not resolved' }
$sharedRoot = (Resolve-Path -LiteralPath $sharedRoot).Path
$protectedPaths = @(
    '.agents/skills/chief-of-staff/SKILL.md',
    '.agents/skills/co-thinker/SKILL.md',
    '.agents/skills/orchestrator/SKILL.md',
    '.agents/skills/planner/SKILL.md',
    '.agents/skills/reviewer/SKILL.md',
    '.agents/skills/test-writer/SKILL.md',
    '.claude/agents/reviewer.md',
    '.claude/agents/test-writer.md',
    '.claude/skills/chief-of-staff/SKILL.md',
    '.claude/skills/co-thinker/SKILL.md',
    '.claude/skills/orchestrator/SKILL.md',
    '.claude/skills/planner/SKILL.md',
    '.claude/skills/reviewer/SKILL.md',
    '.claude/skills/test-writer/SKILL.md',
    '.codex/agents/reviewer.toml',
    '.codex/agents/test-writer.toml'
)

function Get-ProtectedManifest([string]$root, [string[]]$paths) {
    @($paths | ForEach-Object {
        $relativePath = $_
        $absolutePath = Join-Path $root $relativePath
        if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) {
            throw "protected path is missing: $absolutePath"
        }
        [ordered]@{
            SharedRoot = $root
            RelativePath = $relativePath
            Status = (@(git -C $root status --short -- $relativePath) -join "`n")
            Staged = (@(git -C $root diff --cached --name-status -- $relativePath) -join "`n")
            Sha256 = (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash
        }
    })
}

$beforeManifest = @(Get-ProtectedManifest $sharedRoot $protectedPaths)
$beforeManifestJson = $beforeManifest | ConvertTo-Json -Depth 3 -Compress
```

Pass `beforeManifestJson` verbatim with the absolute `sharedRoot` in the dispatch brief. After
merge, restore `$protectedPaths`, `$sharedRoot`, and `$beforeManifestJson` from that brief, define
the same function, and run:

```powershell
$afterManifest = @(Get-ProtectedManifest $sharedRoot $protectedPaths)
$afterManifestJson = $afterManifest | ConvertTo-Json -Depth 3 -Compress
if ($afterManifestJson -cne $beforeManifestJson) {
    throw "protected shared-tree manifest changed`nBEFORE $beforeManifestJson`nAFTER  $afterManifestJson"
}
```

After the lane commits merge, the orchestrator returns to the manifest's same absolute
`SharedRoot`, recalculates the same fields for the same paths, and requires exact entry-by-entry
equality. Any missing path, changed hash, changed status, or changed staged state blocks the
sprint. Do not repair a mismatch with reset, restore, checkout, or stash.

### Isolated generation of checked-in outputs

All steps below run inside the dedicated lane worktree, never the manifest's shared dirty root.
Use a disposable project that cannot see or rewrite repository generated artifacts:

1. Confirm the dispatch brief contains the 16-entry shared-tree protection manifest above and
   that the current lane root is not equal to its `SharedRoot`.
2. Build the lane source with `dotnet build DynaDocs.csproj --no-restore` after the focused test
   restore/build has succeeded. Resolve the built `dydo.dll` to an absolute path.
3. Create a uniquely named directory directly under `[IO.Path]::GetTempPath()`. Change location
   into it inside `try/finally`, invoke the absolute source-built DLL with `init none`, then invoke
   it with `sync`. Always restore the original location.
4. Assert the scratch project contains both `self-improvement/SKILL.md` files and neither agent
   definition. Assert the skill files are byte-identical.
5. Regenerate and replace both owned `self-improvement/SKILL.md` outputs by copying those exact
   scratch files to their matching repository paths. Do not copy any other generated artifact.
6. Before recursively deleting the scratch directory, resolve its absolute path and prove it is a
   strict descendant of the system temp directory. Then remove only that exact directory.
7. Return to the lane root. Do not inspect, stage, or change any protected shared-tree path here;
   the orchestrator performs the meaningful comparison against the shared root after merge.

Do not hand-edit the generated files. If generation produces content that violates the contract,
fix the template or compiler test, regenerate in a new scratch project, and copy only the two
outputs again.

## Out of scope for this slice

- Entry-point template/current entry files and `InitCommandTests` — slice 2 owns them.
- Compiler production code, resources, hooks, nudges, config, records other than this slice, and
  all existing generated roles.
- Release/version work.

## Gate

Run in order and require every command/assertion to pass. The `$slicePaths` array contains exactly
the eight files owned by this slice; stage all eight before inspecting the cached diff:

```powershell
py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~TemplateGeneratorTests|FullyQualifiedName~RoleDefinitionServiceTests|FullyQualifiedName~SyncCommandTests|FullyQualifiedName~CodexSyncArtifactsE2ETests|FullyQualifiedName~TemplateOverrideTests"
py DynaDocs.Tests/coverage/run_tests.py
py DynaDocs.Tests/coverage/gap_check.py --force-run
dotnet run --project DynaDocs.csproj -- check
$slicePaths = @('Templates/mode-self-improvement.template.md', '.claude/skills/self-improvement/SKILL.md', '.agents/skills/self-improvement/SKILL.md', 'DynaDocs.Tests/Services/TemplateGeneratorTests.cs', 'DynaDocs.Tests/Services/RoleDefinitionServiceTests.cs', 'DynaDocs.Tests/Commands/SyncCommandTests.cs', 'DynaDocs.Tests/Integration/CodexSyncArtifactsE2ETests.cs', 'DynaDocs.Tests/Integration/TemplateOverrideTests.cs')
git add -- $slicePaths
if ($LASTEXITCODE -ne 0) { throw "exact staging failed: $LASTEXITCODE" }
$actual = @(git diff --cached --name-only)
$difference = @(Compare-Object ($slicePaths | Sort-Object) ($actual | Sort-Object))
if ($difference.Count -ne 0) { throw "staged paths differ from slice allowlist: $($difference | Out-String)" }
git diff --cached --check -- $slicePaths
if ($LASTEXITCODE -ne 0) { throw "cached diff check failed: $LASTEXITCODE" }
```

Also require the isolated-generation assertions above. The orchestrator's post-merge shared-tree
manifest comparison is a sprint completion gate, not a lane command.
