---
title: Simplify the skill model
status: draft
area: project
type: context
linear-project: https://linear.app/bodnar-balazs/project/dydo-30-simplify-the-skill-model-cddfdcbb2848
---

# Simplify the skill model

Cut the compiler's leftover "role" model and every customisation path the human has abandoned, so
that a skill template is one shipped `skill-<name>.template.md` whose frontmatter is the whole
metadata and whose compiled output derives from the embedded set alone. Runs under today's tooling —
the `master`-era compiled skills and hands-on sub-agent dispatch by the planning session — in
parallel with the human's file-by-file template pass (DYD-64) on `DYD-64-human-pass`, from whose tip
`9875c9a6` this Project branches.

## 1. Specification

### Intent

dydo 3.0 is a breaking boundary and the product serves one human. The compiler still calls every
skill template a "role", binds model tiers under `models.roles`, keeps C# fallback copies of documents
it always ships as templates, and carries a template-override, template-update, model-cap, path-set
and legacy-migration apparatus that nobody uses. After this Project the code names what the
[glossary](../../reference/dydo-glossary.md) names — a skill template that may also emit an agent —
reads one metadata key set from one place, fails loudly instead of falling back, and carries the
generator changes DYD-75 asked for.

### In scope

- `models.roles` becomes `models.agents`; the legacy `dydo.json` upgrades go (S-1).
- The C# fallback document strings go (S-3).
- The project-local template layer goes: no `dydo/_system/templates/` mirror, override, custom
  skill, include re-anchoring, template hash, or `mode-*` legacy handling (S-2).
- `dydo model cap|status|uncap`, `models.fallback`, and the guard's cap restore go (S-4).
- `paths`, tool-scoped file nudges, `models.efforts`, and the project `name` go (S-5).
- `RoleDefinition` becomes `SkillTemplate`, with its service, discovery, parameter and test names (S-6).
- Frontmatter key `name:` replaces `mode:` on every shipped skill template; the compiler requires it
  to equal the filename slug (S-7).
- DYD-75 items 2–4: `web: true`, a compiled `scout` agent, `argument-hint` pass-through (S-8).
- Closeout: migration notes for the human's two projects, the audit, the assimilation brief (S-9).

### Out of scope

- The prose of any `Templates/*.template.md` and of every document under `dydo/`. The human's
  DYD-64 pass owns the templates, and the human's rule of 2026-09-02 covers both: outside removing or
  renaming a straight reference to something this Project cuts, no prompt or doc file is rewritten
  here. The whole licence is: add a frontmatter key; rename `mode` to `name` and `models.roles` to
  `models.agents` where they are cited; delete the line, row or section that describes a cut feature;
  move the scout body verbatim under a frontmatter block; replace the one dead `resources/scout.md`
  link with the `scout` agent's name. No replacement prose, new table row, classification sentence,
  softened clause or invented argument hint; every gap left is listed on its Issue and in the
  assimilation brief for the human's pass.
- Regenerating installed and compiled output: `.claude/**`, `.codex/**`, `.agents/**`, and the
  hash-tracked framework docs beyond what `dydo template update` writes. That is DYD-75 item 1, after
  DYD-64. Deleting `dydo/_system/templates/**` is in scope (S-2).
- The retired-artifact sweeps in `SyncCommand` (`RetiredManagedRoles`, `RetiredWorkflows`,
  `RetiredSkillResources`) and `TemplateCommand` (`RetiredBinaryFiles`, `RetiredDocFiles`): they still
  clean the human's two initialised projects on their next sync and update; a later bearing retires them.
- `{{include:name}}` additions under `dydo/_system/template-additions/`: both of the human's projects
  use them; they stay, and so does `ResolveIncludes`.
- `dydo template update` for the six framework-owned documents, `_system/types.json`, and the nudge
  and scan-exclude defaults: stays, minus `--force`.
- Performing the LC project's migration: documented, not executed.
- Model tier defaults, the `integrations` toggles, bash-command nudges, the guard's tiers.

### Acceptance criteria

Proved at the final merge of `feature/simplify-skill-model`, from the repository root. `CODE` below
means `Commands Services Models Utils Rules Serialization Program.cs DynaDocs.Tests -g '!DynaDocs.Tests/Fixtures/**'`
(the audit-transcript fixtures quote retired paths and are never scrubbed).

