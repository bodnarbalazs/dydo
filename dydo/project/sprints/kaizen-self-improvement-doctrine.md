---
title: Kaizen Self-Improvement Doctrine
seq: 11
status: done
gate-result: audit PASS (2026-08-08; 2,526 tests; 131/131 coverage)
area: project
type: context
---

# Kaizen Self-Improvement Doctrine

Plant a tiny recurring-improvement cue and route its safe method through one portable skill.

## 1. Specification

**Intent** — Plant a small, runtime-neutral kaizen seed in every new project's entry point and
route the detail into one reusable `self-improvement` skill. When an agent sees recurring friction,
failure, correction, or workaround in the agent harness, it should identify the pattern and
propose one small durable harness improvement without gaining authority, widening its task, or
creating a trail of generic doctrine documents.

**In scope**

- Add exactly two sentences to the canonical entry-point template:

  > Practice kaizen: when a failure, correction, or workaround recurs, treat the pattern as
  > evidence that the harness may need one small, durable improvement. Invoke the
  > `self-improvement` skill to choose and route the smallest justified change without expanding
  > the current task or silently changing policy.
- Add the same two sentences to this repository's current `CLAUDE.md` and `AGENTS.md` entry
  surfaces without otherwise reconciling their existing content.
- Ship one concise `mode-self-improvement.template.md` with `emit: skill`.
- Compile the skill to both native skill surfaces while emitting no spawnable agent definition.
- Make the skill explain kaizen, use `1.01^365 ≈ 37.8` only as an illustration of compounding,
  define a repeat-signal trigger, select the least invasive improvement, route durable knowledge,
  and forbid autonomous overreach.
- Scope kaizen exclusively to the agent harness and its documentation/process surfaces. Product
  features and product code are never a self-improvement lever, proposal, or side effect.
- Add focused discovery, compiler, sync, entry-point, and integration regressions.

**Out of scope**

- A new decision, guide, backlog item, pitfall, hook, nudge, configuration field, command, role
  model binding, or workflow.
- Automatic monitoring, scoring, telemetry, memory creation, recursive self-editing, or a literal
  one-percent-per-day performance promise.
- Any product-feature or product-code proposal or change, even when benevolent, adjacent,
  otherwise authorized, or plausibly useful to the product being built.
- Changing the existing auto-memory decision or backlog, or adding its routing paragraph to the
  canonical entry template in this sprint.
- Reformatting or fully regenerating this repository's divergent current entry files.
- Version bumps, package manifests, publication, release notes, or an official release.
- Recompiling, overwriting, staging, or committing any pre-existing generated skill file that is
  already dirty in the shared working tree.

**Acceptance criteria**

- `Templates/entry-point.template.md`, `CLAUDE.md`, and `AGENTS.md` contain the exact two-sentence
  seed above once each; the template contains no platform-specific wording.
- A fresh all-integrations init produces `CLAUDE.md` and `AGENTS.md` with identical content and
  the exact seed.
- `Templates/mode-self-improvement.template.md` is discoverable as role
  `self-improvement`, has a non-empty description, and has `EmitAgent == false`.
- `dydo sync` emits `.claude/skills/self-improvement/SKILL.md` and
  `.agents/skills/self-improvement/SKILL.md`, whose content is identical, while
  `.claude/agents/self-improvement.md` and `.codex/agents/self-improvement.toml` do not exist.
- The compiled skill contains the verbatim trigger, least-invasive lever order, destination table,
  verification/rollback requirement, and every anti-overreach boundary specified in slice 1.
- The canonical template and both regenerated compiled outputs contain exactly:
  `Do not propose or perform product-feature or product-code changes, including benevolent or
  otherwise authorized adjacent product work; kaizen here applies only to the agent harness and
  its documentation and process surfaces.` The focused sync regression asserts this exclusion
  phrase.
- The canonical Method lever keeps `harness implementation code only when the earlier layers
  cannot express the behavior.` contiguous on one physical template line, so the compiled skill
  contains the exact literal required by the focused sync regression.
