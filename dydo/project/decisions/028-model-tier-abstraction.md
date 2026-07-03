---
area: general
type: decision
status: accepted
date: 2026-07-03
participants: [balazs, Brian]
---

# 028 — Model Tiers: Roles Declare a Tier, the Compiler Binds the Model

Worker roles and workflow stages declare an abstract **model tier** (`strong` / `standard` / `light`) plus optionally an **effort** level — never a concrete model ID. A per-vendor mapping in `dydo.json` binds tiers to real models, and `dydo sync` resolves role → tier → model when emitting native artifacts (`.claude/agents/<role>.md` frontmatter carries the resolved model). Judgment-heavy work runs strong; well-defined implementation runs standard; mechanical sweeps run light. Everything is configurable in dydo; nothing is hardcoded.

## Context

Intelligence is priced per token, and the work agents do varies enormously in how much marginal intelligence is worth. Translating an already-designed plan into code against established patterns is the most compressible work in the system; co-thinking, planning, reviewing, and auditing are the least — highest variance, highest impact of a better model. Using the strongest available model (Fable 5) for monotonic implementation is overkill; using a weak model for judgment work is malpractice. The runtime already supports per-agent model selection at every level (Agent tool `model` param, workflow `agent()` `opts.model`/`opts.effort`, agent-definition frontmatter) — what's missing is dydo owning the policy so it stays vendor-portable and configurable, per the compiler role from [024](./024-dydo-2-native-pivot.md) §6 and the Codex-paving intent (roles must not name vendor-specific models).

## Decision

### 1. Tier abstraction
Three tiers, semantic not vendor-bound:
- **strong** — judgment work: reviewer, sprint-auditor, inquisitor, judge, planner (and all Tier-1 modes by default: co-thinker, orchestrator, chief-of-staff).
- **standard** — defined production work: code-writer, test-writer, docs-writer.
- **light** — mechanical work: scout fan-outs inside workflows, index/formatting sweeps, telemetry summarization for the board. Not code that ships.

### 2. Vendor mapping in `dydo.json`
```json
"models": {
  "tiers": {
    "anthropic": { "strong": "fable-5", "standard": "opus-4-8", "light": "haiku-4-5" }
  },
  "roles": {
    "code-writer": "standard", "test-writer": "standard", "docs-writer": "standard",
    "reviewer": "strong", "sprint-auditor": "strong", "inquisitor": "strong",
    "judge": "strong", "planner": "strong"
  }
}
```
A future `openai` key maps the same tiers for Codex targets — the roles section never changes per vendor. Unmapped role → inherit the session model (no silent downgrades).

### 3. The compiler binds, workflows stay tier-blind
`dydo sync` resolves role → tier → concrete model and writes it into the generated `.claude/agents/<role>.md` frontmatter. Workflows just say `agentType: 'code-writer'` and inherit — no model strings in workflow JS. Workflow-local overrides (`opts.model`/`opts.effort`) remain legal for stage-specific needs (e.g. a light scout stage inside an inquisition), but the default flows from the agent definition.

### 4. Effort is a second dial
Reasoning effort (`low`…`max`) composes with tier: same model, cheaper thinking for mechanical stages. Role definitions may declare a default effort alongside the tier; workflow stages may override per call. Tier picks the brain, effort picks how hard it thinks.

### 5. The review asymmetry (policy, not just default)
**The reviewer stays ≥ the writer's tier, always.** Verification is higher-leverage per token than generation: a strict strong reviewer catches what a standard writer misses, and the loop converges. Tiering down the reviewer to match a tiered-down writer is the one configuration this decision explicitly rejects.

### 6. Empirical, not guessed
`run-sprint` already returns rounds-per-slice. That is the tier-pricing metric: if standard-tier slices average materially more review rounds than strong-tier history, the tier is mispriced (each failed round costs a review + a re-code — savings evaporate). Revisit assignments on data; consider surfacing per-role round averages on the Notion board.

## Consequences

- `dydo.json` schema gains the `models` section (defaults shipped in the template; absent section = everything inherits session model, current behavior).
- `dydo sync` gains tier resolution + frontmatter emission; generated agent defs carry `model:` (and optionally effort).
- Docs: configuration reference documents the tiers; work-model/glossary mentions which roles run which tier.
- Vendor-portability acceptance test: adding a hypothetical second vendor mapping requires zero changes to role definitions or workflows.

## Revisit When

- Rounds-per-slice data shows standard-tier writers thrashing (tier code-writer up, or make tier assignment per-slice-complexity).
- A vendor's model lineup shifts (mapping edit only — that being true is the point of this decision).
