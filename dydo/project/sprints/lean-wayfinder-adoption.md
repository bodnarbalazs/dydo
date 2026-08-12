---
title: Lean Wayfinder Adoption and v2.2.6 Release
seq: 13
status: done
gate-result: PASS — audited 6d7688a0065105d728a32909b305544854d67315
area: project
type: context
---

# Lean Wayfinder Adoption and v2.2.6 Release

Ship the smallest coherent Wayfinder adoption, align it with dydo's PM model, and prove the
result through the published 2.2.6 package.

## 1. Specification

**Intent** — Ship a small set of prompt-level improvements inspired by Matt Pocock's skills while
preserving dydo's existing PM hierarchy and platform-owned execution model. Wayfinder adds an
optional Campaign navigation overlay for committed work whose route is foggy; it does not add a
runtime, tracker, PM Record type, or competing orchestration system. The release also restores MIT,
refreshes dydo's public positioning, and proves the installed package can update and compile this
repository as a downstream consumer.

**In scope**

- Three shipped skill-only templates: `wayfinder`, `grilling`, and `bro`.
- Locked definitions and cross-references that make Waypoint orthogonal to Campaign/Sprint/Slice/
  Task and keep execution in the current top-level manager plus native subagents.
- Conditional glossary routing without making every agent read both glossaries.
- Fresh-init, template-update, dual-runtime compilation, content, and link-resolution tests.
- README/about/npm positioning, Matt Pocock inspiration credit, MIT relicensing, and version 2.2.6.
- Regenerated runtime artifacts from canonical templates after preserving existing template-backed
  local work.
- Post-audit release: commit, push `master`, tag/push `v2.2.6`, wait 15 minutes, terminate the old
  `dydo.exe`, install the global NuGet tool, run `dydo template update` then `dydo sync`, and verify.

**Out of scope**

- A Waypoint frontmatter type, folder, Notion database, CLI, graph renderer, Chartr/Obsidian plugin,
  claim/release protocol, or top-level agent spawning.
- Compiler support for vendor invocation-policy metadata. Explicit-only behavior is expressed by
  precise skill descriptions in this release.
- A generic portable-skill/provenance/update subsystem.
- Standalone research, prototype, domain-modeling, router, setup, `to-spec`, or `to-tickets` skills.
- Sync-model lifecycle cleanup (`FutureFeature.shaping` and legacy Sprint/Slice statuses); active
  prompts must not endorse those terms, but schema correction needs its own design.
- Rewriting historical records or unrelated dirty files.

**Acceptance criteria**

1. The shipped glossary defines Campaign's optional Wayfinding map, Waypoint, Frontier, Fog, HITL,
   AFK, and FutureFeature with one consistent model: Waypoint is not a Record or Slice; delivery
   points to one Sprint, which alone decomposes into Slices.
2. `wayfinder` operates only on an active Campaign, works one frontier Waypoint at a time except
   bounded parallel AFK research, keeps Fog out of the backlog, and never spawns/co-ordinates
   top-level sessions or invents implementation outside normal planning.
3. `grilling` is a focused elicitation method used deliberately by co-thinker/Wayfinder; `bro`
   re-pitches the immediately preceding response in normal technical English without lowering its
   technical level.
4. Co-thinker, chief-of-staff, planner, plan reviewer, and orchestrator agree on promotion,
   incremental planning, and execution boundaries and point to canonical vocabulary rather than
   duplicating it.
5. Fresh init and existing-project template update ship all three templates; sync emits matching
   Claude/Codex skills and no native agent definition for them. Generated planner links resolve.
6. README/about/npm state the personal, evolving, opinionated nature of dydo, credit
   `https://github.com/mattpocock/skills` as inspiration, and all license/package surfaces say MIT.
7. All focused tests, `py DynaDocs.Tests/coverage/run_tests.py`, coverage gap check, `dydo check`,
   and the merged audit pass.
8. Tag `v2.2.6` publishes successfully; after 15 minutes a global `dotnet tool install -g dydo`
   installs 2.2.6, `dydo template update` materializes the new shipped templates in this project's
   `dydo/_system/templates/`, `dydo sync` emits the three skills, and final checks pass.

**Questions & answers**

- Is a Waypoint a Slice? No. It is an optional Campaign-map navigation node and not a PM Record;
  a delivery Waypoint points to a Sprint, whose reviewed plan owns its Slices.
- Is every Campaign a map? No. A Campaign may own a Wayfinding map only when the route cannot be
  responsibly planned at once. Clear work goes directly to the planner.
