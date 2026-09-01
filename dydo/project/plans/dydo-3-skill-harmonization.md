---
title: Harmonize the skill system
status: draft
area: project
type: context
linear-project: https://linear.app/bodnar-balazs/project/dydo-30-harmonize-the-skill-system-d84a9ab72416
---

# Harmonize the skill system

Build the skill system [DR 045](../decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md)
describes: one flow map every agent can place itself on, twenty-five skills that trigger correctly and
cross-reference only their genuine neighbours — each reference bound by the §7 table and living in
exactly one place, never a web of everything naming everything — five rubrics, two planner resources, a working-tree contract, a guard
that protects orientation files without hiding them, and a compiler whose agents actually reach their
skill. This plan runs **under today's tooling** — the current orchestrator-era skills, hands-on
sub-agent dispatch, and a disabled guard — because the system it builds does not exist yet.

## 1. Specification

### Intent

Make every skill discoverable, hard to misinvoke, small enough to read in one sitting, anchored by a
leading word that does work, and correct about where it sits on the map. Remove the four mechanical
defects. Import the Matt Pocock skills DR 045 names, adapted. Leave the repository byte-derived: every
`.claude/`, `.codex/` and `.agents/` file is the product of `dydo sync` over canonical sources.

### In scope

- Guard: a **protected tier** (readable, never writable by any tool), the review-block nudge, dead
  Codex hook matcher names removed.
- Compiler: Must-Reads retained and link-rewritten for every role; `skills:` preload and the `Skill`
  tool on Claude agents; `delegates: true` → the `Agent` tool; Codex agents told to load their skill
  by name; `planner` emitted as an agent as well as a skill; `merge-sprint` → `merge`; `run-issues` retired;
  inventory tests derived from the shipped template set instead of a hard-coded count.
- Sources: the sixteen files Codex restored, rewritten to the DR 045 contract; `issue-captain`,
  `admiral`, `walkthrough` new; `wayfinder` reshaped as a method; nine imports from
  mattpocock/skills at `6654f6b6` (diagnosing-bugs, research, codebase-design, domain-modeling,
  prototype, handoff, teach, improve-codebase-architecture, `SKILL-MECHANICS` as a resource);
  planner resources `project` and `issue`; the five rubrics; the working-tree contract guide; the
  entry point and `dydo/index.md`; the inquisition workflow's `confirmed` gate and prompt wording.
- Vocabulary and docs: glossary, work model, Issue lifecycle, architecture, orchestration pitfalls,
  writing good briefs, customizing roles, templates-and-customization, dydo commands, guard system,
  third-party notices; two FutureFeatures (routine admiral; cross-vendor review).
- Runtime: `dydo.json` model bindings for the `planner`, `issue-captain`, and `research` agents; Linear labels
  `question`, `HITL`, `AFK` present in the workspace; an implementation-Issue template carrying the
  required fields (outcome, owned paths, blockers, exact gates, base branch).
- Regeneration, idempotence, template-hash reconciliation, the human's file-by-file pass, and a
  human-confirmed inquisition.

### Out of scope

- No PM ontology change, no Linear tooling inside dydo, no release, tag or publication.
- No cross-vendor review automation, no routine/cron admiral (FutureFeature candidates).
- No protection of files outside the DR 045 list; no `tools` field for Codex agents (none exists).
- No prose-freezing tests: tests prove structure, metadata and boundaries.

### Acceptance criteria

1. `dydo guard` (3.0.0) allows `Read`/`cat` of every protected file and blocks `Edit`, `Write`,
   `NotebookEdit`, and Bash-detected writes or deletes to them; `dydo/index.md` and
   `dydo/files-off-limits.md` are no longer in the off-limits block list.
2. Every compiled `.claude/skills/*/SKILL.md` and `.agents/skills/*/SKILL.md` carries its
   `## Must-Reads` with links that resolve from that folder; links to a skill's `resources/` are
   rewritten to the host's emitted path (`.claude/skills/<role>/resources/<n>.md`,
   `.agents/skills/<role>/resources/<n>.md`); `{{include:extra-must-reads}}` resolves for skill-only
   roles (proved by fixtures).
3. Every `.claude/agents/*.md` carries `skills: [<name>]` and `Skill` in `tools`; only roles with
   `delegates: true` carry `Agent`; every `.codex/agents/*.toml` `developer_instructions` names the
   skill to load and, for writers, sets `sandbox_mode = "workspace-write"`.
4. The skill inventory is exactly the DR 045 taxonomy on both hosts; explicit-only skills carry
   `disable-model-invocation: true` / `policy.allow_implicit_invocation: false`; every model-invoked
   description begins with its trigger.
5. `rg -n "Tier-1|Managers Doctrine|Sprint|Slice|Waypoint|orchestrator|run-issues|run-sprint|merge-sprint|references/|decision ticket|separately generated"` over `Templates/skill-*`, `Templates/*-resource-*`, `Templates/workflow-*`, `Templates/entry-point*`, `Templates/index.template.md` returns no hits (an attribution comment may cite the upstream skill name). The glossary is governed by Gate D, whose "Retired PM terms" paragraph legitimately names the retired words.
6. Every skill names its stage and its neighbours exactly as §7's cross-reference table binds; no
   skill references a skill, rubric, resource or guide that does not exist after regeneration.
7. `dydo sync` removes retired outputs: `.claude/workflows/run-sprint.js` (internal name
   `run-issues`), `.claude/skills/orchestrator/` and the stale `reviewer/resources/merge-sprint.md`
   (each with whatever Codex twin exists under `.agents/skills/`) are gone after regeneration; `.claude/workflows/inquisition.js` refuses to run without `confirmed: true` and
   cites `.claude/skills/reviewer/resources/merge.md`.
8. Two consecutive `dydo sync` runs produce a byte-identical closed path set; `dydo template update
   --diff` reports zero pending updates; `dydo validate` and `dydo check` report zero errors **and
   zero warnings**; Release build has zero warnings; the isolated full suite and forced coverage
   pass; `git diff --check` is clean; `CLAUDE.md` and `AGENTS.md` equal the entry-point template
   output; a fresh `dydo init claude` in a scratch directory installs
   `dydo/guides/working-tree-contract.md` and ships `dydo.json` with `planner: strong`,
   `issue-captain: strong`, and `research: standard`.