- The canonical Boundary bullet keeps the complete categorical product-exclusion sentence on one
  physical template line, so the compiled skill contains the full exact sentence required by the
  same focused sync regression.
- The two new committed generated outputs come from a source-built sync in a disposable project;
  the repository-wide sync command is never run against the dirty shared working tree.
- A before/after manifest over the 16 protected generated artifacts named in Ordering & isolation
  proves their shared-tree SHA-256, scoped status, and staged state are unchanged.
- The historical initial slice gate retains its eight changed/owned paths. The current
  audit-remediation gate stages and compares exactly four changed paths — the mode template, both
  generated skill outputs, and `SyncCommandTests.cs` — without fake edits to the four already
  committed unchanged regression files.
- Focused gates, the full isolated runner, forced coverage gap gate, and source-built
  `dydo check` pass.

**Questions & answers**

- **What is the mentality called?** Kaizen, commonly rendered as continuous improvement through
  small changes. The compound expression `1.01^365 ≈ 37.8` is a useful illustration, not an
  empirical guarantee or a quota to manufacture changes.
- **What counts as a trigger?** A repeated failure, correction, workaround, or avoidable friction
  in the agent harness with concrete evidence. Product behavior never triggers this skill. One
  weak or isolated harness inconvenience does not trigger framework work; one severe systemic
  harness incident may still be routed through the normal issue path.
- **Does invoking the skill authorize edits?** No. The current role, user request, slice, and
  reviewed workflow remain the authority boundary. Without pre-existing authority, the agent
  creates or modifies nothing: it reports the evidence and suggests one destination/change.
- **What is the preferred intervention order?** Clarify a canonical prompt or skill first; use a
  warn-level nudge for a recognizable risky action; use a hook only when action-time guidance or
  enforcement is demonstrably needed; change harness implementation code only when the prior
  layers cannot express the behavior. Product implementation is never in this order. Existing
  harness records and mechanisms are reused before any new one is proposed.
- **Can the doctrine improve the product itself?** No. It applies only to the agent harness and
  its documentation/process surfaces. It never proposes or performs product features or product
  code, even when that adjacent work would otherwise be authorized or beneficial.
- **Where does durable knowledge go?** When record creation or editing is already in task scope
  and allowed by the current role/workflow, observed defects go to issues, schedulable
  improvements to backlog records, accepted non-obvious policy to decisions, and stable
  operational guidance to the narrowest existing guide or pitfall. Otherwise those are suggested
  destinations only. Project facts and temporary workarounds do not become harness memory.
- **Should the entry point explain all of this?** No. It plants exactly two sentences and names
  the skill. All method and boundaries live in the skill template.
- **Was an earlier implementation found?** No. Case-insensitive repository search, all-ref Git
  history search, branch inspection, and stash inspection found no self-improvement/kaizen seed,
  role, or skill. Decision 038, its backlog, and the current `CLAUDE.md` memory line are adopted
  prior art because they identify recurring behavioral memory as evidence of missing framework
  guidance; they do not implement this doctrine.

## 2. Prior art

- `Templates/entry-point.template.md` is the single runtime-neutral source used by
  `TemplateGenerator.GenerateEntryPointMd`; `InitCommand` materializes it as both entry filenames.
- `Templates/mode-planner.template.md` and `Templates/mode-co-thinker.template.md` demonstrate
  skill-only mode frontmatter and concise reusable methodology.
- `Services/RoleDefinitionService.cs` discovers every shipped `mode-*.template.md`; no registry or
  production code change is needed for a new skill-only template.
- `Commands/SyncCommand.cs` already compiles `emit: skill` roles to both skill surfaces and omits
  both agent-definition surfaces. This existing path is adopted unchanged.