- Does Wayfinder launch top-level agents? No. The current human-facing manager invokes the skill;
  HITL stays in that conversation, AFK discovery may use bounded native subagents, and delivery
  uses the existing plan/review/orchestration workflow.
- Does Wayfinder operate on FutureFeatures? No. FutureFeatures are unscheduled hypotheticals;
  Wayfinding begins only after human promotion into an active Campaign.
- Are runtime changes needed? No. The existing `emit: skill` path distributes and compiles the
  templates. Hard vendor invocation policy and generic portable skills remain deferred.
- Which files are product source? Top-level `Templates/` and package metadata. This repository's
  `dydo/` is dydo's own project state; `.agents/`, `.claude/`, and `.codex/` are generated outputs.
- Release number? 2.2.6, the next unused patch after 2.2.5; a prompt-only shipped skill addition is
  compatible with the repository's patch-release cadence.

## 2. Prior art

- Matt Pocock's `wayfinder`, `grilling`, and `wait-what` skills were inspected at
  `https://github.com/mattpocock/skills`; the repository is MIT. Adopt the philosophy and minimal
  workflow, not its issue-tracker setup, `.plan` schema, or orchestration cockpit.
- `Templates/mode-self-improvement.template.md` proves a concise shipped `emit: skill` template;
  `Services/TemplateGenerator.cs`, `Services/RoleDefinitionService.cs`, and
  `Commands/SyncCommand.cs` prove automatic discovery, template distribution, and dual emission.
- `Templates/mode-co-thinker.template.md`, `Templates/mode-planner.template.md`,
  `Templates/mode-orchestrator.template.md`, and `Templates/reviewer-resource-plan.template.md`
  are the existing lifecycle/gate authorities; augment them instead of building parallel roles.
- `Templates/dydo-glossary.template.md` is the locked framework vocabulary; project-domain words
  stay in `Templates/glossary.template.md`.
- Decision 041 fixes the boundary: dydo authors and knows; native platforms run and coordinate.
- `.github/workflows/release.yml` publishes GitHub assets, NuGet, and npm from `v*` tags; 2.2.6 is
  absent locally and remotely at planning time.

Rejected alternatives: first-class Waypoint records (premature second hierarchy), Chartr as a
dependency (second runtime/schema), modifying `sync-model.template.json` in this release (scope
expansion), and hard invocation-policy support (compiler work unsupported by the lean intent).

## 3. Design

The Campaign body may contain a compact optional map with Destination/goal referenced once, settled
Outcomes, visible Waypoints, Fog, and Out of scope. Waypoints store route semantics and a compact
outcome/link only; canonical Decisions, notes, Tasks, Sprints, and Slices retain their existing
detail. The frontier is the actionable set whose prerequisites are settled—no claims in v1.

New skills are top-level `Templates/mode-*.template.md` with `emit: skill`. Scaffolding and
`dydo template update` copy them into downstream `dydo/_system/templates/`; `dydo sync` derives the
name from the filename and emits only `.claude/skills/<name>/SKILL.md` and
`.agents/skills/<name>/SKILL.md`. Avoid essential content under compiler-stripped headings. Trigger
descriptions carry the explicit/deliberate invocation boundary because compiler-level policy is
deferred.

Framework-updated docs (`dydo-glossary`, `how-to-use-docs`, mode templates) reach existing
downstreams through `dydo template update`; `entry-point` and `index` changes apply to fresh init.
This repository's project-owned `dydo/index.md` and `dydo/understand/work-model.md` are updated
separately for local harmony, never mistaken for distribution sources.

Existing dirty generated artifacts are preserved by treating their corresponding root templates
as canonical and running one deterministic sync after template changes. Unrelated task/issue/
handoff files are never staged. Rollback is one release revert plus deleting the `v2.2.6` tag only
if publication has not escaped; after publication, issue a corrective patch rather than rewriting
the public tag.

## 4. Slice map