9. `THIRD-PARTY-NOTICES.md` and `npm/THIRD-PARTY-NOTICES.md` list every adapted skill and are
   byte-identical; both packages include the notice.
10. The human has walked every source file with an agent (H-11) and every edit from that pass has
    been independently reviewed; the inquisition (H-12) returned PASS with an assimilation brief.

### Questions and answers

- **Where do Must-Reads live for skill-only roles?** In the compiled skill body; the agent file keeps
  its context block too. Duplication in build output is fine; the source is single.
- **Preload on Codex?** Not inlined. `developer_instructions` says "Load the `$<name>` skill before
  working." H-2 includes one empirical Codex spawn recording whether the child saw `AGENTS.md` and
  the skill; the finding goes to the assimilation brief and, if negative, to a follow-up Issue.
- **How does the review discipline hold before the Issue Captain skill exists?** The admiral session
  spawns a fresh `reviewer` sub-agent per Issue and per file and refuses to merge without its review
  block; H-10 re-reviews the integrated result.
- **Who writes `CLAUDE.md`/`AGENTS.md`?** They are not guard-protected — the harness defends its own
  orientation and config files, and off-limits keeps its original meaning of files agents must not
  even read. Only §8's ownership rule governs them: H-3 (they mirror its template; parity is its
  contract) and H-10 (final regeneration); afterwards the human. The entry-point parity test
  enforces equality either way.
- **Are project-local template copies part of the change?** Yes: `dydo/_system/templates/` mirrors
  every shipped `skill-*` and `*-resource-*` source (workflows are not mirrored); only H-10 touches
  that folder, via `template update`, which refreshes hash-clean copies and deletes hash-tracked
  stale ones (the renamed `merge-sprint` copy included).
- **Why does the Issue Captain compile as an agent when DR 045 calls it a hat?** Both, by decision:
  it is the hat a top-level session wears when it picks a ticket *and* a spawnable agent so an admiral
  can keep N Issues in flight as sub-agents (DR 045 §2, amended 2026-08-30). A spawned Issue Captain
  returns `blocked` with its question instead of waiting on the human.
- **Why does the planner compile as an agent when it is also a hat?** The same method serves both:
  a session may wear the hat directly, while an invoker may spawn a fresh planner with exactly one
  target, `project` or `issue`. The role is bound to the strong tier.
- **What about `tdd`?** Folded, not imported: seams and anti-patterns into test-writer, red-before-
  green into code-writer and Issue Captain.

## 2. Prior art