1. `rg -n "models\.roles|\"roles\"|UpgradeLegacy|MigrateHashFormat" CODE dydo.json dydo/reference dydo/guides dydo/understand -g '!dydo/guides/migrating-dydo-2x-to-3x.md'` prints nothing (the migration guide names the old key on purpose); `dydo.json` carries `models.agents`.
2. `rg -n "GenerateFallback|catch \(FileNotFoundException\)" Services/TemplateGenerator.cs DynaDocs.Tests` prints nothing.
3. `rg -n "RoleDefinition|DiscoverRoles|roleName|IRoleDefinitionService" CODE` prints nothing.
4. `rg -n "^mode:" Templates` prints nothing; every `Templates/skill-<x>.template.md` carries `name: <x>`; a test proves `SkillTemplateService` throws `InvalidDataException` naming the file when `name:` is missing or differs from the filename slug.
5. `dydo/_system/templates/` is absent from the repository and from a fresh `dydo init`; `rg -n "_system/templates|GetProjectTemplatesPath|GetProjectSkillTemplateNames|IncludeReanchor|mode-\*|MigrateLegacy|TryGetLegacySkillPath|FrameworkTemplateFiles" CODE dydo.json` prints nothing; every `frameworkHashes` key in `dydo.json` names a file under `reference/` or `guides/`.
6. `rg -n "ModelCap|model cap|\"fallback\"|RestoreExpired" CODE dydo.json` prints nothing; `dotnet bin/Release/net10.0/dydo.dll help` lists no `model` command.
7. `rg -n "PathSets|pathSets|CheckFileNudges|MatchesFileNudgePattern|Efforts|\"efforts\"|\"paths\"" CODE dydo.json` prints nothing; `DydoConfig` has no `Paths` or `Name` member and `NudgeConfig` no `Tools`.
8. `dydo init` then `dydo sync` in a scratch directory compiles: `.claude/agents/research.md` with `WebFetch, WebSearch` in `tools:`; `.codex/agents/research.toml` with `web_search = true`; `.claude/agents/scout.md` with both web tools and none of `Edit`, `Write`, `Agent`; `.claude/skills/handoff/SKILL.md` with `argument-hint:`; `.agents/skills/handoff/agents/openai.yaml` with `interface.default_prompt`. `handoff` and `teach` carry `argument-hint:` at source with the upstream texts; the other four human commands are listed for the human's pass.
9. `dotnet test DynaDocs.Tests --nologo -v q` reports only the §4 stage baseline red; every test an Issue adds is green.
10. `dotnet bin/Release/net10.0/dydo.dll check dydo` reports no finding that `9875c9a6` did not.
11. `dydo/guides/migrating-dydo-2x-to-3x.md` names every removed `dydo.json` key, the `dydo/_system/templates/` deletion and the model-cap marker directory as the human's migration for a 2.x or early-3.0 project.
12. `dydo/project/migrations/3.0-skill-model-simplification-assimilation.md` exists with what changed, what was learned, and what remains — including every template prose line the human's pass must revisit.

### Questions and answers

- **Which key names a template?** `name:`, and the compiler checks it: the Agent Skills spec and the
  upstream sources use `name:`, the compiled SKILL.md already emits it, and a mismatch is a defect
  worth failing on. The human's uncommitted `mode:` → `skill:` rename on the main checkout is
  superseded by S-7 and discarded by the planning session. (Human, 2026-09-02.)