- `DynaDocs.Tests/Services/RoleDefinitionServiceTests.cs`,
  `DynaDocs.Tests/Commands/SyncCommandTests.cs`, and
  `DynaDocs.Tests/Integration/CodexSyncArtifactsE2ETests.cs` are the existing discovery and
  dual-compilation seams.
- `DynaDocs.Tests/Integration/InitCommandTests.cs` already verifies all-integrations init creates
  both entry files; extending that test proves the one-template/two-surface contract.
- Decision 038 and `dydo/project/backlog/auto-memory-policy.md` establish the adopted routing
  principle: recurring behavioral notes reveal missing framework guidance, while memory is not a
  project archive. Rejected: adding more memory instructions or implementing the deferred memory
  write nudge here.
- Rejected: a new command, global hook, or automatic self-modifier. The user's requested seed is
  prompt-side, and the existing mode compiler supplies the portable skill packaging.

## 3. Design

The entry point receives the exact two sentences from the specification after its existing skill
routing paragraph. Current `CLAUDE.md` and `AGENTS.md` receive only those same sentences at EOF;
their pre-existing difference, including the memory paragraph present only in `CLAUDE.md`, is
preserved. A fresh init still produces identical entry files because both are generated from the
canonical template.

The new mode template is skill-only. Its verbatim body is locked in slice 1. Its bounded method
establishes recurrence in the agent harness, searches existing harness routes, chooses one
least-invasive harness lever, classifies one harness destination, and names verification plus
rollback. Classification is not authorization: unless the current task and role/workflow already
authorize an edit, the only permitted output is a report of the harness evidence plus one
suggested harness destination/change. Product features and product code are categorically outside
the algorithm, even when adjacent product work has separate authorization. Its boundaries also
forbid hand-edited generated outputs, unauthorized global/user harness changes, unreviewed
enforcement escalation, widened task scope, temporary-workaround memory, and recursive
self-improvement.

No compiler source change is expected: enumeration plus `emit: skill` already handles the new
template. The compiler tests are regressions against that existing behavior. The two checked-in
skill outputs are generated in a disposable initialized project with the source-built binary,
then copied by exact path into the lane. This avoids running full sync over the shared tree's
unrelated dirty generated skills. Before generation, capture their hashes and status; after
generation and focused staging, require both to match.

Rollback is deletion of the one mode template and its two generated skill folders plus reversion
of the three entry surfaces and focused tests. There is no schema, migration, or runtime state.

## 4. Slice map

| # | slice file | files touched (disjoint) | deps | gate |
|---|---|---|---|---|
| 1 | `kaizen-self-improvement-doctrine-1-skill` | `Templates/mode-self-improvement.template.md`; `.claude/skills/self-improvement/SKILL.md`; `.agents/skills/self-improvement/SKILL.md`; `DynaDocs.Tests/Services/TemplateGeneratorTests.cs`; `DynaDocs.Tests/Services/RoleDefinitionServiceTests.cs`; `DynaDocs.Tests/Commands/SyncCommandTests.cs`; `DynaDocs.Tests/Integration/CodexSyncArtifactsE2ETests.cs`; `DynaDocs.Tests/Integration/TemplateOverrideTests.cs` | — | skill-focused isolated runner + disposable-project sync smoke |
| 2 | `kaizen-self-improvement-doctrine-2-entry-seed` | `Templates/entry-point.template.md`; `CLAUDE.md`; `AGENTS.md`; `DynaDocs.Tests/Integration/InitCommandTests.cs` | 1 | entry-focused isolated runner + exact-content smoke |

## 5. Ordering & isolation

Use two serial slices in one isolated lane, because slice 2's initialized-project assertion should
see the skill template already present and the merged sprint must be audited as one prompt
contract. The files are disjoint; slice 2 depends on slice 1 semantically, not because it edits
the same files.

Before creating the lane, the orchestrator works from the **shared dirty worktree**, resolves its
absolute root, and captures an immutable manifest with relative path, absolute root, scoped
`git status --short`, scoped `git diff --cached --name-status`, and SHA-256 for every protected
path below. The manifest is passed verbatim in the slice dispatch brief; the slice does not
recreate it against the clean lane.

