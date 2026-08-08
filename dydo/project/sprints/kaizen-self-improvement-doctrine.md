---
title: Kaizen Self-Improvement Doctrine
seq: 11
status: active
gate-result: plan PASS (2026-08-08)
area: project
type: context
---

# Kaizen Self-Improvement Doctrine

Plant a tiny recurring-improvement cue and route its safe method through one portable skill.

## 1. Specification

**Intent** — Plant a small, runtime-neutral kaizen seed in every new project's entry point and
route the detail into one reusable `self-improvement` skill. When an agent sees recurring
friction, failure, correction, or workaround, it should identify the pattern and propose one
small durable harness improvement without gaining authority, widening its task, or creating a
trail of generic doctrine documents.

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
- Add focused discovery, compiler, sync, entry-point, and integration regressions.

**Out of scope**

- A new decision, guide, backlog item, pitfall, hook, nudge, configuration field, command, role
  model binding, or workflow.
- Automatic monitoring, scoring, telemetry, memory creation, recursive self-editing, or a literal
  one-percent-per-day performance promise.
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
- The two new committed generated outputs come from a source-built sync in a disposable project;
  the repository-wide sync command is never run against the dirty shared working tree.
- A before/after manifest over the 16 protected generated artifacts named in Ordering & isolation
  proves their shared-tree SHA-256, scoped status, and staged state are unchanged.
- Focused gates, the full isolated runner, forced coverage gap gate, and source-built
  `dydo check` pass.

**Questions & answers**

- **What is the mentality called?** Kaizen, commonly rendered as continuous improvement through
  small changes. The compound expression `1.01^365 ≈ 37.8` is a useful illustration, not an
  empirical guarantee or a quota to manufacture changes.
- **What counts as a trigger?** A repeated failure, correction, workaround, or avoidable friction
  with concrete evidence. One weak or isolated inconvenience does not trigger framework work;
  one severe systemic incident may still be routed through the normal issue path.
- **Does invoking the skill authorize edits?** No. The current role, user request, slice, and
  reviewed workflow remain the authority boundary. Without pre-existing authority, the agent
  creates or modifies nothing: it reports the evidence and suggests one destination/change.
- **What is the preferred intervention order?** Clarify a canonical prompt or skill first; use a
  warn-level nudge for a recognizable risky action; use a hook only when action-time guidance or
  enforcement is demonstrably needed; change code only when the prior layers cannot express the
  behavior. Existing records and mechanisms are reused before any new one is proposed.
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
establishes recurrence, searches for an existing route, chooses one least-invasive lever,
classifies one destination, and names verification plus rollback. Classification is not
authorization: unless the current task and role/workflow already authorize an edit, the only
permitted output is a report of the evidence plus one suggested destination/change. Its
boundaries also forbid hand-edited generated outputs, unauthorized global/user harness changes,
unreviewed enforcement escalation, widened task scope, temporary-workaround memory, and recursive
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
| 1 | `kaizen-self-improvement-doctrine-1-skill` | `Templates/mode-self-improvement.template.md`; `.claude/skills/self-improvement/SKILL.md`; `.agents/skills/self-improvement/SKILL.md`; `DynaDocs.Tests/Services/TemplateGeneratorTests.cs`; `DynaDocs.Tests/Services/RoleDefinitionServiceTests.cs`; `DynaDocs.Tests/Commands/SyncCommandTests.cs`; `DynaDocs.Tests/Integration/CodexSyncArtifactsE2ETests.cs` | — | skill-focused isolated runner + disposable-project sync smoke |
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
two new `self-improvement/SKILL.md` files into the lane. Each slice explicitly stages its exact
allowlist, proves the complete lane index equals that allowlist, and then checks the cached diff;
never use `git add -A` or `git add .`.

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
  staged content. Stage the exact seven-path allowlist first, verify
  `git diff --cached --name-only` equals that allowlist, then require
  `git diff --cached --check -- <seven paths>` so the template and both generated outputs are
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