- **How does the compiler know what a template compiles to?** The filename prefix `skill-` makes it
  a skill template; any other `*.template.md` is a document or resource template. `emit: agent` adds
  a spawnable agent; `name:` is identity only. S-7 does not write that sentence into the docs (the
  human's no-rewrite rule); it lists the gap for the human's pass. (Human's question, 2026-09-02;
  answered from the compiler.)
- **Which customisation paths go?** All four offered — project-local templates, model caps, `paths`
  with file-scoped nudges, `efforts` and `name` — plus every legacy migration, per the 3.0 stance.
  (Human, 2026-09-02.)
- **Where is a new skill authored after S-2?** In dydo's own `Templates/`, like every shipped skill:
  the product is the human's, and a skill only one project needs is still a shipped skill.
- **Does `--force` survive on `dydo template update`?** No. Its documented purpose — writing past an
  include tag the update could not re-anchor — leaves with re-anchoring; `--diff` stays.
- **Which tier does `scout` bind to?** `standard`, like `research`, in `models.agents`; the human may
  retune it. (Planning session's call, flagged on S-8.)
- **What of DYD-75?** Items 2–4 and its two small items become S-8 here; item 1 (reflection) stays on
  DYD-75, blocked by S-8 and DYD-64. (Human, 2026-09-02.)
- **Argument hints for skills upstream leaves without one?** Upstream at `6654f6b6` carries
  `argument-hint` only on `handoff` and `teach`. S-8 adds those two with the upstream texts; `grill-me`,
  `bro`, `walkthrough` and `improve-codebase-architecture` get none from this Project — their wording
  is the human's — and are listed for the pass.

## 2. Prior art

- The handoff of 2026-09-02 from the human's template session: the three symptoms, the 3.0 stance,
  the ownership boundary, the baseline-red list, and the IDE auto-staging hazard. Every cited line was
  verified against the tree at `9875c9a6`.
- Commit `9875c9a6` "fold test-writer into code-writer": the complete touchpoint set for retiring a
  shipped template (`ConfigFactory` defaults, `dydo.json`, and the allowlists in `SyncCommandTests`,
  `RoleDefinitionServiceTests`, `TemplateGeneratorTests`, `TemplateOverrideTests`).
- [DR 028](../decisions/028-model-tier-abstraction.md): tier → vendor model; kept, keyed by agent.
  [DR 002](../decisions/002-template-update-system.md): the template update system; retired for
  templates, kept for framework documents. [DR 045](../decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md)
  §2 taxonomy, §7 gates, §9 invocation policy, §10 guard and compiler.
- Read in full at `9875c9a6`: `Services/RoleDefinitionService.cs`, `Services/TemplateGenerator.cs`,
  `Commands/SyncCommand.cs`, `Commands/TemplateCommand.cs`, `Commands/GuardCommand.cs`
  (`CheckFileNudges`, `RestoreExpiredModelCapsIfDue`), `Services/ModelCapService.cs`,
  `Services/ConfigFactory.cs`, `Services/FolderScaffolder.cs`, `Models/*Config.cs`. The cut lists in
  §3 and §4 come from them, not from the handoff alone.
- DYD-75's body: web tools, the scout fence, `argument-hint`; the Codex `tools` toggles noted in
  `SyncCommand.BuildCodexAgent` (issue 0271) and `agents/openai.yaml` in `WriteCodexInvocationPolicy`.
- Upstream mattpocock/skills at `6654f6b60cd9d5be8b54c6fafe44346dabeb3b76`: `name:` on every
  SKILL.md; `argument-hint: "What will the next session be used for?"` on `handoff` and
  `argument-hint: "What would you like to learn about?"` on `teach`; none on `grill-me`, `wait-what`,
  `improve-codebase-architecture`.
- The human's LC project (`C:\Users\User\Desktop\LC`): `dydo/_system/templates/` holds thirteen 2.x
  `mode-*` mirror copies and five reviewer resource copies (two under retired names, three stale mirrors
  of current names), no custom skill; `dydo/_system/template-additions/` holds six additions in use.
  Hence overrides go, includes stay, and LC's migration is one directory delete.
- `dydo/reference/dydo-glossary.md` — Role, Hat, Worker, Method, Human command: the taxonomy the code
  should name; "role" remains a glossary word for an authored skill source, so prose may keep it while
  code identifiers say skill template and agent.

## 3. Design

### Shape

- `Models/SkillTemplate.cs`: `Name`, `Description`, `TemplateFile`, `EmitAgent`, `ReadOnly`,
  `Delegates`, `ExplicitInvocation`, `Web`, `ArgumentHint`. Frontmatter keys: `name` (required, equal
  to the filename slug), `description`, `emit`, `read-only`, `delegates`, `invocation`, `web`,
  `argument-hint`. Unknown keys are ignored so the human's upstream-shaped frontmatter never breaks a sync.
- `Services/SkillTemplateService.cs`: `DiscoverSkills()` enumerates the embedded `skill-*.template.md`
  set (source `Templates/` in dev-mode; retired names excluded) and `Parse(templateFile, content)` turns
  one template into a `SkillTemplate` — a pure function the tests exercise directly now that
  project-local fixtures are gone. An invalid `invocation`, a missing `name`, or a `name` that differs
  from the filename slug throws `InvalidDataException` naming the file.
- `Commands/SyncCommand.cs`: `ResolveModel(models, agentName)` returns the vendor model or null. Claude
  agent tools are `Read, Grep, Glob, Bash, Skill`, plus `Edit, Write` unless read-only, plus `Agent`
  when delegating, plus `WebFetch, WebSearch` when `web: true`. The Codex agent adds
  `[tools]` / `web_search = true` when `web: true`. `agents/openai.yaml` is written when the skill is
  explicit or carries an argument hint (`policy.allow_implicit_invocation: false`,
  `interface.default_prompt: <hint>`) and removed otherwise; the Claude SKILL.md gains `argument-hint:`
  when set. `SyncRole`/`SyncCodexRole`/`SyncSkillOnlyRole` become `SyncAgent`/`SyncCodexAgent`/`SyncSkill`;
  `RetiredManagedRoles` becomes `RetiredSkills`; `IsReadOnlyRole` is inlined.
- `Services/TemplateGenerator.cs`: reads templates from embedded resources (source `Templates/` in
  dev-mode) only; every `Generate*Md` reads its template or throws `FileNotFoundException`;
  `ResolveIncludes` keeps its project-root lookup of `dydo/_system/template-additions/`;
  `GetAllTemplateNames` stays as the shipped inventory the tests enumerate.
- `Commands/TemplateCommand.cs`: `dydo template update [--diff]` handles `FrameworkDocFiles`,
  `_system/types.json`, nudge and scan-exclude defaults, and the retired binary and doc cleanups.
  `Services/IncludeReanchor.cs`, `FrameworkTemplateFiles`, `FrameworkBinaryFiles`,
  `FrameworkGeneratedFiles`, `UpdateBinaryFile` and the `.unplaced`/`.backup` paths are deleted.
- `dydo.json` after the Project: `version`, `structure`, `integrations`, `models { tiers, agents }`,
  `scanExclude`, `nudges`, `frameworkHashes` (framework docs only). `Serialization/DydoJsonContext.cs`
  loses `PathsConfig`, `ModelCap`, `ModelCapBinding`.
- `Models/ModelsConfig.cs`: `Tiers`, `Agents`. `Services/ConfigFactory.cs`: `CreateDefaultModels` binds
  the `emit: agent` skills plus `scout`; no upgrade helpers; the `dotnet run` nudge pattern lists the
  surviving commands only.
- `dydo init` scaffolds `_system/template-additions/` and `_system/.local/` but no `_system/templates/`;
  `FolderScaffolder.StoreInitialFrameworkHashes` covers the framework docs only;
  `FixFileHandler.IsExcludedPath` no longer names the template folder.
- `Utils/RuleSkipPaths.cs` drops `TemplatesPrefix`: `IsTemplateOrAddition` becomes the addition-only
  predicate (renamed `IsTemplateAddition`), and every rule test that used a `_system/templates/` path as
  its skipped example uses `_system/template-additions/` instead.
- Guard: `CheckFileNudges`, `ApplyFileNudge`, `MatchesFileNudgePattern`, `RestoreExpiredModelCapsIfDue`
  and their call sites go; `GlobMatcher` stays for `OffLimitsService`.

### Invariants

- Compiled output is byte-derived from `Templates/`, `dydo.json` and `dydo/_system/template-additions/`;
  a second `dydo sync` is a no-op.
- No compatibility shim: an old `dydo.json` key is ignored by System.Text.Json and named in the
  migration guide; nothing reads it.
- A skill template that cannot be parsed fails `dydo sync` with its file name; nothing falls back silently.
- Every Issue's suite run reports the stage baseline in §4 and nothing else red.
- No Issue commits under `.claude/`, `.codex/` or `.agents/`; `git status --porcelain` on those paths
  is empty at every gate.

### Hazards

- **Concurrent template edits.** The human's DYD-64 pass changes prose on `DYD-64-human-pass` while
  S-7 changes line 2 of all 25 skill templates. S-7's template change is exactly
  `sed -i 's/^mode: /name: /' Templates/skill-*.template.md` followed by `unix2dos` on those files,
  recorded on the Issue, so a merge conflict on any template is resolved by taking the human's file
  and re-running the line. The compiler check then fails loudly on any template still carrying
  `mode:` — which is the point.
- **Hot files.** `SyncCommand.cs`, `TemplateGenerator.cs`, `ConfigFactory.cs`, `ModelsConfig.cs`,
  `GuardCommand.cs`, `dydo.json`, `SyncCommandTests.cs`, `RoleDefinitionServiceTests.cs` and
  `customizing-roles.md` are touched by several Issues; §5 serialises them. Only S-1 and S-3 run in
  parallel, on disjoint files.
- **Line endings.** `core.autocrlf=true`: sources are CRLF on disk and LF in the index. A scripted
  edit (`sed`, heredoc, Python) is followed by `unix2dos` on the touched file, or the diff shows every
  line. `.claude/**` stays LF per `.gitattributes`.
- **IDE auto-staging.** Rider stages new files on creation, and the human may commit from the main
  checkout mid-session. Every commit is preceded by `git diff --cached --stat` and `git log -1`, and
  stages by explicit path.
- **Baseline-red tests.** Fourteen integration tests are red at `9875c9a6` (listed in §4); none is
  this Project's. S-2 deletes two as moot. If a cut turns others green or changes their failure, the
  Issue records which and why; it does not chase them otherwise.
- **Framework-doc twins.** `Templates/dydo-commands.template.md` and `dydo/reference/dydo-commands.md`
  must stay identical (`CommandDocConsistencyTests`). From S-2 on, an Issue that edits the template
  runs `dotnet bin/Release/net10.0/dydo.dll template update` so the installed copy and its hash follow;
  that run also rewrites `guides/working-tree-contract.md` and reports `reference/dydo-glossary.md` as
  user-edited — both pre-existing on the branch and owned by DYD-64 / DYD-75 — so the Issue stages
  only the two doc twins it owns and reverts the rest (`git checkout -- dydo/guides/working-tree-contract.md`).
  `files-off-limits.md` is project-owned: the template and the installed copy are edited identically by hand.
- **Docs that describe cut features.** `README.md`, `npm/README.md`, `dydo/understand/*.md`,
  `dydo/guides/*.md` and `dydo/reference/*.md` get straight reference deletions only: the sentence,
  row, bullet or section that describes the cut feature goes, and nothing is written in its place —
  a section that becomes false as a whole (the Add and Override sections of `customizing-roles.md`)
  goes as a whole. `Templates/about-dynadocs.template.md` gets only the deletion of its two false
  lines (the `_system/templates/` bullet and tree row at base); its installed copy follows through
  `template update`. Every deletion is listed on its Issue for the human's pass.
- **Custom-skill tests.** Every test that today writes a `dydo/_system/templates/skill-*.template.md`
  fixture (four `skill-*` and one `mode-*` in `SyncCommandTests`, eight in `RoleDefinitionServiceTests`,
  three `skill-*` and four `mode-*` in `TemplateCommandTests`) is rewritten in S-2 against `Parse(templateFile, content)` or a constructed
  `RoleDefinition` compiled through the existing `SyncRole`/`SyncCodexRole` seams — the behaviour
  (delegates → `Agent`, read-only → tool profile, explicit invocation, invalid invocation) keeps its proof.

### Migration (the human's projects)

Written by S-9 into `dydo/guides/migrating-dydo-2x-to-3x.md` as one list, no prose beyond it: delete `dydo/_system/templates/`; in
`dydo.json` rename `models.roles` to `models.agents`, delete `models.efforts`, `models.fallback`,
`paths`, `name`, and every `frameworkHashes` key under `_system/templates/`; delete
`dydo/_system/.local/model-caps/` if present; run `dydo template update`, `dydo sync`, `dydo check`.
For LC that directory delete also removes its thirteen `mode-*.template.md` copies and five stale resource copies.

### Rollback

Each Issue lands as one `--no-ff` merge on `feature/simplify-skill-model`; `git revert -m 1 <merge>`
removes it. `master` and `DYD-64-human-pass` are untouched until the human lands the feature branch.

## 4. Implementation Issue map

### First pickable Issues

Base branch for every Issue: `feature/simplify-skill-model` (from `DYD-64-human-pass` at
`9875c9a6`). Issue branch `DYD-<n>-<slug>` in `../DynaDocs.worktrees/DYD-<n>-<slug>`. Every Issue is
`Improvement` + `AFK`. "Only" after a file name means that file is shared with another Issue and this
Issue edits nothing else in it.

Owned paths are exclusive where Issues run in parallel (S-1 with S-3). Along the serial chain an
Issue's surface is what its row names plus every file its cut demonstrably breaks — a compile error, a
test green at the Issue's base that the cut turns red, or a hit from its own Gate R — each listed on
the Issue with a one-line reason; the reviewer confirms each such extra change is a straight
consequence of the cut and nothing more. A region another Issue names stays off-limits beyond that
consequence.

| Issue | Outcome | Owned paths | Blockers | Gate | Base branch |
|---|---|---|---|---|---|
| S-1 | `models.roles` is `models.agents` in code, config and docs; `ResolveModel(models, agentName)`; `UpgradeLegacyPlannerRole`, `UpgradeLegacyOpenAiTierDefaults` and `MigrateHashFormat` are gone with their tests; the migration guide names the key rename | `Models/ModelsConfig.cs`; `Services/ConfigFactory.cs`; `Commands/TemplateCommand.cs` (`ApplyConfigDefaults`, `MigrateHashFormat` and its call only); `Commands/SyncCommand.cs` (`ResolveModel` and its two call sites only); `dydo.json` (`models.roles` key only); `dydo/reference/configuration.md` (the `models` rows, the schema block's `"roles"` key line and the Model tiers paragraph only); `dydo/guides/customizing-roles.md` (Model tier section and its Related line only); `dydo/guides/migrating-dydo-2x-to-3x.md`; tests `ConfigFactoryTests.cs`, `SyncCommandTests.cs` (`Roles`/`ResolveModel` spots only), `TemplateCommandTests.cs` (`TemplateUpdate_MigratesLegacyOpenAiModelDefaults` only), `TemplateUpdateTests.cs` (`MigrateHashFormat_*` only) | — | S, R1, D | `feature/simplify-skill-model` |
| S-3 | Every `Generate*Md` in `TemplateGenerator` reads its embedded template or throws; the ten `GenerateFallback*Md` bodies and their nine tests are gone (`GenerateFallbackDydoGlossaryMd` has none) | `Services/TemplateGenerator.cs` (the `Generate*Md` / `GenerateFallback*Md` region only); `DynaDocs.Tests/Services/TemplateGeneratorTests.cs` (the fallback tests only) | — | S, R2 | `feature/simplify-skill-model` |
| S-2 | Templates are read from the embedded set only: no `dydo/_system/templates/` scaffold, mirror, override, custom skill, include re-anchoring, template hash, `mode-*` handling, or `--force`; `DiscoverRoles()` takes no project root; `Parse(templateFile, content)` is the testable seam; the 35-file mirror is deleted from this repository and its hashes pruned; the docs and README lose every line that described it | `Services/TemplateGenerator.cs` (outside S-3's region); `Services/RoleDefinitionService.cs`; `Services/FolderScaffolder.cs`; `Services/IFolderScaffolder.cs`; `Services/IncludeReanchor.cs` (delete); `Commands/TemplateCommand.cs` (outside S-1's spots); `Commands/SyncCommand.cs` (`Execute`, `WarnAboutLegacyModeTemplates`, `CleanRetiredArtifacts`, `ExtractMethodology`, `ExtractMustReads` and the class-summary sentence that names `dydo/_system/templates/` only); `Commands/InitCommand.cs` (hash call only); `Commands/FixFileHandler.cs`; `Utils/RuleSkipPaths.cs`; `dydo/_system/templates/**` (delete); `dydo.json` (`frameworkHashes`: the template entries and the `reference/about-dynadocs.md` and `reference/dydo-commands.md` hash values only); `README.md` (template lines); `Templates/about-dynadocs.template.md` (two line deletions); `Templates/dydo-commands.template.md` + `dydo/reference/dydo-commands.md` (`dydo template update` section); `dydo/reference/about-dynadocs.md` (via `template update` only); `THIRD-PARTY-NOTICES.md`, `npm/THIRD-PARTY-NOTICES.md` (the one `_system/templates/` clause each); `Templates/template-additions-readme.md` + `dydo/_system/template-additions/_README.md` (the re-anchoring sentence only); `dydo/understand/templates-and-customization.md`; `dydo/understand/architecture.md` (template lines); `dydo/guides/customizing-roles.md` (outside S-1's section); `dydo/reference/configuration.md` (Customization points and `frameworkHashes` row only); `dydo/guides/troubleshooting.md`, `dydo/guides/adding-a-command.md` (mentions only); tests `TemplateOverrideTests.cs` (rename to `TemplateScaffoldingTests.cs`; keep the template-additions, framework-doc hash and `ReadBuiltInTemplate_*` tests, delete the rest), `InstalledTemplateParityTests.cs` (delete), `IncludeReanchorTests.cs` (delete), `TemplateUpdateTests.cs`, `TemplateCommandTests.cs`, `RoleDefinitionServiceTests.cs`, `SyncCommandTests.cs` (project-local fixtures only), `FolderScaffolderTests.cs`, `InitCommandTests.cs`, `InitCheckIntegrationTests.cs` (`FreshInit_TemplatesAreExcludedFromCheck` delete only), `DocumentationTests.cs` (`Fix_DoesNotRenameTemplateFiles`, `Fix_DoesNotCreateHubFilesInSystemFolders` and `Fix_DoesNotReportManualFixesForTemplates` only), `ChiefOfStaffSyncTests.cs` (the project-local copy fixture and its `DiscoverRoles` call only), `WayfinderHarmonyTests.cs` (`DiscoverRoles` call sites only), `FixFileHandlerTests.cs` (the `_system/templates` exclusion test only), `RuleSkipPathsTests.cs`, `DocScannerTests.cs`, `Rules/{BrokenLinks,Frontmatter,FolderMetaFiles,Naming,HubFiles,OrphanDocs,Summary}RuleTests.cs`, `CommandDocConsistencyTests.cs`, `TemplateGeneratorTests.cs` and `CodexSyncArtifactsE2ETests.cs` (their `_system/templates` spots only), `EndToEnd/CliEndToEndTests.cs` (`TemplateUpdate_EndToEnd_UserAddedInclude` and `TemplateUpdate_EndToEnd_RepeatedUserEdits` only) | S-1, S-3 | S, R5, D | `feature/simplify-skill-model` |
| S-4 | `dydo model` and every model-cap seam are gone: command, service, models, `models.fallback`, guard restore, completions, help, JSON context, nudge word list, docs and READMEs | `Commands/ModelCommand.cs`, `Services/ModelCapService.cs`, `Models/ModelCap.cs`, `Models/ModelCapBinding.cs` (delete); `Models/ModelsConfig.cs` (`Fallback` only); `Services/ConfigFactory.cs` (`Fallback` and the `dotnet run` nudge word list only); `Commands/GuardCommand.cs` (`RestoreExpiredModelCapsIfDue` and its call only); `Commands/HelpCommand.cs`; `Services/CompletionProvider.cs`; `Program.cs`; `Serialization/DydoJsonContext.cs` (cap types only); `dydo.json` (`models.fallback`, the two `dotnet run` nudge patterns and the `reference/dydo-commands.md` hash value only); `Templates/dydo-commands.template.md` + `dydo/reference/dydo-commands.md` (Model Commands section); `Templates/files-off-limits.template.md` + `dydo/files-off-limits.md` (the `dydo model cap` mention); `dydo/reference/configuration.md` (`fallback` row and the model-cap sentence only); `dydo/understand/guard-system.md` (model-cap text only); `README.md`, `npm/README.md` (model rows); tests `ModelCommandTests.cs`, `ModelCapServiceTests.cs` (delete), `CommandSmokeTests.cs`, `GuardIntegrationTests.cs`, `CompletionProviderTests.cs`, `CompletionsCommandTests.cs`, `HelpCommandTests.cs`, `CommandDocConsistencyTests.cs`, `ConfigFactoryTests.cs` (`Fallback` spots and `DefaultNudges_DotnetRunPatternExcludesRetiredWorkCommands` only) | S-2 | S, R6, D | `feature/simplify-skill-model` |
| S-5 | `paths`, `pathSets`, tool-scoped file nudges, `models.efforts` and `name` are gone; `ResolveModel` returns the model only; `IRoleDefinitionService` and `ResolvePathSets` are gone; config docs match | `Models/PathsConfig.cs` (delete); `Models/DydoConfig.cs`; `Models/ModelsConfig.cs` (`Efforts` only); `Models/NudgeConfig.cs`; `Services/ConfigFactory.cs` (the two `Tools` copy lines only); `Services/ValidationService.cs` (the `tools`/`audience` validation branch only); `Commands/GuardCommand.cs` (`CheckFileNudges`, `ApplyFileNudge`, `MatchesFileNudgePattern`, `NudgeAppliesToAudience` if orphaned, their call sites, the `Tools` skip in `CheckNudges` and the `Tools` copy in the block-nudge merge only); `Services/IRoleDefinitionService.cs` (delete); `Services/RoleDefinitionService.cs` (`ResolvePathSets` only; class becomes static); `Commands/SyncCommand.cs` (`ResolveModel` and the `effort` line only); `Serialization/DydoJsonContext.cs` (`PathsConfig` only); `dydo.json` (`paths`, `name`, `models.efforts` only); `dydo/reference/configuration.md` (the `paths`, `name`, `efforts` rows, the schema block minus its `models` key line, the `paths.pathSets` bullet and the Nudges paragraph's `tools` phrase only); `dydo/understand/guard-system.md` (tool-scoped nudge text only); tests `GuardCommandTests.cs` (file-nudge tests only), `RoleDefinitionServiceTests.cs` (`ResolvePathSets` region only), `ConfigurablePathsTests.cs` (delete), `ConfigServiceTests.cs`, `ConfigFactoryTests.cs` (`Efforts`/`Name` spots only), `SyncCommandTests.cs` (`effort` spots only), `ValidateCommandTests.cs` (config-literal lines only), `ValidationServiceTests.cs` (config literals and the tool-scoped-nudge tests only), `GuardWorkerLaneTests.cs` (the four file-nudge theories only) | S-4 | S, R7, D | `feature/simplify-skill-model` |
| S-6 | The compiler's model is named for what it is: `SkillTemplate`, `SkillTemplateService.DiscoverSkills`/`Parse`, `skillName`/`agentName`, `SyncAgent`/`SyncSkill`, `RetiredSkills`; test classes and names follow, and skill-only templates are asserted as skills, `EmitAgent` ones as agents | `Models/RoleDefinition.cs` → `Models/SkillTemplate.cs`; `Services/RoleDefinitionService.cs` → `Services/SkillTemplateService.cs`; `Commands/SyncCommand.cs`; `Services/TemplateGenerator.cs` (`roleName` parameters and doc comments only); `Commands/GuardCommand.cs` (remaining references only); tests `RoleDefinitionServiceTests.cs` → `SkillTemplateServiceTests.cs`, `SyncCommandTests.cs`, `ChiefOfStaffSyncTests.cs`, `WayfinderHarmonyTests.cs`, `CodexSyncArtifactsE2ETests.cs`, `TemplateGeneratorTests.cs`; `dydo/understand/architecture.md` (class names only) | S-5 | S, R3 | `feature/simplify-skill-model` |
| S-7 | Every shipped skill template's first frontmatter key is `name: <slug>`; `Parse` requires it and fails with the file name on a mismatch; the skill-mechanics `mode` row becomes `name` and its now-false "Read by nothing:" fragment is deleted (the rest of the cell stays true); the `customizing-roles.md` example and row say `name`; the enforcement sentence and the classification sentence are listed for the human's pass | `Templates/skill-*.template.md` (the `mode:` line only, via the recorded `sed`); `Templates/writing-for-agents-resource-skill-mechanics.template.md` (the `mode` table row only); `Services/SkillTemplateService.cs`; `dydo/guides/customizing-roles.md` (example block and frontmatter table only); tests `SkillTemplateServiceTests.cs`, `SyncCommandTests.cs` (fixtures and the frontmatter-leak assertion only), `ChiefOfStaffSyncTests.cs`, `TemplateCommandTests.cs` and `UpstreamSkillSourceTests.cs` (key fixtures only), `TemplateGeneratorTests.cs` (the two `mode: code-writer` assertions only), `TemplateScaffoldingTests.cs` (the one `mode: code-writer` assertion only) | S-6 | S, R4 | `feature/simplify-skill-model` |
| S-8 | DYD-75 items 2–4: `web: true` compiles `WebFetch, WebSearch` on Claude and `web_search = true` on Codex; `scout` is a shipped read-only, web-enabled, non-delegating agent whose body is the former research resource; `argument-hint` compiles to Claude `argument-hint:` and Codex `interface.default_prompt`; `handoff` and `teach` carry it with the upstream texts; the four other hints, the two skill-mechanics rows and the softened worker clause are listed for the human's pass | `Commands/SyncCommand.cs` (`BuildAgent`, `BuildCodexAgent`, `BuildSkill`, `WriteCodexInvocationPolicy` only); `Models/SkillTemplate.cs`; `Services/SkillTemplateService.cs`; `Services/ConfigFactory.cs` (`scout` binding only); `dydo.json` (`models.agents.scout` only); `Templates/skill-scout.template.md` (new); `Templates/research-resource-scout.template.md` (delete); `Templates/skill-research.template.md` (frontmatter `web: true` and the one `resources/scout.md` link only); `Templates/skill-handoff.template.md`, `Templates/skill-teach.template.md` (the `argument-hint` line only); tests `SyncCommandTests.cs`, `SkillTemplateServiceTests.cs`, `CodexSyncArtifactsE2ETests.cs`, `UpstreamSkillSourceTests.cs` | S-7 | S, C | `feature/simplify-skill-model` |
| S-9 | Closeout: the migration guide carries §3's migration as one list; the audit over the integrated branch is confirmed; the assimilation brief records what changed, what was learned, what remains and every doc or prompt gap the human's pass must fill (the DR 028 and DR 002 sentences that now read as history included); DYD-75 and this plan are updated | `dydo/guides/migrating-dydo-2x-to-3x.md`; `dydo/project/migrations/3.0-skill-model-simplification-assimilation.md` (new); this plan's `## Amendment` entries; Linear (DYD-75 comment and relations; Project status) | S-8 | S, D, A | `feature/simplify-skill-model` |

### Later bearings

- DYD-75 item 1, after DYD-64: regenerate `.claude/**`, `.codex/**`, `.agents/**` and the installed
  framework docs and hashes from the finished sources, then `InstalledTemplateParityTests`'s successor
  (a `dydo template update --diff` that reports nothing) goes green.
- The human migrates LC per the guide; then both projects are on 3.0 and the retired-artifact sweep
  lists (`RetiredSkills`, `RetiredWorkflows`, `RetiredSkillResources`, `RetiredBinaryFiles`,
  `RetiredDocFiles`) can be emptied.
- With six framework documents left under `dydo template update`, the command may fold into `dydo init`
  as a refresh; not decided here.

### Exact gates

Run from the Issue worktree root. Build first so `dotnet bin/Release/net10.0/dydo.dll` is current.

**Gate S — the suite (every Issue)**

```powershell
dotnet build DynaDocs.sln -c Release
dotnet test DynaDocs.Tests --nologo -v q
git diff --check
git status --porcelain -- .claude .codex .agents
```

The build succeeds. The failed set equals the stage baseline: for S-1 and S-3 the fourteen below;
from S-2 on, the twelve that remain once `FreshInit_TemplatesAreExcludedFromCheck` and
`MattDerivedTemplates_ShippedSourceEqualsInstalledCopy` are deleted — or fewer, with the Issue recording
which baseline test went green and why. `git diff --check` and the status line print nothing.

Baseline at `9875c9a6` (14 red, all pre-existing, none owned here): `InitCheckIntegrationTests`
{`FreshInit_TemplatesAreExcludedFromCheck`, `FreshInit_OffLimitsFileDoesNotCreateFalsePatterns`,
`Check_ExcludesAgentWorkspaceFiles`, `FreshInit_WelcomeMdLinksToGlossary`, `FreshInit_PassesCheck_WithOneWarning`};
`FixCommandIntegrationTests` {`Fix_BracketedTitle_RemainsReachableAfterHubRegeneration`,
`Fix_AfterInit_ProducesNoChanges`, `Check_IgnoresObsidianFolder`, `Fix_GeneratedHubsPassFrontmatterCheck`};
`DocumentationTests.Check_FreshLinearNativeScaffold_PassesWithoutRepositoryWorkHierarchy`;
`ChangelogStructureTests` {`Check_AcceptsAlternativeChangelogStructure`, `Check_AcceptsFlatChangelogStructure`,
`Check_AcceptsMixedChangelogStructure`}; `InstalledTemplateParityTests.MattDerivedTemplates_ShippedSourceEqualsInstalledCopy`.

**Gate R — residue (R*n* is acceptance criterion *n*'s `rg` line)**

The Issue's `rg` line from §1 prints nothing — with one recorded exception: R1 at S-1's gate may show
exactly one hit, the `roles` assertion in `ConfigFactoryTests.DefaultNudges_DotnetRunPatternExcludesRetiredWorkCommands`,
which S-4 retires together with the `dotnet run` word list; at the final merge R1 prints nothing.
`CODE` expands to
`Commands Services Models Utils Rules Serialization Program.cs DynaDocs.Tests -g '!DynaDocs.Tests/Fixtures/**'`.

**Gate D — docs (S-1, S-2, S-4, S-5, S-9)**

```powershell
dotnet bin/Release/net10.0/dydo.dll check dydo
dotnet bin/Release/net10.0/dydo.dll template update --diff
```

Both are baselined: run them on the Issue's base commit first and record that output on the Issue; the
candidate reports no finding, pending item or warning the base did not. (At `9875c9a6` the `--diff`
line reports the mirror as pending (32 entries), a pending `guides/working-tree-contract.md`, and a
user-edited `reference/dydo-glossary.md`: the mirror leaves with S-2; the two framework docs belong
to the human's DYD-64 pass and DYD-75's reflection, not to this Project.)

**Gate C — compile proof (S-8)**

```powershell
$scratch = Join-Path $env:TEMP ("dydo-s8-" + [guid]::NewGuid().ToString("N").Substring(0,8))
New-Item -ItemType Directory $scratch | Out-Null
Push-Location $scratch
dotnet <worktree>/bin/Release/net10.0/dydo.dll init claude
dotnet <worktree>/bin/Release/net10.0/dydo.dll init codex --join
dotnet <worktree>/bin/Release/net10.0/dydo.dll sync
Select-String -Path .claude/agents/research.md,.claude/agents/scout.md -Pattern '^tools:'
Select-String -Path .codex/agents/research.toml,.codex/agents/scout.toml -Pattern 'web_search'
Select-String -Path .claude/skills/handoff/SKILL.md -Pattern 'argument-hint'
Get-Content .agents/skills/handoff/agents/openai.yaml
Pop-Location
```

The output shows acceptance criterion 8 verbatim; paste it on the Issue.

**Gate A — audit (S-9)**

Inquisitor sub-agents over the integrated feature branch, one lens each: dead references to cut
features (code, config, docs, READMEs); truth of every doc touched; the new seams' test coverage
(`Parse`, web tools, `argument-hint`, `scout`); the migration guide against the LC tree. Every finding
is adversarially verified before it counts. A confirmed finding becomes an Issue or an amendment;
the brief records the rest.

## 5. Ordering and isolation

Kickoff, by the planning session before any Issue is pickable: create S-1 … S-9 in the Linear Project
from the §4 rows (title `S-n — <outcome>`, body = the row plus its gates and base branch, labels
`Improvement` + `AFK`, native blockers per the Blockers column); confirm `feature/simplify-skill-model`
exists at `9875c9a6`; post the governing commit on the Project; set DYD-75 blocked by S-8 and comment
its narrowed scope.

Merge order into the feature branch, each as a `--no-ff` merge followed by a fresh merge review:
**S-1 and S-3** in parallel (disjoint files), then **S-2**, **S-4**, **S-5**, **S-6**, **S-7**, **S-8**,
**S-9**. Each later Issue branches from the feature branch after its blocker has merged, so every
worker starts from the reduced surface.

Per Issue: one `code-writer` sub-agent in the Issue worktree with the contract; where the Issue owns
`dydo/` documents, a `docs-writer` sub-agent follows in the same worktree with the doc list; one fresh
`reviewer` (`code` rubric) on the branch; findings loop to the writer; on PASS the planning session
merges and reviews the merge. The planning session — this human-started session, wearing the current
`orchestrator`-era skills — never edits sources itself beyond this plan and Linear, dispatches
sub-agents directly because current workers cannot delegate, and stages by explicit path.

Hot files and their order: `Services/ConfigFactory.cs` (S-1, S-4, S-8), `Models/ModelsConfig.cs`
(S-1, S-4, S-5), `Commands/SyncCommand.cs` (S-1, S-2, S-5, S-6, S-8), `Services/TemplateGenerator.cs`
(S-3 its region, S-2 the rest, S-6 names), `Commands/TemplateCommand.cs` (S-1 its spots, S-2 the
rest), `Commands/GuardCommand.cs` (S-4, S-5, S-6), `dydo.json` (S-1, S-2, S-4, S-5, S-8 — one key
group each), `SyncCommandTests.cs` (S-1, S-2, S-5, S-6, S-7, S-8), `RoleDefinitionServiceTests.cs`
(S-2, S-5, S-6 rename, S-7, S-8), `dydo/guides/customizing-roles.md` (S-1, S-2, S-7),
`dydo/reference/configuration.md` (S-1, S-2, S-4, S-5). Never two of these in flight at once.

## 6. Watch-outs

- Do not write a shim: no reading of `models.roles`, `paths`, `name`, `efforts`, `fallback` or a
  template hash "for one release". The migration guide is the compatibility layer.
- Do not rewrite a prompt or doc file. A frontmatter key, a straight reference rename (`mode` →
  `name`, `models.roles` → `models.agents`), the deletion of a line, row or section that describes a
  cut feature, the verbatim scout move and the one dead-link replacement are the whole licence, in
  templates and under `dydo/` alike; replacement prose, a new row, a softened clause or an invented
  hint is a finding.
- Do not regenerate `.claude/**`, `.codex/**` or `.agents/**` and commit it: Gate S's last line must
  stay empty. If a gate ran `sync` in the worktree, `git checkout -- .claude .codex .agents`.
- Do not chase a baseline-red test. If a cut changes one, record it on the Issue and move on.
- Do not delete the retired-artifact sweeps or `ResolveIncludes`; they are out of scope by design.
- Do not rename `customizing-roles.md` or the glossary's Role entry: "role" stays a prose word.
- Do not filter the suite before a commit; the whole suite, then the baseline comparison.
- Do not write `@` before an agent's name in Linear text; a mention starts a session.
- Do not stage with `git add -A` or `git add .`; the IDE may have staged files that are not yours.
- Do not leave LF-only sources behind a scripted edit; `unix2dos` the touched file.

## Not yet specified

- Whether `scout` should bind to `light` rather than `standard`: a tier choice the human makes after
  watching a few research runs.
- Whether the `research` body still wants a scout brief once `scout` is an agent that preloads its own
  skill: S-8 replaces the link with the agent's name and the human's pass decides the sentence.