Protected manifest paths:

- `.agents/skills/chief-of-staff/SKILL.md`
- `.agents/skills/co-thinker/SKILL.md`
- `.agents/skills/orchestrator/SKILL.md`
- `.agents/skills/planner/SKILL.md`
- `.agents/skills/reviewer/SKILL.md`
- `.agents/skills/test-writer/SKILL.md`
- `.claude/agents/reviewer.md`
- `.claude/agents/test-writer.md`
- `.claude/skills/chief-of-staff/SKILL.md`
- `.claude/skills/co-thinker/SKILL.md`
- `.claude/skills/orchestrator/SKILL.md`
- `.claude/skills/planner/SKILL.md`
- `.claude/skills/reviewer/SKILL.md`
- `.claude/skills/test-writer/SKILL.md`
- `.codex/agents/reviewer.toml`
- `.codex/agents/test-writer.toml`

The implementation commands run only inside the **dedicated lane worktree**. Slice 1 performs
sync only inside a further disposable project under the system temp directory and copies only the
two new `self-improvement/SKILL.md` files into the lane. The initial slice implementation stages
its exact eight-file ownership allowlist. A later audit-remediation lane stages only the exact four
owned files that remediation actually changes: the mode template, both generated skill outputs,
and `SyncCommandTests.cs`. Each phase proves its complete lane index equals its phase-specific
allowlist, then checks the cached diff; never use `git add -A` or `git add .`, and never create a
fake edit merely to make an unchanged owned file stageable.

After the lane commits merge, the orchestrator returns to the same absolute **shared dirty
worktree** recorded in the manifest, takes the same five-field snapshot of the same 16 paths, and
requires structural equality with the pre-lane manifest. A mismatch blocks completion; do not
repair it with stash, reset, restore, or checkout.

## 6. Watch-outs

- Keep the seed exactly two sentences and identical across the template and both current entry
  files. Do not move the skill's detailed checklist into entry surfaces.
- Do not describe the compound expression as guaranteed agent performance.
- A skill-only role is not a new agent identity. Never add either agent-definition output.
- The self-improvement skill is subordinate to current authority; its wording must not invite
  unsolicited edits to global prompts, hooks, configuration, memory, or adjacent code.
- Do not blur harness improvement into product improvement. Product features and product code are
  excluded even if an agent believes they would help or sees separate authority for adjacent
  product work.
- Do not turn every hiccup into a record. Require recurrence evidence, deduplicate first, and
  produce at most one smallest justified proposal per pattern.
- Do not edit generated `SKILL.md` files by hand. Generate both from the canonical template and
  assert byte equality.
- Do not run `dydo sync` or source-built sync from the shared repository root while unrelated
  generated skills are dirty.
- Do not edit indexes or create a changelog/decision/backlog/guide for this small doctrine.

## Plan review

**Round 1: FAIL** (2026-08-08, fresh-eyes reviewer).

The cited production seams are accurate: mode templates are wildcard-embedded and enumerated,
`emit: skill` selects both skill writers without either agent writer, both skill writers use the
same normalized builder, and init obtains both entry filenames from one entry generator. The two
slices also own disjoint file sets. Independent baseline gates are green: 2,524 tests passed with
10 live tests skipped; forced coverage passed 131/131 modules; and all three plan records have
zero `dydo check` errors (only orphan warnings while the records are untracked).

- **Finding: slice 1's diff gate does not inspect its three new artifacts.** The slice creates the
  template and two generated skill files as new paths
  (`kaizen-self-improvement-doctrine-1-skill.md:28-30`), but its gate runs
  `git diff --check -- ...` before the stated pre-commit staging check (lines 163-172). An
  unstaged Git diff omits untracked files; after staging, the same non-`--cached` diff omits their
  staged content. Stage the exact owned-path allowlist first, verify
  `git diff --cached --name-only` equals that allowlist, then require
  `git diff --cached --check -- <owned paths>` so the template and both generated outputs are
  actually checked.
