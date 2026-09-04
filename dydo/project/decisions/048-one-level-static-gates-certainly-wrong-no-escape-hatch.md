---
area: project
type: decision
status: accepted
date: 2026-09-05
accepted: 2026-09-05
participants: [balazs, Claude (Fable)]
---

# 048 — One-Level Static Gates: Certainly Wrong, No Escape Hatch

Replaces the three-tier coverage system (T1/T2/T3 with CRAP thresholds 30/15/5) with one set of
static gates applied to every module of every stack, admitted by a single rule: a gate exists only
where a violation is certainly wrong, at a threshold where no exception would be accepted, with no
per-file suppression. Settled in the 2026-09-03 to 2026-09-05 co-think and measured against a private
downstream project rather than this repository; the measurements and the transition debt are parked
in Linear DYD-95, and project-specific detail stays there.

---

## Context

- [Testing Strategy](../../guides/testing-strategy.md) and [DR 009](./009-crap-per-method-metric.md)
  hold every module to a tier: line and branch floors plus `CRAP = CC² × (1 − cov)³ + CC` at 30, 15
  and 5. At full coverage CRAP equals cyclomatic complexity, so each tier is also a hard cyclomatic
  cap. Cyclomatic counts every `case`, so a fully covered 21-case reducer scores 25 and fails T2 with
  nothing left to cover; the only move the gate leaves is splitting a flat switch into helpers, which
  is worse code. Measured downstream, nineteen C# and TypeScript functions are flat but branchy (CC
  above 15, cognitive at or below 15) and would be forced apart by T2.
- Tiers are assigned per file by annotation and registry. The downstream project carries 179 T2 and
  24 T3 entries; this repository carries two T2. The tier is a judgment made once and rarely
  revisited, and T2 and T3 demand shape changes no reviewer would ask for.
- The downstream project's C# coverage has never included async methods: its Coverlet settings
  exclude `CompilerGeneratedAttribute`, which the compiler places on every async state machine, so
  Coverlet drops the body of every `async` method. The T2 and T3 figures were measured on the sync
  remainder. Recorded with evidence in Linear DYD-95.
- Agents iterate against a gate's error until it passes, so they find the cheapest fix far more
  reliably than people do. A gate is sound for agent-written code only when the cheapest way to
  satisfy it is the change actually wanted. Cyclomatic fails this test (split anywhere). Cognitive
  complexity passes it (remove nesting, extract the nested block). Coverage alone fails it (a test
  without assertions). Class-coupling counts fail it (hide ten types behind one facade).
- The gate is the first line, not the only one. Tests, mutation testing, independent review against
  the coding standards and the domain model, and the hardener hop carry "could be better". The gate
  carries "certainly wrong".

## Decision

### 1. Selection rule

A static gate is admitted only where a violation is certainly wrong, its threshold sits where no
exception would be accepted, and no per-file suppression exists. Path exclusions apply to code not
maintained here: vendored and minified libraries, generated files. Everything with a legitimate
exception is review territory, not a gate.

### 2. One level: universal policy, per-stack mechanism

Every module of every stack is held to the same set. The tier registry, `@test-tier` annotations and
per-tier thresholds are removed. A stack that lacks a mechanism for a gate skips that gate and records
the gap in the project's testing guide; the gate is neither dropped elsewhere nor weakened to fit the
weakest stack.

dydo owns the rules, not the runners. Each project keeps its own `gap_check.py` and the small
producers that feed it, tuned to its stacks, built from the pattern this record and the Testing
Strategy describe. Setting the gate up, or opening the Issue that will, is a step of adopting dydo.

### 3. The gate set

| Gate | Threshold | C# | TypeScript | Python |
|---|---|---|---|---|
| Build invariants | warnings as errors, strict types | compiler and analyzers as configured | tsconfig strict | type checker as configured |
| Dead code | none: no unused locals, parameters, private members, exports or files | IDE0051, IDE0052, IDE0060 as errors | noUnusedLocals, noUnusedParameters, knip | ruff F401, F841, vulture |
| Tests | all pass; every non-trivial module has a test file | | | |
| Coverage floor | line ≥ 80%, branch ≥ 60% per module | Coverlet | Istanbul (LCOV) | coverage.py |
| HCRAP | `CC² × (1 − cov)³ + cognitive` ≤ 20 per method | gap_check, Roslyn walker | gap_check, TypeScript walker | gap_check, complexipy |
| Cognitive complexity | ≤ 20 per method, also at build time | Sonar S3776 as error | eslint-plugin-sonarjs cognitive-complexity | complexipy |
| Parameters | ≤ 7 per method or function; constructors excluded | gap_check, walker | gap_check, walker | gap_check, its own AST count |
| Nested ternary | none | Sonar S3358 | eslint no-nested-ternary | no mechanism; gap recorded |
| Duplication | no clone of at least 15 lines and 100 tokens | jscpd | jscpd | jscpd |
| Dependency cycles | none between namespaces or modules | NsDepCop or the architecture tests | dependency-cruiser | import-linter |

