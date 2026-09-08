# DYD-141 capacity and captain-ownership specification

## Immutable contract

DYD-141 is a `Bug` / `AFK` correction on `DYD-141-preserve-captain-ownership` from
`1bc93c5d2122305ad5e3a3fc997d7a6609ae77e4`. It corrects the Admiral, Issue Captain and narrow
working-tree guidance so an Issue Captain retains every crew dispatch and its scope when the native
agent budget is exhausted. A saved brief is a portable handoff to a captain; it never authorizes an
Admiral to commission the captain's specifier, production worker, hardener or reviewer. The route
budgets the complete native task tree, serializes stages when necessary, preserves every required
fresh role, and raises a concrete host limitation to the human when documented capacity or lifecycle
handling cannot make the next captain-owned stage runnable. The change adds prompt/protocol proof and
bounded native evidence only; it adds no scheduler, queue, cleanup primitive or enforcement engine.

The pre-change reproduction is the 2026-09-08 observation in
`dydo/agents/workspace/20260908-review-capacity-observation.md`: at four live inventory entries a
captain-owned spawn was refused and the Admiral later dispatched from the saved brief. The first fact
is bounded native evidence; the second is the defect. Official native configuration establishes that
`agents.max_concurrent_threads_per_session` counts open spawned threads and excludes the primary
thread; its schema is an unsigned integer with minimum 1 and no maximum. DYD-86 retains ownership of
durable `dydo init` configuration emission. DYD-141 may record the user's separately validated local
value 16, backup and hashes, but must not emit that value or claim it as a project/default setting.
DYD-88 retains the broader lifecycle matrix.

The user's local configuration was preservation-safely changed to 16 before this specifier was
created. A completed researcher disappeared from inventory before or with this replacement's
accepted spawn, so that acceptance may be ordinary reclamation. Root's subsequent necessary fresh
captain spawn and that captain's necessary fresh spec-review spawn both refused at four inventory
entries. The value 16 is schema-valid but its consumption or effect in this existing desktop task is
not proved. Desktop reload semantics are undocumented. The CLI visible during specification reports
`codex-cli 0.153.4`; no evidence available to this worker proves that it is the desktop backend's
version or proves the effective model identity.

## Spec

### Scenarios

Production adds `DynaDocs.Tests/Features/captain-capacity.feature` with these scenarios and binds only
their prompt/compiler assertions in `DynaDocs.Tests/Steps/CaptainCapacitySteps.cs`.

```gherkin
Feature: Captain ownership under native agent capacity

  Scenario: An Admiral commissions work only through the Issue Captain
    Given a pickable Issue has a captain, an exact worker brief, and a native task-tree budget
    When the next captain-owned stage is runnable
    Then the Admiral commissions or resumes the Issue Captain from the record
    And only that captain commissions the stage with its exact scope
    And the worker return goes to the captain
    And the canonical Admiral and Issue Captain prompts and the shared guide state that a saved brief is a portable handoff and never Admiral-to-crew authority

  Scenario: A captain budgets a complete serial delivery chain
    Given the Issue requires specification, production, hardening, fresh Issue review, and a later Merge Sub-issue
    When native capacity cannot hold those agents concurrently
    Then the captain schedules the stages serially within the configured open-thread budget
    And completion or interruption is not treated as slot release without native evidence
    And no required fresh specifier or reviewer is reused or omitted to fit the budget

  Scenario: A capacity refusal preserves hierarchy and scope
    Given the native inventory accounts for the configured open spawned-thread budget
    When the captain makes one bounded attempt to commission the exact next stage and the host refuses it for capacity
    Then the captain preserves the record, candidate, hop SHA, and exact brief
    And the captain does not broaden the brief, retry blindly, or ask the Admiral to dispatch the crew
    And the Admiral does not dispatch that saved brief directly
    And documented lifecycle handling is used only when its effect is established for this host
    And if no documented handling makes the captain-owned stage runnable, the captain releases or returns the concrete limitation for escalation through Admiral to human

  Scenario: Configuration and observed capability remain separate evidence
    Given the local configuration records agents.max_concurrent_threads_per_session as 16 with preservation evidence
    And a completed researcher disappeared before or with one accepted replacement spawn
    When later necessary captain and fresh spec-review spawns are refused at four live inventory entries
    Then the evidence records that configuration consumption and effectiveness remain unproved in the existing task
    And it treats the accepted replacement as possible ordinary reclamation
    And it does not claim desktop reload, slot reclamation, backend, version, model, or lifecycle behavior
    And it leaves durable project configuration emission to DYD-86 and broader lifecycle claims to DYD-88
```