- **Finding: dirty-tree isolation is neither location-safe nor self-contained.** The root says to
  create a dedicated lane worktree and then capture status/hashes for dirty generated paths
  (`Ordering & isolation`, lines 161-166); slice 1 refers only to paths "reported at sprint
  kickoff" and never lists them or identifies the shared working-tree root
  (`kaizen-self-improvement-doctrine-1-skill.md:129-146`). A fresh implementer given only the
  slice cannot know the protected paths, and hashing those paths inside the clean lane proves
  nothing about the dirty bytes in the shared worktree. Specify that the orchestrator captures an
  immutable manifest of absolute shared-tree root, relative path, status, staged state, and
  SHA-256 before lane creation; pass that exact manifest to the slice; and require the post-merge
  comparison against the same absolute shared-tree paths. Keep lane allowlist checks separate.
- **Finding: the skill body is not mechanically fixed and its no-authority rule still permits an
  unauthorized record write.** Slice 1 explicitly allows prose to be rewritten while requiring
  policy not to change (`kaizen-self-improvement-doctrine-1-skill.md:48-50`), then gives several
  non-exact semantic prompts; meanwhile its focused test requires case-sensitive fragments such
  as `Kaizen` and unspecified "verification"/"rollback language" (lines 112-122). More
  importantly, the method says that when the task does not authorize the improvement the agent
  may "record or suggest it" (lines 76-77), and the root repeats that choice (Specification Q&A,
  lines 80-82), even though creating an issue/backlog/decision is itself an edit. Lock the full
  concise template body verbatim (or provide an exact expected compiled body) and state that
  without pre-existing authority the agent must not create or modify any record or harness
  surface: it may only report the evidence and suggest one destination/change. Align the focused
  assertions with that exact body.

Status remains `plan-review`; implementation is not green-lit.

**Round 1 remediation** (2026-08-08): both slice gates now stage their exact allowlists first,
compare the complete cached index to that allowlist, and run the cached diff check over every
owned path. The root and slice now distinguish the shared dirty worktree, dedicated lane, and
disposable generation project; lock the 16 protected paths; and provide one reproducible
before/after manifest algorithm for scoped status, staged state, and SHA-256. Slice 1 now locks
the complete skill template verbatim. Its authority branch creates or modifies nothing unless
the current task and role/workflow already authorize the edit; otherwise it only reports evidence
and suggests one destination/change. Status remains `plan-review` pending round 2.

**Round 2: PASS** (2026-08-08, fresh-eyes reviewer).

All round-1 findings are closed. Both slices stage and compare their complete exact allowlists
before cached diff checks, including every new artifact. The shared-tree isolation contract now
locks the absolute root and the exact 16-path protected manifest with scoped status, staged
state, and SHA-256 before/after comparison; those paths match the current generated-output dirty
set. Slice 1 locks the complete skill body verbatim and explicitly creates or modifies nothing
without existing task and workflow authority, while slice 2 locks the two-sentence seed. The
cited template/compiler/test seams remain accurate, slice ownership is exactly disjoint, and the
steps are mechanically executable without autonomous scope expansion.

Independent gates are green: all three records have zero `dydo check` errors (only expected
orphan warnings while untracked), the isolated suite passed 2,524 tests with 10 live tests
skipped, and forced coverage passed 131/131 modules. Implementation is green-lit; status is
`active`.

**Implementation raise-hand amendment** (2026-08-08): adding the required embedded mode template
correctly increases `TemplateGenerator.GetAllTemplateNames()` from 14 to 15, exposing the existing
exact inventory assertion in `DynaDocs.Tests/Integration/TemplateOverrideTests.cs`. Slice 1 now
owns that file and mechanically adds `mode-self-improvement.template.md`, changes the explanatory
count from 9 mode templates plus 5 resources to 10 plus 5, and changes the expected total from 14
to 15. Its focused filter and exact staging allowlist now contain all eight owned paths. Slice 2
remains file-disjoint. The sprint returns to `plan-review` pending review of this amendment.