- **HCRAP**, hybrid CRAP, keeps CRAP's coverage term with cyclomatic complexity, because cyclomatic counts the
  paths coverage must reach, and replaces the floor with cognitive complexity, because the floor is
  what remains when tests are perfect and that is a readability question. At full coverage the score
  equals cognitive complexity, so its threshold is the cognitive threshold. A 21-case reducer at full
  coverage scores its cognitive value, 7. A six-deep nest at full coverage scores 21 and fails.
  Untested code is punished by the coverage term exactly as before. DR 009's per-method rule stands.
- **Cognitive complexity** follows SonarSource's definition: `switch` and `switch` expressions cost 1
  plus nesting and their cases are free; `if`, loops, `catch` and `?:` cost 1 plus nesting; `else`
  and `else if` cost a flat 1; a run of one boolean operator costs 1; lambdas and local functions
  raise nesting without cost. For TypeScript the mechanism must match SonarJS: each function is scored
  on its own control flow and reported separately, a React component is not charged for its
  callbacks, and `{a && b && <X/>}` chains inside JSX are exempt. A producer that charges components
  for their callbacks fails every React component downstream (121 functions instead of 31).
- **Threshold 20** was chosen after reading downstream code at 18 and 22. The 22 is a chain of
  thirteen guard clauses, nine at the top level and four inside one nested branch pair, plus a
  ternary and a catch; the gate's fix, extracting that pair, takes it to 12 and is a better shape.
  Denial by the gate changes shape; it does not make code worse. At 25 the gate would catch only
  code nobody defends; at 15 it catches code reviewers would pass.
- **Parameters** live in gap_check, fed by the C# and TypeScript walkers and by gap_check's own
  AST count for Python, because Sonar's S107 counts constructor parameters and cannot be told to
  skip them. Constructors are excluded because
  their count is dependency fan-in, and the cheapest fix is a facade that hides it.
- **Not gated**, by the selection rule: cyclomatic complexity on its own, method and file length,
  nesting depth (cognitive 20 already caps a chain at five levels), expression complexity,
  inheritance depth, class coupling, cohesion, maintainability index. They remain review lenses.

### 4. Mutation testing is the assurance layer, not a static gate

T3's intent, adversarial certainty on auth, crypto and billing paths, is carried by mutation score
and by the reviewer and inquisitor rubrics, not by a coverage tier. Mutation thresholds are set per
project as a separate gate; where they push coverage above the floor in practice, the system is
working.

### 5. The transition is the validation

When the new gates first run on an existing codebase, every failure is triaged against the selection
rule: code judged good as is means the gate or its threshold is wrong and is corrected; code judged
rightly caught is fixed. The triage is recorded per stack on the transition Project. The old tiers
passed code they never measured, so the failures the new gates surface are the true picture, not a
regression.

## Consequences

- `gap_check.py` in each project: one threshold set; HCRAP replaces classic CRAP; per-method
  cognitive complexity and parameter counts come from per-stack producers (a Roslyn walker of about
  110 lines, a TypeScript walker mirroring SonarJS of about 130 lines, complexipy JSON); the tier
  registry, the annotations and `TIER_THRESHOLDS` go.
- Build-time analyzers per stack: SonarAnalyzer.CSharp with S3776 and S3358 as errors and every
  other rule off; eslint-plugin-sonarjs cognitive-complexity and ESLint's own no-nested-ternary;
  complexipy with
  `--max-complexity-allowed 20`; jscpd in every gate run.
- The measured failure counts at adoption live on DYD-95, not here. Per stack and gate they range
  from zero to the mid twenties, and the largest single family is one duplicated worker skeleton.
- Downstream transition Project, first Issues in order: the coverage settings fix and re-baseline
  (DYD-95); the producers; the gate swap; failure triage per stack; jscpd; dependency cycles;
  mutation thresholds. Mutation testing for the former T3 paths in C# is transition debt, carried on
  DYD-95 until it lands.
- Rejected: keeping the tiers; classic CRAP at 15 (forces the reducer split); "fail only if both
  gates fail" (passes a fully covered six-deep nest and passes untested code); modified cyclomatic
  inside CRAP (under-penalises an untested switch); Sonar S107 for parameters (counts constructors);
  method and file length as gates (legitimate exceptions exist); shipping the producers with dydo
  (the rules are universal, the runners are per project).

## Supersedes and amends

- Supersedes the three-tier system in [Testing Strategy](../../guides/testing-strategy.md) and in
  [Coverage Tools](../../reference/coverage-tools.md).
- Keeps [DR 009](./009-crap-per-method-metric.md): per-method max complexity, now applied inside
  HCRAP.

## Affects

- [Testing Strategy](../../guides/testing-strategy.md): rewritten around the one level.
- [Coverage Tools](../../reference/coverage-tools.md): tier table, annotation and registry sections go.
- `Templates/coding-standards.template.md`: the shipped tier table and CRAP paragraph are replaced by
  the gate set, so downstream projects compile the new policy.
- `Templates/skill-hardener.template.md` and [Control Flow](../../understand/control-flow.md): the
  hardener measures HCRAP against the one threshold, not "against the tier".
- [Glossary](../../glossary.md): HCRAP is defined there.
- `DynaDocs.Tests/coverage/gap_check.py` and each downstream project's copy: as in Consequences.
- [Getting Started](../../guides/getting-started.md): setting up the gate for the project's stacks,
  or opening the Issue that will, becomes a step of adopting dydo.
- Linear DYD-95: the ark for the measurements and the transition debt until the downstream
  transition Project exists.