The feature examples deliberately use no table: changing any host number would not change the
protocol outcome, and the local value 16 belongs in evidence rather than shipped policy.

### Prompt wording obligations

The three authored templates must say, in their existing vocabulary:

- The Admiral budgets all open spawned threads in the native task tree before commissioning. It
  commissions or resumes a captain from the record and never uses a saved captain brief to dispatch
  crew. On refusal it preserves state, avoids blind retries, and escalates the exact host limitation
  only after documented capacity/lifecycle handling cannot make captain-owned work runnable.
- The Issue Captain alone commissions every specifier, production worker, hardener and reviewer for
  its Issue and keeps the worker's scope exact. It budgets the whole chain, uses serial stages when
  needed, and preserves fresh-specifier and fresh-reviewer obligations. A completed, returned or
  interrupted thread is not assumed to free capacity. On a bounded capacity refusal it preserves the
  record and brief and returns/releases through the normal hierarchy rather than delegating upward.
- The shared working-tree guide defines the same ownership and refusal route once, without a fixed
  numeric capacity or a claimed cleanup command. It states that a saved brief carries scope and
  resume context, not dispatch authority. Host configuration and an observed native result are
  recorded separately.

All managed copies must carry the same normative sentences after regeneration. No wrapper metadata,
role model, permissions, Mode/Type map, review rubric or freshness definition changes.

### Native proof

Retain the compact results and hashes in `dydo/agents/workspace/dyd141-capacity-evidence.md`; do not
paste raw transcripts into Linear or the PR.

1. Record the existing-task chronology: the completed researcher disappeared before or with the
   accepted replacement; the later necessary fresh captain and fresh spec-review spawns refused at
   four inventory entries; the local config key/value has the Admiral's backup and before/after
   hashes. Mark configuration consumption/effect, engine, desktop backend/version, effective model,
   reload and reclamation semantics `unproved` unless the native surface reports them directly.
2. After restart or otherwise fresh configuration consumption, deliver DYD-141 through its necessary
   stages only. The Admiral commissions the Issue Captain; that captain commissions production,
   consumes its return, commissions hardening, consumes its return, and then commissions a different
   fresh reviewer. Run these stages strictly serially and record inventory before each necessary
   spawn plus the parent/child names. Success requires Admiral -> Captain -> Crew throughout.
3. Do not create agents merely to fill capacity. If one necessary DYD-141 stage is refused at actual
   pressure, record that single refusal, preserve its exact brief/candidate/hop SHA, perform no retry,
   and return the concrete limitation through Captain -> Admiral -> human. The Admiral must not
   dispatch the brief. The already observed refusal supplies the refusal case unless a necessary
   post-restart stage naturally supplies newer bounded evidence.

If the host does not expose inventory or a documented control that makes the necessary serial stage
runnable, record that missing control as the concrete escalation result. Do not manufacture pressure.

### Gates

- Focused behavioral gate:
  `py DynaDocs.Tests/coverage/run_tests.py -- --filter FullyQualifiedName~CaptainCapacity --verbosity minimal`
  passes with all four scenarios green. The test reads authored and managed outputs and fails if any
  ownership, saved-brief, serial-budget, freshness, refusal or evidence-separation obligation is
  absent.
- Full repository gate:
  `py DynaDocs.Tests/coverage/run_tests.py -- --verbosity minimal` exits 0.
- Build gate: `dotnet build DynaDocs.sln -c Release --warnaserror` exits 0 with zero warnings.
- Documentation gate: `dydo check` exits 0 for the candidate.
- Generation gate: a source-built template update followed by two synchronizations leaves the second
  run byte-identical and changes only the generated paths named below. Each managed prompt contains
  the required source wording; wrapper files remain byte-identical.
- Native gate: the three-step proof above records the strictly serial captain-owned production,
  hardening and fresh review run plus the already observed or one naturally occurring necessary
  refusal, or the exact missing inventory/control escalation. A configuration value alone cannot
  pass it.
- Review gate: fresh `reviewer(spec)` PASS before production, fresh whole-Issue CODE PASS after the
  native evidence is committed, and fresh MERGE review in the later Merge Sub-issue.

## Plan

### Approach and pattern

Make a prompt-level correction in the three canonical templates, regenerate their exact managed
copies, pin it with one focused behavior feature, and retain one bounded native evidence record. This
follows the existing ownership split in `Templates/skill-admiral.template.md`,
`Templates/skill-issue-captain.template.md` and `Templates/working-tree-contract.template.md`, and the
source-to-output compiler contract in `dydo/understand/architecture.md`. It extends the existing
Admiral -> Issue Captain -> Crew and fresh-review rules; it does not introduce a runtime mechanism.