**Implementation raise-hand amendment review: PASS** (2026-08-08, fresh-eyes reviewer).

The amended ownership fully closes the inventory mismatch. Production currently yields nine mode
templates plus five reviewer resources; adding the resource-less embedded self-improvement mode
raises `GetAllTemplateNames()` from 14 to 15 exactly as specified. The root slice map, slice 1
touch list, focused filter, staging allowlist, and cached diff check consistently cover the same
eight paths, including `DynaDocs.Tests/Integration/TemplateOverrideTests.cs`. Slice 2 remains
exactly disjoint, and the unchanged 16-path shared-tree manifest and disposable-project sync
isolation remain valid. No new implementation decision or scope ambiguity was introduced.

All three records remain `dydo check`-clean with zero errors. The focused integration class
passed 12/12 tests, the full isolated suite passed 2,524 tests with 10 live tests skipped, and
forced coverage passed 131/131 modules. The amendment is green-lit; status is `active`.

**Merged-sprint audit: FAIL — binding scope clarification** (2026-08-08).

The human clarified that kaizen in this doctrine applies only to the agent harness and its
documentation/process surfaces. The locked body said only `code`, which could be read as product
implementation code, and it lacked an explicit product exclusion. That ambiguity is an audit
finding even though the implementation matched the reviewed plan.

**Audit-finding amendment** (2026-08-08): slice 1 now changes the final Method lever to `harness
implementation code only when...`, adds the exact Boundary exclusion sentence, requires both
compiled outputs to be regenerated, and adds an exact focused regression assertion. The eight
owned files and all dirty-tree isolation rules are unchanged; slice 2 and its concise seed are
unchanged. The sprint returns to `plan-review` pending amendment review.

**Audit-finding amendment review: PASS** (2026-08-08, fresh-eyes reviewer).

The locked body now binds kaizen to the agent harness at every routing point: it defines the
harness and excludes the product, limits triggers to agent-harness evidence, limits the final
lever to harness implementation code, labels every destination as a harness classification that
routes no product work, and categorically forbids proposing or performing product features or
product code even when benevolent or otherwise authorized adjacent work. The authority check
therefore cannot reopen product scope.

Slice 1 requires both compiled outputs to be regenerated from the canonical template, remain
byte-identical, and pass exact focused assertions for the trigger, harness-code lever, destination
scope, and complete product-exclusion sentence. Its root map, touch list, focused filter, staging
allowlist, and cached diff gate remain consistent at the same eight files. Slice 2 has no diff,
the slices remain disjoint, and the 16-path shared-tree protection contract is unchanged and
still matches the protected dirty set.

All three records have zero `dydo check` errors. The full isolated suite passed 2,526 tests with
10 live tests skipped, and forced coverage passed 131/131 modules. The amendment is green-lit;
status is `active`.

**Implementation blocker amendment** (2026-08-08): the reviewed verbatim template wrapped
`harness implementation code` and `only when...` across a physical newline, while the focused
regression requires their exact contiguous literal and the compiler correctly preserves template
newlines. Slice 1 now locks the complete `Choose one lever` list item onto one physical line and
explicitly forbids reflowing it. Semantics, harness-only scope, eight-file ownership, generated
output regeneration, and slice 2 remain unchanged. The sprint returns to `plan-review` pending
amendment review.

**Line-wrap amendment review: PASS** (2026-08-08, fresh-eyes reviewer).

Slice 1 locks the complete `Choose one lever` item to exactly one physical template line; the
single 337-character line contains and ends with the mandated contiguous literal. The compiler preserves it:
include resolution does not reflow prose, frontmatter stripping takes an unchanged body
substring, ordered-list renumbering replaces only the numeric prefix, skill construction
interpolates the resulting methodology, and the write boundary only normalizes line endings.
The planned exact focused assertion is therefore satisfiable.