- The current sources at `ec5f7158` (Codex's restoration) are the structural baseline: shape,
  attribution, invocation metadata. Their review (2026-08-30) found identity-not-trigger descriptions,
  no routing, taglines, skill↔workflow mismatch, and the four defects DR 045 lists.
- Pre-restoration sources at `596e3839` carry the leading words worth restoring (Gandalf / YOU SHALL
  NOT PASS, the conductor, "no reviewed intent, no code", the inquisitor's calibration section, the
  test-writer's "a good test is a contract"). Evidence for voice, not text to paste back.
- [mattpocock/skills at `6654f6b6`](https://github.com/mattpocock/skills/tree/6654f6b60cd9d5be8b54c6fafe44346dabeb3b76)
  for every import; `ask-matt` for the router shape the entry point borrows; `retro` for the lens list
  self-improvement absorbs; `to-tickets` for tracer bullets and expand–contract; `code-review` for the
  Fowler smell baseline the code rubric absorbs.
- Claude Code docs (`code.claude.com/docs/en/sub-agents.md`, `skills.md`) and Codex docs
  (`learn.chatgpt.com/docs/agent-configuration/subagents`, `build-skills`, `hooks`) as verified on
  2026-08-30 — the compiler lane's contract.

## 3. Design

- **Guard.** `dydo/files-off-limits.md` gains a `## Protected Patterns` block; `OffLimitsService`
  loads both lists; `GuardCommand` checks protected patterns only on write/delete paths — direct
  tools and the Bash analyzer's detected writes — with a message that says the file is readable and
  human-owned. Protected members are dydo's own system files only: `dydo/index.md`,
  `dydo/files-off-limits.md`, `dydo.json` (the hardcoded system pattern moves it from off-limits to
  protected). `CLAUDE.md`, `AGENTS.md` and harness config files stay outside the guard — the
  harness owns its own defensive measures — and off-limits keeps its original meaning: files agents
  must not even read. Shipped
  defaults live in `Services/ConfigFactory.cs`: `DefaultNudges` reaches `dydo.json` through
  `EnsureDefaultNudges` — the review-block nudge is added there at warn severity (matching
  `gh pr create` whose command lacks `Independent review`), the DR 026 "Tier-1 agents are managers …
  run-sprint workflow" nudge is retired from the factory, every remaining `run-sprint` mention in
  that file (including comments) goes with it, and this repo's `dydo.json` mirrors both changes;
  `CreateDefaultModels` binds `planner: strong`, `issue-captain: strong`, and `research: standard`,
  so a fresh `dydo init` ships the DR 045 bindings, and this repo's `dydo.json` is updated to match.
  The Codex hook matcher (`InitCommand.CodexGuardMatcher`) becomes exactly
  `Bash|apply_patch|Edit|Write|Agent|shell_command|exec|local_shell|unified_exec` — the documented
  Codex matcher names first (shell and unified exec match as `Bash`; `apply_patch` also as
  `Edit`/`Write`; `spawn_agent` as `Agent`), with the legacy shell names **retained** because they
  were added empirically (issue 0295: the hook fired but never matched Codex's shell lane) and the
  documentation reading is unproven against the installed Codex; the Claude-only UI names are
  dropped. H-2's recorded Codex spawn therefore includes one shell probe that must come back
  `BLOCKED` through the hook; that evidence goes to the assimilation brief, and trimming the legacy
  names is a follow-up Issue, not this Project. Every `orchestrator` mention in `GuardCommand.cs` — the stderr
  message and the comment above it — is reworded against the admiral role.
- **Compiler.** Delete `DropOrchestrationSections`; rewrite `../../../<x>` and `dydo/<x>` links in
  the compiled body to `../../../dydo/<x>` (valid from both `.claude/skills/<n>/` and
  `.agents/skills/<n>/`) and `resources/<n>.md` links to the host's emitted path;
  `RoleDefinition.Delegates` from frontmatter `delegates: true` grants `Agent` (workers never get
  it); Claude agent frontmatter gains `skills:` and `Skill`; Codex `developer_instructions` gains
  the load line and writers gain `sandbox_mode`. `dydo sync` removes retired outputs: extend
  `RetiredManagedRoles` with `orchestrator`, add a retired-workflows list (`run-sprint`) and a
  retired-resources list (`reviewer/resources/merge-sprint.md`), each cleaned on every host.
  Inventory tests enumerate `Templates/skill-*.template.md`. The working-tree contract ships as a
  framework document: `Services/FolderScaffolder.cs`, `Commands/TemplateCommand.cs` and
  `Services/TemplateGenerator.cs` map `working-tree-contract.template.md` →
  `guides/working-tree-contract.md` so `dydo init` scaffolds it and `template update` tracks it.
- **Sources.** Shape for hats and workers: H1 → one-line job → `## Must-Reads` → `## Boundary` →
  `## Method` (each step ends on a completion criterion) → `## Return` or `## Handoff`. Methods and
  reference skills keep upstream shape. The per-file brief is §7.
- **Working-tree contract.** One guide; host-managed worktrees when the host isolates, otherwise
  `git worktree add ../<repo>.worktrees/<branch>`; base SHA, branch and worktree path posted on the
  Issue before the first edit; Issue Captain environment check before any spawn; cleanup after merge;
  orphan sweep by chief-of-staff.
- **Hazards.** Renaming `merge-sprint`: H-2 performs the pure file rename (`git mv`, content
  untouched) and updates code and test references; H-7 writes the content and updates the reviewer
  template's link line and the inquisition citation. Between the two merges the feature branch
  carries one dangling link — tolerated, and Gate E proves it closed. Exact-wording tests
  (`ChiefOfStaffSyncTests`, `WayfinderHarmonyTests`, `SyncCommandTests.MattDerivedSkills…`,
  `UpstreamSkillSourceTests`'s wayfinder-explicit and grill-me-phrase assertions,
  `EntryPointParityTests.SharedTemplate_ContainsOnlyTheMinimalEntryContract`'s required/forbidden
  word lists, and `SyncCommandTests`' `planner` model `InlineData`) will fail on the new prose and
  config; H-2 replaces them with structural assertions (the entry point: ≤ 25 non-blank lines, a
  link to `dydo/index.md`, CLAUDE.md/AGENTS.md parity kept — the working-tree link is H-3's and is
  proved by its per-file review and Gate E, never asserted before H-3 merges). The
  shipped-equals-installed parity assertion in `UpstreamSkillSourceTests` is guaranteed to fail on any
  branch where a source changed but H-10 has not mirrored it; H-2 moves it into its own class,
  `InstalledTemplateParityTests`, whose comparison set is **exactly the five Matt-derived skills it
  compares today** (wayfinder, grilling, grill-me, bro, writing-for-agents) — general parity is
  proved by Gate E's `template update --diff` reporting zero pending, not by this test. It passes at
  Gates A and B because neither H-1 nor H-2 edits a compared skill, is not run at Gates C/D, and is
  expected **red on the feature branch from H-3's merge** (H-3 rewrites writing-for-agents) **until
  H-10 mirrors** — the second named tolerated window beside the `merge-sprint` dangling link. "Merges
  with the suite green" therefore means green at the Issue's own gate; the full suite is demanded
  only at Gates A, B and E. **No Gate C Issue merges before
  H-2 has merged** (they may be worked in parallel).
- **Gates A, B and E run the whole suite.** `DynaDocs.Tests/coverage/gap_check.py --force-run`
  executes the full test suite with no filter and exits non-zero on any failure; a gate's `--filter`
  line is only the fast first pass. Gates C and D run no or a narrow xunit set by design, and their
  Issues merge with the suite green only because every test their sources trip is either de-frozen by
  H-2 (merged first) or kept green by the Issue itself — H-3 keeps `CLAUDE.md`/`AGENTS.md` in parity
  with its rewritten template for exactly that reason. Test ownership in §4 is spot-exact: H-1's config, matcher and nudge changes break
  `SyncCommandTests`' Codex-matcher assertions and `planner` model `InlineData`, and
  `ConfigFactoryTests`' managers-doctrine nudge tests — those spots are H-1's, and `ConfigFactory.
  IsLegacyDefaultNudge` becomes dead code H-1 removes. `GuardIntegrationTests` asserts that an edit of
  `dydo/index.md` exits 2 with `BLOCKED` on stderr; the protected tier keeps that contract for writes
  and H-1 adds the read-allowed case beside it. `InitCommandTests.cs` is edited by both H-1 (Codex
  matcher assertions) and H-2 (inventory count) — H-2 is blocked by H-1. Rewriting
  `.codex/hooks.json` (H-1) invalidates the SHA-pinned hook trust in `~/.codex/config.toml`, so the
  human's Codex sessions run unguarded until re-trusted: H-1's return says so explicitly. Rollback
  is `git revert` of the feature merge; no data or config migration.

## 4. Implementation Issue map

| Issue | Outcome | Exclusive surface | Blockers | Gate |
|---|---|---|---|---|
| H-1 | Guard protected tier, review-block default nudge, DR 026 nudge retired, Codex hook matcher cleanup | `Services/OffLimitsService.cs`, `Commands/GuardCommand.cs`, `Commands/InitCommand.cs` (hooks), `Services/ConfigFactory.cs` (retire the DR 026 nudge and every `run-sprint` mention, add the review-block default, fix `CreateDefaultModels`), `Templates/files-off-limits.template.md`, `dydo/files-off-limits.md`, `dydo.json` (nudges; `models.roles`: bind `planner: strong`, `issue-captain: strong`, `research: standard`), `.codex/hooks.json`, `dydo/understand/guard-system.md`, tests `OffLimitsServiceTests`, `GuardCommandTests`, `GuardIntegrationTests`, `ConfigFactoryTests`, `InitCommandTests` (Codex matcher assertions only), `SyncCommandTests` (only the Codex-matcher assertions and the `planner` model `InlineData`) | — | A |
| H-2 | Compiler contract, test de-freezing, retired-output cleanup, `merge` file rename, working-tree-contract scaffolding | `Commands/SyncCommand.cs`, `Models/RoleDefinition.cs`, `Services/RoleDefinitionService.cs`, `Services/TemplateGenerator.cs` (incl. its fallback role table, which still lists `orchestrator`), `Services/FolderScaffolder.cs`, `Commands/TemplateCommand.cs`, new stub `Templates/working-tree-contract.template.md` **and its installed twin** `dydo/guides/working-tree-contract.md` (each frontmatter, H1 and a one-sentence summary — so no later worktree materializes an orphan and `dydo check` stays warning-free; H-2 regenerates `dydo/guides/_index.md` so the hub line exists; H-6 writes the real content of both), `git mv Templates/reviewer-resource-merge-sprint.template.md Templates/reviewer-resource-merge.template.md` (content untouched), delete `Templates/workflow-run-sprint.js`, tests `SyncCommandTests` (everything except H-1's two spots), `RoleDefinitionServiceTests`, `TemplateGeneratorTests`, `CodexSyncArtifactsE2ETests`, `ChiefOfStaffSyncTests`, `WayfinderHarmonyTests`, `TemplateOverrideTests`, `InitCommandTests` (inventory count only), `UpstreamSkillSourceTests`, `EntryPointParityTests`, new `InstalledTemplateParityTests` | H-1 | B |
| H-3 | The standard-setters: writing-for-agents + `SKILL-MECHANICS` resource, entry point, `dydo/index.md` taxonomy | `Templates/skill-writing-for-agents.template.md`, new `Templates/writing-for-agents-resource-skill-mechanics.template.md`, `Templates/entry-point.template.md` with its mirrors `CLAUDE.md` and `AGENTS.md` (parity is this Issue's contract), `Templates/index.template.md`, `dydo/index.md` | — | C |
| H-4 | Thinking cluster | `skill-co-thinker`, `skill-grilling`, `skill-grill-me`, `skill-bro`, new `skill-domain-modeling`, `skill-research`, `skill-prototype` | H-3 | C |
| H-5 | Planning cluster | `skill-planner`, new `planner-resource-project`, `planner-resource-issue`, `skill-wayfinder` (method), new `skill-codebase-design` | H-3 | C |
| H-6 | Delivery cluster + working-tree contract | new `skill-issue-captain`, `skill-admiral` (from orchestrator, which is deleted), `skill-code-writer`, `skill-test-writer`, `skill-docs-writer`, new `skill-diagnosing-bugs`, `skill-handoff`, the content of `Templates/working-tree-contract.template.md` (H-2 ships the stub and the scaffolding) + its installed copy `dydo/guides/working-tree-contract.md` (written by hand here; H-10 reconciles the hash) | H-3 | C |
| H-7 | Review cluster + inquisition workflow | `skill-reviewer` (incl. its rubric link line → `resources/merge.md`), `skill-inquisitor`, `reviewer-resource-{code,tests,docs,plan,merge}` (content), `Templates/workflow-inquisition.js` (`confirmed` gate, prompt wording, citation of the compiled `merge` rubric path) | H-2, H-3 | C |
| H-8 | Human cluster | `skill-chief-of-staff`, `skill-self-improvement`, new `skill-walkthrough`, `skill-teach`, `skill-improve-codebase-architecture` | H-3 | C |
| H-9 | Vocabulary, docs, notices | `Templates/dydo-glossary.template.md` + `dydo/reference/dydo-glossary.md`, `dydo/understand/{work-model,task-lifecycle,architecture,templates-and-customization}.md`, `dydo/guides/{orchestration-pitfalls,customizing-roles,writing-good-briefs}.md`, `Templates/dydo-commands.template.md` + `dydo/reference/dydo-commands.md`, `THIRD-PARTY-NOTICES.md`, `npm/THIRD-PARTY-NOTICES.md`, new `dydo/project/future-features/{routine-admiral,cross-vendor-review}.md` | H-2, H-3 | D |
| H-10 | Regenerate, reconcile, integrate | `dydo/_system/templates/**`, `dydo.json` (hashes only), `.claude/**`, `.codex/agents/**`, `.agents/skills/**`, `CLAUDE.md`, `AGENTS.md`, generated hubs | H-1 … H-9 | E |
| H-11 | HITL: the human's file-by-file pass with an agent; edits re-reviewed | every `Templates/skill-*`, `*-resource-*`, `entry-point`, guide; then H-10's surface again | H-10 | E |
| H-12 | Inquisition (confirmed) + assimilation brief | `dydo/project/inquisitions/<date>-skill-harmonization.md`, `dydo/project/migrations/3.0-skill-harmonization-assimilation.md` | H-11 | F |

Template paths above are `Templates/<name>.template.md`; every `skill-*`/`*-resource-*` change is
mirrored into `dydo/_system/templates/` by H-10, never by the source Issue.

### Exact gates

Run from the repository root in the Issue worktree. `dydo` below means `dotnet bin/Release/net10.0/dydo.dll`
until the 3.0.0 CLI is reinstalled.

**Gate A — guard**

```powershell
dotnet build DynaDocs.sln -c Release
py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~OffLimitsServiceTests|FullyQualifiedName~GuardCommandTests|FullyQualifiedName~InitCommandTests"
py DynaDocs.Tests/coverage/gap_check.py --force-run
dydo check dydo
git diff --check
```

Fixtures prove: protected file readable via `Read` and `cat`; blocked via `Edit`, `Write`, `sed -i`,
`echo >`, `rm` (exit 2, `BLOCKED:` on stderr); off-limits still blocks reads; the review-block nudge
fires on `gh pr create` without the block and stays silent with it; the emitted Codex matcher
equals the §3 literal (Claude-only UI names dropped, legacy shell names retained); a fresh
`CreateDefaultModels` binds `planner: strong`, `issue-captain: strong`, and `research: standard`.
`gap_check.py` runs the whole suite: H-1's named test spots keep it green.

**Gate B — compiler**

```powershell
dotnet build DynaDocs.sln -c Release
py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~SyncCommandTests|FullyQualifiedName~RoleDefinitionServiceTests|FullyQualifiedName~TemplateGeneratorTests|FullyQualifiedName~CodexSyncArtifactsE2ETests|FullyQualifiedName~ChiefOfStaffSyncTests|FullyQualifiedName~WayfinderHarmonyTests|FullyQualifiedName~TemplateOverrideTests|FullyQualifiedName~InitCommandTests|FullyQualifiedName~UpstreamSkillSourceTests"
py DynaDocs.Tests/coverage/gap_check.py --force-run
rg -n --type cs "run-sprint|run-issues|merge-sprint|Must-Reads|orchestrator" Commands Services Models DynaDocs.Tests
git diff --check
```

The `rg` may hit only: `Must-Reads` where the compiler keeps it, and the literals `run-sprint`,
`merge-sprint` and `orchestrator` inside the retired-output lists in `Commands/SyncCommand.cs` and
their fixtures in `SyncCommandTests`. H-2 clears every other mention in its own files: the
`SyncCommand.cs` doc comments, the `CodexSyncArtifactsE2ETests` forbidden-phrase list, and every test
that names `orchestrator` as a live role (`TemplateGeneratorTests`, `TemplateOverrideTests`,
`RoleDefinitionServiceTests`, `SyncCommandTests`, `CodexSyncArtifactsE2ETests`, `WayfinderHarmonyTests`)
is rewritten against a surviving role or derived from the template set; H-1 has already cleared
`ConfigFactory.cs` and the "top-level orchestrator" wording in `GuardCommand.cs`. Any remaining hit
is a finding. (`--type cs` keeps the gitignored coverage HTML out.) Fixtures cover: skill-only role keeps
Must-Reads with a resolved include; Must-Reads and `resources/` link rewriting from both skill
folders; `delegates: true` → `Agent` and its absence on workers; `skills:` and `Skill` on every Claude
agent; Codex load line and `sandbox_mode`; inventory derived from the template set;
`explicit`/`automatic` metadata; a stale `run-sprint.js` and `skills/orchestrator/` removed by sync;
the stub `working-tree-contract.template.md` scaffolded by `dydo init` and tracked by
`template update`; the emitted Codex matcher equals the §3 literal. One recorded Codex spawn (`codex exec` of a compiled agent asking it to name its `AGENTS.md` first line and
its loaded skill) is attached to the Issue as evidence.

**Gate C — a source cluster**

```powershell
dotnet build DynaDocs.sln -c Release
dydo template update
dydo sync
rg -n "Tier-1|Managers Doctrine|Sprint|Slice|Waypoint|orchestrator|run-issues|run-sprint|merge-sprint|references/|decision ticket|separately generated" <the Issue's owned files under Templates/>
dydo check dydo
git diff --check
git checkout -- dydo/_system dydo.json .claude .codex .agents
git clean -fd -- dydo/_system .claude .codex .agents
```

The per-file reviews run **between** `dydo sync` and the two cleanup lines, on the source-compiled
output; the `git clean` removes the untracked files a new role or resource leaves behind, which a
plain checkout cannot (a fresh worktree holds nothing user-owned under those paths). A Gate C
worktree branched before H-2 merged rebases onto the feature branch after H-2 lands and re-runs this
gate, so the compiled check always uses the post-H-2 compiler.
`dydo sync` reads a role's project-local copy under `dydo/_system/templates/` before the shipped
source (`TemplateGenerator.ReadTemplate`), so without the `template update` step it would compile the
stale copy of every pre-existing role and only brand-new roles would show the rewrite. The
`template update` refreshes the hash-clean local copies from the rebuilt source **inside the Issue
worktree only**; the final `git checkout` discards those refreshed copies, the hash changes and the
generated output before the Issue returns — H-10 alone commits them. The `rg` is scoped to the Issue's
own files and returns no hits; the full-set `rg` is acceptance
criterion 5, proved at Gate E. `dydo check` exits 0 with zero errors; an orphan-hub warning for a file
this Issue creates is tolerated until H-10 regenerates the hubs, any other warning is a finding. No
xunit run at Gate C: structural tests are H-2's and run at Gate E once every cluster has merged. Every file in the cluster receives one fresh `reviewer` sub-agent with
the `docs` rubric **and** §6 (the writing checklist) as its brief, reading the template first and the
compiled output second (source-compiled, thanks to the step above); its review block is attached to
the Issue. Source Issues never commit generated output, local template copies or hash changes.

**Gate D — docs and vocabulary**

```powershell
dydo check dydo
rg -n "Waypoint|Wayfinding map|orchestrator|Sprint|Slice" dydo/reference/dydo-glossary.md dydo/understand dydo/guides Templates/dydo-glossary.template.md Templates/dydo-commands.template.md
py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~CommandDocConsistencyTests|FullyQualifiedName~DocumentationTests"
git diff --check
```

The `rg` may hit only the "Retired PM terms" paragraph and historical migration guides. `dydo check`
exits 0 with zero errors; orphan-hub warnings for the two new FutureFeatures are tolerated until H-10
regenerates the hubs. The notices are verified by H-10's Gate E run of `InstalledTemplateParityTests`
and `UpstreamSkillSourceTests`, not here.

**Gate E — integration**

```powershell
dotnet build DynaDocs.sln -c Release
$generatedRoots = '.agents/skills','.claude/skills','.claude/agents','.claude/workflows','.codex/agents'
dydo template update
dydo sync
$first = Get-ChildItem $generatedRoots -File -Recurse | Sort-Object FullName | ForEach-Object { [pscustomobject]@{ Path = (Resolve-Path -Relative $_.FullName); Hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash } }
dydo sync
$second = Get-ChildItem $generatedRoots -File -Recurse | Sort-Object FullName | ForEach-Object { [pscustomobject]@{ Path = (Resolve-Path -Relative $_.FullName); Hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash } }
if (($first | ConvertTo-Json -Compress) -ne ($second | ConvertTo-Json -Compress)) { throw 'Generated output changed on second sync' }
dydo template update --diff
dydo validate
dydo check dydo
py DynaDocs.Tests/coverage/run_tests.py
py DynaDocs.Tests/coverage/gap_check.py --force-run
git diff --check
```

`template update` runs **before** the first `dydo sync` so its stale-copy cleanup (the local
`skill-orchestrator` and `reviewer-resource-merge-sprint` copies) precedes role discovery; it may
change only `frameworkHashes` keys in `dydo.json` and the mirrored `dydo/_system/templates/` set;
`--diff` then reports zero pending.
`CLAUDE.md` and `AGENTS.md` are rewritten to equal the template output. A fresh `reviewer` with the
`merge` rubric reviews the integrated feature branch; the human's pass (H-11) starts only after its
PASS. After H-11, Gate E runs again and every edited file gets a fresh `docs`-rubric review.

**Gate F — inquisition**

Run `.claude/workflows/inquisition.js` with `confirmed: true` only after the human says go, over the
full feature diff against this plan. PASS with an assimilation brief closes the Project.

## 5. Ordering and isolation

Kickoff, one act by the admiral before any Issue is pickable: confirm the Dydo team labels
`question`, `HITL`, `AFK` and `Needs human` exist (they do as of 2026-08-30; `Needs human` is the
raise-hand label the escalation ladder uses); create the twelve Issues from §4 if they do not exist
yet — title `H-n — <outcome>`, description from the §4 row plus its gate letter and base branch,
label `AFK` (H-11 gets `HITL`), native blocking per the Blockers column — and confirm each carries
base branch, blockers and gate letter; create the feature branch; post the governing commit on the
Project; block DYD-11 (the 3.0.0 release) and DYD-47 (Linear-PM acceptance) on H-12.

Feature branch `feature/skill-harmonization` from `master` at the governing commit. One Issue branch
`DYD-<n>-<slug>` per row, in its own worktree; one writer per worktree; serial merges into the feature
branch in this order:

1. **H-1** first, then **H-2** (blocked by H-1: shared `InitCommandTests.cs`, and Gate B's `rg`
   needs H-1's `ConfigFactory.cs` cleanup). **H-3** is worked in parallel with both — it sets the
   standard every other prose Issue is reviewed against — but **merges only after H-2**, like every
   Gate C Issue.
2. **H-4, H-5, H-6, H-8** in parallel after H-3 lands. Each is executed by the admiral session
   fanning out **one writer sub-agent per file** with §6 + that file's §7 row as the brief, then one
   fresh reviewer per file; findings loop to the writer; the Issue merges only when every file has a
   PASS block.
3. **H-7** after H-2 (rename) and H-3.
4. **H-9** after H-2 and H-3 (it documents the new frontmatter keys and vocabulary).
5. **H-10**, then **H-11** (HITL), then **H-12**.

Merge order into the feature branch: H-1, H-2, H-3, then the clusters as they pass, H-7 and H-9 after
their blockers, H-10 last before the human's pass. Hot files: `dydo.json` (H-1 nudges and model
bindings; H-10 hashes — never both in flight); `DynaDocs.Tests/Integration/InitCommandTests.cs` and
`DynaDocs.Tests/Commands/SyncCommandTests.cs` (H-1 its named spots, then H-2 the rest);
`dydo/_system/templates/**` (H-10 only). Under
today's tooling the admiral session is a human-started Claude or Codex session wearing the current
`orchestrator` skill; it dispatches sub-agents directly because current workers cannot delegate. It
never edits sources itself and never merges without a review block.

## 6. The writing checklist (every writer and reviewer brief carries this)

1. **Description = trigger.** Model-invoked: front-load the leading word, then the "use when"
   branches, one trigger per branch, no identity the body already carries. Explicit-only: one punchy
   human-facing line. Workers spawned by name may state their job, but still in trigger form.
2. **One anchor.** One leading word or image per skill that does work (Gandalf, conductor, *tight*
   loop, *red*, fog, *relentless*). A tagline that fails the no-op test is deleted, not softened.
3. **Shape** (hats and workers): H1 → one-line job → `## Must-Reads` → `## Boundary` → `## Method`
   with a completion criterion on every step → `## Return`/`## Handoff` with the exact shape the
   receiver expects. Methods and reference skills keep upstream shape.
4. **Place on the map.** One sentence naming the stage, who hands to this skill and who it hands to,
   exactly as §7 binds. Nothing else is cross-referenced.
5. **Vocabulary.** DR 045 terms only; Linear nouns only at real handoffs; retired words never (Gate C).
6. **Positive phrasing.** Prohibitions only as hard guardrails, paired with the positive target.
7. **Cache vs environment.** Never restate `--help`, config or directory layout; point at the guide.
8. **Budget.** Hats ≤ 60 lines, workers ≤ 45, rubrics ≤ 50, methods pruned to what changes behaviour.
9. **Upstream text.** Keep Matt's wording where it is better than ours; adapt only the bindings
   (Linear, dydo, hosts); keep the attribution comment.
10. **Return shapes** match their consumer: the review block; the Issue Captain's review slot; the
    inquisitor's `confirmed | plausible | refuted` with `high | medium | low`.

## 7. Per-file brief and binding cross-references

| File | Verdict | Direction | Must reference |
|---|---|---|---|
| entry-point | rewrite | ≤ 25 non-blank lines: identity; read `dydo/index.md`; the flow map, one line per stage naming its skills; the two boundaries; pointer to the working-tree contract; `CLAUDE.md`/`AGENTS.md` kept in parity | index, every hat, working-tree-contract |
| index.template / dydo/index.md | fix | "Skills and Roles" becomes the taxonomy (hats, workers, methods, commands, workflow, rubrics) with one-line routing each | all skills by name |
| writing-for-agents | fix | keep upstream body; replace the compiler sentence with a pointer to the `skill-mechanics` resource | skill-mechanics, self-improvement |
| skill-mechanics (resource) | new | Matt's `SKILL-MECHANICS` adapted to dydo: `mode`, `description`, `emit`, `read-only`, `delegates`, `invocation`, Must-Reads, includes, resources, `dydo sync`, protected files | customizing-roles |
| co-thinker | fix | restore curiosity and "do your homework"; step for grilling and domain-modeling; research for facts; hand-off table (DR / FutureFeature / planner / wayfinder-via-admiral) | grilling, domain-modeling, research, planner, wayfinder |
| grilling | keep | faithful upstream; description already a trigger | — |
| grill-me | fix | "Call the Skill tool with `grilling`." one line; human-facing description | grilling |
| bro | fix | description: *Stop. That did not land — re-pitch it.*; body keeps STE + both glossaries; note it is the corrective for agent-speak anywhere | glossary, dydo-glossary |
| domain-modeling | import | glossary discipline for `dydo/glossary.md` and DRs (ADR test = hard to reverse + surprising + real trade-off); no CONTEXT.md | glossary, decisions, co-thinker |
| research | import | `emit: agent`, `read-only: true`; primary sources; cited Markdown at a named location or as an Issue comment; invoked by co-thinker, wayfinder, admiral | co-thinker, wayfinder |
| prototype | import | throwaway artifact to raise fidelity; `prototype/<name>` branch; linked from the question Issue | wayfinder, co-thinker |
| planner | fix | `emit: agent`, `planner: strong`, while remaining a hat; "Start only when ripe" stays; invoker names one of two targets via resources; tracer bullets; required Issue fields incl. base branch; hand-off to reviewer(plan) then admiral | project, issue, wayfinder, codebase-design, writing-good-briefs, reviewer, admiral |
| planner-resource-project | new | plan skeleton with frontmatter (`title`, `status`, `area`, `type`, `linear-project`), the six sections, `## Not yet specified` when foggy, amendment convention | wayfinder, reviewer(plan) |
| planner-resource-issue | new | the Issue-resolution plan: files, pattern to copy with path, steps, edge cases, gates; authored by a spawned `planner(issue)` at the Issue Captain's direction, then implemented by delegated writers | issue-captain, working-tree-contract |
| wayfinder | reshape | method, `invocation: automatic`; map body, fog/frontier, **question Issues** (label `question`, `## Question`), types research/prototype/grilling/task; consumed by planner (chart) and admiral (work the map); no identity, no "modes" | grilling, research, prototype, planner, admiral |
| codebase-design | import | glossary of module/interface/depth/seam/adapter/leverage/locality + principles; used by planner, reviewer, test-writer | planner, reviewer, test-writer |
| issue-captain | new | `emit: agent`, `delegates: true`; anchor: *One Issue. One accountable captain.*; the Issue contract is the destination, its reviewed plan the route, and spawned planners, writers, and independent reviewers the crew; method: claim → environment check (right base, isolated worktree, base SHA posted, clean tree, owned paths) → parent record or one level of disjoint lane Sub-issues → spawn `planner(issue)` just in time until implementation is mechanical → direct all code, test and docs production through the crew, using `diagnosing-bugs` where needed → fresh binding reviewer loop (a fifth consecutive FAIL on one candidate escalates — the retired workflow's cap, now prose) → integrate passed lanes serially → combined gates and final parent review → review block on Issue + PR → return the pushed PR to admiral, or merge an atomic Issue → cleanup every captain-owned artifact; accountable for every delegated change; never authors production or self-reviews; fog → discovery → question Issue; escalation ladder and precedence order (DR 045 §6) inline | working-tree-contract, planner(issue), code-writer, test-writer, docs-writer, reviewer, diagnosing-bugs, admiral |
| admiral | rewrite from orchestrator | `invocation: explicit`; anchor: *One Project. Many captains. One accountable admiral.*; carry an approved Project from plan approval to a human-landable feature branch; one `issue-captain` owns each Issue and its crew while the admiral coordinates the captains; perfect plans are fiction, so the plan fixes the destination while the admiral uses `wayfinder` to create, split or resequence Issues as fog clears; open the feature; commission pickable Issues; integrate serially; merge review after every merge; record dated amendments; propose inquisition; escalation ladder and precedence order (DR 045 §6) inline; never implements or self-reviews | working-tree-contract, issue-captain, reviewer(merge), wayfinder, planner, inquisition, chief-of-staff |
| code-writer | polish | keep; red-before-green inline; return shape with the Issue Captain as consumer | issue-captain, coding-standards |
| test-writer | polish | keep; seams + anti-patterns (tautological, horizontal slicing) from `tdd`; anchor: *a good test is a contract* | issue-captain, codebase-design |
| docs-writer | polish | keep; assimilation-brief headings; writing-docs pointer | issue-captain, writing-docs |
| diagnosing-bugs | import | keep upstream phases; drop CONTEXT.md/ADR lines; `scripts/hitl-loop` reference removed or replaced; anchor: *tight loop that goes red* | issue-captain, test-writer |
| handoff | import | scratch-dir output; suggested skills section; redaction | — |
| working-tree-contract (guide) | new | DR 045 §8 as procedure: branch names, host vs fallback worktrees (`../<repo>.worktrees/`), Issue fields, environment check, cleanup, orphan sweep, atomic-Issue path | issue-captain, admiral, chief-of-staff, planner(issue) |
| reviewer | fix | anchor: *Gandalf — YOU SHALL NOT PASS*; five rubrics named as the invoker names them; review block as the only return | code, tests, docs, plan, merge, inquisitor |
| reviewer-resource-code | fix | add the Fowler smell baseline as judgement calls; review block | review block |
| reviewer-resource-tests | fix | align with test-writer's anti-patterns | — |
| reviewer-resource-docs | fix | add the §6 writing checklist as the rubric for agent-facing documents | writing-for-agents |
| reviewer-resource-plan | fix | align with planner-resource-project sections and question Issues; "Wayfinding Fog is not a gap" stays | planner-resource-project |
| reviewer-resource-merge | rewrite from merge-sprint | merge review: mechanical spot check scaling with size — merge artifacts, seams, gates rerun on the integrated state; plan acceptance at the final merge; no lens-hunting; no "two characters" | inquisition |
| inquisitor | polish | restore the calibration section and severity scale; name the inquisition as its only invoker | inquisition, reviewer |
| workflow-inquisition.js | fix | `confirmed: true` arg gate; prompts cite `.claude/skills/reviewer/resources/merge.md`; inquisitor prompts carry the lens name | inquisitor, reviewer(merge), docs-writer |
| chief-of-staff | fix | `invocation: explicit`; anchor: *the human's attention is the scarcest resource*; the three lists; HITL question surfacing + grilling; board hygiene incl. orphan sweep; routes to admiral | grilling, admiral, self-improvement, working-tree-contract |
| self-improvement | fix | keep threshold/lever/authority/rollback; add `retro`'s lens list (navigation, automated checks, coding standards, entry point size, tool economy, no-ops, information access) | writing-for-agents |
| walkthrough | new | `invocation: explicit`; argument = what to walk through; output = brief for the human: what changed and why (Issues/DRs), where to look, how to try it, what reviewers flagged or deferred; ephemeral | — |
| teach | import | as upstream; workspace = current directory | — |
| improve-codebase-architecture | import | as upstream minus CONTEXT.md; HTML report to scratch; grills the chosen candidate | codebase-design, grilling, co-thinker |
| orchestrator, run-sprint workflow, merge-sprint resource | delete | — | — |

## 8. Watch-outs

- Do not let synchronized generated output stand in for source review: every Gate C review reads
  the **template**, then confirms the compiled skill matches — and the compiled skill is only
  source-derived after `dydo template update` has refreshed the local copy in that worktree; a
  `dydo sync` without it compiles the stale `dydo/_system/templates/` copy.
- Do not paste `596e3839` back; take its anchors, not its runtime ceremony.
- Do not write to `CLAUDE.md`, `AGENTS.md`, `dydo.json` or `dydo/index.md` from any Issue but H-1
  (`dydo.json` nudges and bindings), H-3 (`dydo/index.md`, `CLAUDE.md`, `AGENTS.md`) and H-10.
- Do not treat a Codex spawn that fails to see its skill as a blocker: record it, finish, file the
  follow-up.
- Do not forget to re-trust the Codex hooks (`~/.codex/config.toml`) after H-1 lands; until then
  Codex sessions run unguarded.
- Do not skip the H-11 pass or fold it into H-10: it is the human's harmonization, and its edits get
  their own review.

## 9. Running this plan under today's tooling

### Admiral brief

Paste this into a fresh Claude Code or Codex session started in the repository:

> You are the **admiral** for the Linear Project *dydo 3.0 / Harmonize the skill system*. Read, in
> this order: `dydo/project/decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md`,
> then `dydo/project/plans/dydo-3-skill-harmonization.md` — the plan is your contract: §4 is the Issue
> map, §5 your sequence, §6 and §7 the briefs you hand out, §9 the prompts you use. First run
> `dotnet build DynaDocs.sln -c Release` so `bin/Release/net10.0/dydo.dll` matches HEAD; use that
> dll for every `dydo` command (the installed CLI is older). Confirm you can reach Linear
> (linear-server MCP); if not, stop and tell the human before doing anything else. Wear the current
> `orchestrator` skill; this Project is its last use. Rules: you never edit a source yourself; every
> file a writer touches gets a fresh reviewer sub-agent whose review block you post on the Issue
> before you merge; you merge Issue branches into `feature/skill-harmonization` serially in §5 order
> and run Gate E after H-9; you stop and ask the human only for the four gates in DR 045 §7 or a
> conflict with a DR. Start with the kickoff in §5, then dispatch H-1 and H-3 in parallel; dispatch
> H-2 when H-1 has merged; merge H-3 only after H-2.

### Writer prompt (one per file, H-3 … H-8)

> Rewrite one dydo source: `Templates/<file>` (or the `dydo/` document your §7 row names). Read
> first: DR 045; plan §6 (the checklist) and your row in §7; the current file; for an import, the upstream file at
> `https://raw.githubusercontent.com/mattpocock/skills/6654f6b60cd9d5be8b54c6fafe44346dabeb3b76/skills/<path>/SKILL.md`;
> `Templates/skill-writing-for-agents.template.md` and its `skill-mechanics` resource. Deliver only
> the rewritten template (never run `dydo sync` into the tree, never touch `dydo/_system/`), plus a
> five-line note: the anchor you chose, what you cut and why, the cross-references carried, the
> return shape and its consumer, one open doubt. Obey the line budget, Gate C's forbidden words, and
> the attribution comment for upstream text.

### Reviewer prompt (one per file)

> Independently review `Templates/<file>` against plan §6, its §7 row, DR 045, and the `docs` rubric.
> FAIL on any of: a description that is not a trigger (or, for explicit-only, not a one-line
> human-facing pitch); a tagline that changes no behaviour; a missing or extra cross-reference
> against §7; a Gate C forbidden word; a line budget exceeded; upstream text changed without a
> binding reason; a return shape its consumer cannot parse; a link to a file that will not exist
> after regeneration. Return the review block with rubric `docs`, the candidate's path and SHA, and
> findings as file:line → consequence → correction.

### Code Issue prompt (H-1, H-2)

> Implement Linear Issue `<key>` — `<title>`. Contract: plan §4 row `<H-n>`, §3 design, Gate `<A|B>`.
> Owned paths are exactly the row's surface. Prove defects with a failing test first; replace any
> prose-freezing assertion you meet with a structural one. Run the full Gate and paste its output.
> Return: changed files, behaviour delivered, gate results, any contract deviation.

## Amendment — 2026-08-31

- H-9 owned paths gain `dydo/reference/configuration.md` (files-off-limits section only), carried from
  DYD-54's review: the section described one tier where H-1 shipped two. Admiral ruling on DYD-62.
- §7 reviewer row — "review block as the only return" is narrowed by admiral ruling (DYD-60): a
  defect the candidate neither created nor exposed is reported as one line after the block, prefixed
  `Observation (out of scope, non-binding):`, never as a finding; the `merge` rubric and the
  reviewer skill state it. Flagged for the human's H-11 pass as a possible DR 045 §6 clarification.
- §7 co-thinker row — "wayfinder-via-admiral" is reconciled with DR 045 §1 and §4 by admiral ruling
  (DYD-57's merge review): a foggy Project not yet charted goes to the `planner`, who charts it with
  `wayfinder` (§1's Chart row); the `admiral` receives question Issues only for a Project already in
  delivery (§4's routing). The co-thinker's Handoff row and the prototype's placement sentence
  follow the planner route.
- **H-10 surface** — `.gitattributes` gains `Templates/*.js text eol=lf` so workflow templates
  stay LF at source like their compiled `.claude/workflows/*.js`; a one-line surface addition
  outside H-10's owned paths, ruled by the admiral on DYD-63 (2026-08-31).