The Bug's reproduce-or-identify and fix placeholders should be collapsed into parent hops. The
pre-change observation is an adequate reproduction at the prompt/protocol seam, and the supplied
schema fact plus the observed refusals identify configured open-thread budgeting as the current
bounded hypothesis without proving that value 16 took effect in the existing task. Retaining serial
Bug Sub-issues would transfer the same prompt paths twice and add no independently trackable work.

### Owned files

Authored production sources:

- `Templates/skill-admiral.template.md` — add Admiral capacity budgeting, saved-brief boundary and
  refusal/escalation instructions.
- `Templates/skill-issue-captain.template.md` — add captain-only crew authority, serial chain budget,
  freshness preservation and refusal return/release instructions.
- `Templates/working-tree-contract.template.md` — add the narrow shared capacity and portable-brief
  contract.

Managed/generated outputs, changed only by source-built template update/sync:

- `dydo/_system/templates/skill-admiral.template.md`
- `dydo/_system/templates/skill-issue-captain.template.md`
- `dydo/guides/working-tree-contract.md`
- `.claude/skills/admiral/SKILL.md`
- `.claude/skills/issue-captain/SKILL.md`
- `.agents/skills/admiral/SKILL.md`
- `.agents/skills/issue-captain/SKILL.md`

Proof:

- `DynaDocs.Tests/Features/captain-capacity.feature` — the four boundary scenarios above.
- `DynaDocs.Tests/Steps/CaptainCapacitySteps.cs` — focused source/output and obligation assertions.
- `dydo/agents/workspace/dyd141-capacity-evidence.md` — configuration provenance, bounded desktop
  observations, native inventory/results and exact candidate identity.

No other generated file is owned. In particular `.claude/agents/issue-captain.md`,
`.codex/agents/issue-captain.toml`, `.agents/skills/admiral/agents/openai.yaml`, user configuration and
existing evidence are read-only and must remain byte-identical.

### Steps and hops

1. **Specify — nonempty.** Commit this file, obtain fresh SPEC review, and close the unused Bug
   reproduce/fix placeholders `Canceled` with the recorded collapse reason.
2. **Implement — nonempty.** First add the four failing focused scenarios. Then edit only the three
   authored templates, run the source-built template update and sync, inspect the named outputs, and
   make the focused/full/build/docs/generation gates pass. Commit the exact owned paths.
3. **Harden — nonempty.** Run the bounded native proof, record compact evidence and hashes, challenge
   every universal host claim, rerun all gates, and commit the evidence or any necessary correction
   inside the owned paths. Do not edit user config or broaden DYD-86/88.
4. **Issue review — required and fresh.** Judge the complete candidate against this spec and the CODE
   rubric, including generated parity, unchanged wrapper bytes, native evidence qualification and
   captain-only dispatch. Correct any FAIL through the owner named by the captain, then review afresh.
5. **Offer — nonempty captain operation.** Push the unsquashed Issue branch, open the PR with the PASS
   block, and keep DYD-141 `Ready to Merge`.
6. **Merge — later Sub-issue required.** Create/specify a Merge/AFK Sub-issue when the candidate and
   target SHAs are known; its captain-owned implementer merges into `feature/dydo-3-consolidation`,
   runs combined gates, and a fresh merge reviewer judges the integrated result. No merge lane is
   created by this specifier.

### Edge and failure behavior

- A configured number without a matching native observation remains configuration evidence only.
- The accepted replacement spawn may reflect ordinary reclamation because a completed researcher
  disappeared before or with it; it does not prove configuration consumption or effectiveness.
- A completed or interrupted child remains counted until the native inventory/control proves
  otherwise; never infer reclamation from status text.
- Inventory below the configured count that nevertheless refuses a spawn is a bounded mismatch:
  preserve the exact snapshot and escalate it, with no retries or hierarchy bypass.
- A missing close/delete/reclamation control is recorded as unavailable, not simulated or invented.
- Capacity pressure never permits scope broadening, role reuse, skipped fresh review, Admiral-authored
  production, Admiral review, or Admiral-to-crew dispatch.
- Regeneration that changes an unowned wrapper or unrelated artifact fails the generation gate and is
  reverted by path before the candidate is offered.

### Plan review

**Recommended.** This correction changes the canonical control-flow prompts and defines the precise
failure/escalation route after a native capacity refusal. Fresh spec review should verify that it
preserves captain-only authority and freshness, does not overstate configuration effectiveness in
the existing task, and does not steal DYD-86 configuration emission or DYD-88 lifecycle scope.