The amendment changes no semantics: harness-only triggers, levers, destinations, categorical
product exclusion, and generated-output regeneration remain binding. Slice 1 still owns the same
eight files and retains the same filter and staging allowlist; slice 2 has no diff. All three plan
records have zero `dydo check` errors. The unchanged implementation baseline remains green at
2,526 isolated tests passed with 10 live tests skipped and forced coverage at 131/131 modules.
The amendment is green-lit; status is `active`.

**Audit-remediation staging blocker amendment** (2026-08-08): the current remediation lane base
already commits four slice-owned regression files unchanged — `TemplateGeneratorTests.cs`,
`RoleDefinitionServiceTests.cs`, `CodexSyncArtifactsE2ETests.cs`, and
`TemplateOverrideTests.cs`. Git cannot stage those unchanged files, so applying the historical
eight-file cached-index equality gate to this lane is impossible without fake edits. Slice 1 now
retains that eight-file gate as the historical initial-implementation requirement and adds a
current audit-remediation gate whose complete cached index must equal exactly the four genuinely
changed owned paths: the mode template, both generated `SKILL.md` outputs, and
`SyncCommandTests.cs`. Semantics, scope, tests, eight-file ownership, dirty-tree isolation, and
slice 2 are unchanged. The sprint returns to `plan-review` pending amendment review.

**Boundary line-wrap blocker amendment** (2026-08-08): the categorical product-exclusion bullet
was physically wrapped across three template lines while the focused regression requires the full
sentence as one contiguous literal. Slice 1 now locks that complete bullet to one physical line
and explicitly forbids reflowing it. Wording, semantics, harness-only scope, test strategy,
eight-file ownership, generated-output regeneration, and slice 2 are unchanged. The sprint
returns to `plan-review` pending amendment review.

**Boundary line-wrap amendment review: PASS** (2026-08-08, fresh-eyes reviewer).

Slice 1 now locks the complete categorical product-exclusion bullet to exactly one physical
template line. The single 226-character line contains the full mandated sentence contiguously.
The compiler preserves it: include resolution and frontmatter/section extraction do not reflow
prose, the ordered-list renumberer does not touch this unordered bullet, skill construction
interpolates it unchanged, and the writer only normalizes line endings. The planned exact focused
assertion is therefore satisfiable.

The prior one-line lever remains intact and contiguous. Wording, harness-only semantics, test
strategy, generated-output regeneration, the same eight owned files/filter/staging allowlist, and
dirty-tree isolation are unchanged; slice 2 has no diff. All three plan records have zero
`dydo check` errors. With no implementation or test change in this amendment, the immediately
preceding baseline remains green at 2,526 isolated tests passed with 10 live tests skipped and
forced coverage at 131/131 modules. The amendment is green-lit; status is `active`.

**Audit-remediation staging amendment review: PASS** (2026-08-08, fresh-eyes reviewer).

The original skill commit changed exactly the eight files retained by the historical ownership
and initial gate. Against the current remediation lane base, the complete staged index contains
exactly the four genuinely changed owned paths: the mode template, both regenerated skill
outputs, and `SyncCommandTests.cs`. The other four named owned regressions have an empty `HEAD`
diff, are explicitly excluded from remediation staging, and require no fake edit. Those four
changed paths are sufficient for the harness-scope wording, both compiled outputs, and their exact
regression assertions; the generated outputs remain SHA-256 identical.

The phase-specific cached-index comparison and scoped cached `diff --check` execute successfully
against the live lane. Semantics, harness-only scope, eight-file ownership, regeneration,
shared-tree manifest isolation, and slice 2 are unchanged. All three plan records have zero
`dydo check` errors. The remediation lane passed 58/58 focused tests, 2,526/2,536 full isolated
tests with 10 live tests skipped, and forced coverage for all 131 modules. The amendment is
green-lit; status is `active`.