| # | slice file | files touched (disjoint) | deps | gate |
|---|---|---|---|---|
| 1 | lean-wayfinder-adoption-1-skills | `Templates/mode-wayfinder.template.md`; `Templates/mode-grilling.template.md`; `Templates/mode-bro.template.md`; `DynaDocs.Tests/Services/TemplateGeneratorTests.cs`; `DynaDocs.Tests/Services/RoleDefinitionServiceTests.cs`; `DynaDocs.Tests/Integration/TemplateOverrideTests.cs`; `DynaDocs.Tests/Integration/InitCommandTests.cs`; `DynaDocs.Tests/Integration/TemplateCommandTests.cs`; `DynaDocs.Tests/Integration/CodexSyncArtifactsE2ETests.cs`; `DynaDocs.Tests/Commands/SyncCommandTests.cs`; `dydo/project/slices/lean-wayfinder-adoption-1-skills.md` | — | focused template/sync/init tests |
| 2 | lean-wayfinder-adoption-2-pm-harmony | `Templates/dydo-glossary.template.md`; `Templates/entry-point.template.md`; `Templates/index.template.md`; `Templates/how-to-use-docs.template.md`; `Templates/mode-co-thinker.template.md`; `Templates/mode-chief-of-staff.template.md`; `Templates/mode-planner.template.md`; `Templates/reviewer-resource-plan.template.md`; `Templates/mode-orchestrator.template.md`; `dydo/reference/dydo-glossary.md`; `dydo/guides/how-to-use-docs.md`; `dydo/index.md`; `dydo/understand/work-model.md`; `DynaDocs.Tests/Services/FolderScaffolderTests.cs`; `DynaDocs.Tests/Integration/InitCheckIntegrationTests.cs`; new `DynaDocs.Tests/Commands/WayfinderHarmonyTests.cs`; `dydo/project/slices/lean-wayfinder-adoption-2-pm-harmony.md` | 1 | focused content/link/docs tests + `dydo check` |
| 3 | lean-wayfinder-adoption-3-public-release | `README.md`; `Templates/about-dynadocs.template.md`; `dydo/reference/about-dynadocs.md`; `npm/README.md`; `dydo/understand/about.md`; `LICENSE`; `npm/LICENSE`; delete `CLA.md`; `DynaDocs.csproj`; `npm/package.json`; `DynaDocs.Tests/Commands/CommandDocConsistencyTests.cs`; `dydo/project/slices/lean-wayfinder-adoption-3-public-release.md` | 2 | command-doc consistency + build checks |
| 4 | lean-wayfinder-adoption-4-generated-and-e2e | `.agents/skills/chief-of-staff/SKILL.md`; `.agents/skills/co-thinker/SKILL.md`; `.agents/skills/orchestrator/SKILL.md`; `.agents/skills/planner/SKILL.md`; `.agents/skills/reviewer/SKILL.md`; `.agents/skills/reviewer/resources/plan.md`; `.agents/skills/test-writer/SKILL.md`; `.agents/skills/wayfinder/SKILL.md`; `.agents/skills/grilling/SKILL.md`; `.agents/skills/bro/SKILL.md`; `.claude/skills/chief-of-staff/SKILL.md`; `.claude/skills/co-thinker/SKILL.md`; `.claude/skills/orchestrator/SKILL.md`; `.claude/skills/planner/SKILL.md`; `.claude/skills/reviewer/SKILL.md`; `.claude/skills/reviewer/resources/plan.md`; `.claude/skills/test-writer/SKILL.md`; `.claude/skills/wayfinder/SKILL.md`; `.claude/skills/grilling/SKILL.md`; `.claude/skills/bro/SKILL.md`; `.claude/agents/reviewer.md`; `.claude/agents/test-writer.md`; `.codex/agents/reviewer.toml`; `.codex/agents/test-writer.toml`; `dydo/project/sprints/lean-wayfinder-adoption.md`; `dydo/project/slices/lean-wayfinder-adoption-4-generated-and-e2e.md`; `dydo/project/tasks/publish-and-adopt-dydo-v2-2-6.md` | 3 | sync, package smoke, full tests, gap check, `dydo check` |

## 5. Ordering & isolation

Run all four slices serially in the current tree. Slices 1–4 are file-disjoint, but slice 4 must
run after all template edits because `dydo sync` rewrites the generated runtime directories as one
compiler-owned set. Serial execution also avoids racing the pre-existing dirty generated files.

Each Slice ends with a path-literal commit containing only that Slice's row above. After Slice 4,
the merged reviewer audits that exact committed `master` HEAD and writes the PASS/FAIL verdict into
this Sprint record. The orchestrator then commits only that verdict/status change. A fresh,
read-only release-seal review verifies the resulting `master` HEAD. Let the two full 40-character
SHAs be `$implementationAuditSha` and `$sealedSha`; it must execute:

```powershell
if ((git branch --show-current).Trim() -ne 'master') { throw 'Seal must review master.' }
if ((git rev-parse HEAD).Trim() -ne $sealedSha) { throw 'HEAD is not the proposed seal.' }
$sealPaths = @(git diff --name-only $implementationAuditSha $sealedSha)
if ($sealPaths.Count -ne 1 -or $sealPaths[0] -ne 'dydo/project/sprints/lean-wayfinder-adoption.md') { throw "Post-audit delta is not metadata-only: $sealPaths" }
$beforeSprint = @(git show "${implementationAuditSha}:dydo/project/sprints/lean-wayfinder-adoption.md")
$afterSprint = @(git show "${sealedSha}:dydo/project/sprints/lean-wayfinder-adoption.md")
$normalizedBefore = $beforeSprint | Where-Object { $_ -notmatch '^(status:|gate-result:)' }
$normalizedAfter = $afterSprint | Where-Object { $_ -notmatch '^(status:|gate-result:)' }
if (($normalizedBefore -join "`n") -cne ($normalizedAfter -join "`n")) { throw 'Sprint content beyond status/gate-result changed after audit.' }
$afterStatus = @($afterSprint | Where-Object { $_ -match '^status:' })
$afterGate = @($afterSprint | Where-Object { $_ -match '^gate-result:' })
if ($afterStatus.Count -ne 1 -or $afterStatus[0] -ne 'status: done') { throw 'Sealed Sprint status is not done.' }
$expectedGate = "gate-result: PASS — audited $implementationAuditSha"
if ($afterGate.Count -ne 1 -or $afterGate[0] -cne $expectedGate) { throw 'Sealed gate-result does not name the audited SHA.' }
git diff --check $implementationAuditSha $sealedSha
if ($LASTEXITCODE -ne 0) { throw 'Seal diff check failed.' }
py DynaDocs.Tests/coverage/run_tests.py
if ($LASTEXITCODE -ne 0) { throw 'Release-seal tests failed.' }
py DynaDocs.Tests/coverage/gap_check.py --force-run
if ($LASTEXITCODE -ne 0) { throw 'Release-seal coverage gap check failed.' }
dydo check
if ($LASTEXITCODE -ne 0) { throw 'Release-seal dydo check failed.' }
```

It then checks pre-release acceptance criteria 1–7 against the named implementation audit and
these fresh gates. Criterion 8 is the separate operational Task's post-release success gate and is
not required to authorize itself. The seal report must end with exactly
`RELEASE-SEAL PASS <sealedSha>` or
`RELEASE-SEAL FAIL <sealedSha>` plus findings. No implementation or generated file may change
during sealing, and only the PASS form authorizes the release Task.

Only after that seal passes does the separate operational Task
`dydo/project/tasks/publish-and-adopt-dydo-v2-2-6.md` own the irreversible release and post-release
adoption. It creates no release commit: it pushes local `master` and tags the exact sealed SHA.
Publication is deliberately not a Slice because it consumes the audited and sealed Sprint rather
than becoming unaudited implementation inside it. Unrelated modified task records, issue 0308, and
`HANDOFF-fix-command-failure.md` remain uncommitted and byte-for-byte unchanged throughout.

## 6. Watch-outs

- Do not copy the superseded first-class Waypoint/claim/Notion proposal from the working note.
- Do not call Waypoint a Record, work item, ticket, Task, Sprint, or Slice.
- Do not make every agent preload the glossary; route conditionally at vocabulary-sensitive seams.
- Do not treat unresolved Campaign Fog as a Sprint specification gap unless the current increment
  depends on it.
- Do not edit generated runtime artifacts by hand or stage unrelated dirty files.
- Do not promise hard explicit-invocation enforcement; the current compiler does not emit it.
- `dydo template update` must precede final `dydo sync`, because downstream local templates win.
- Killing and replacing the global tool is an authorized destructive external action, but confirm
  the installed version before mutating downstream templates.

## Plan review

PASS — fresh-eyes review completed before implementation and authorized all four Slices.

## Implementation result

PASS — the canonical templates compiled once in the repository to 24 deterministic changed
runtime artifacts. The six new skills exist for Claude and Codex, no matching native agent
definitions exist, and all changed generated files are LF-only. Full isolated tests passed with
2,538 passed and 10 expected live-test skips; coverage passed 131/131 modules; `dydo check` found
0 errors and 13 known orphan warnings. Release build, 2.2.6 pack, local tool installation, external
fresh init, external sync, version assertion, artifact assertions, and the bounded Slice-owned
diff check all passed. The release candidate is ready for the merged Sprint audit.
