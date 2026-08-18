---
title: Anthropic Standard Tier Refresh
seq: 17
status: done
gate-result: PASS
area: platform
type: context
---

# Anthropic Standard Tier Refresh

Update Anthropic standard-tier worker bindings in both shipped defaults and generated artifacts.

## 1. Specification

**Intent** — Move Anthropic standard-tier worker agents from the currently shipped identifier to
the requested next-generation identifier. Keep the repository's own configuration and compiled
Claude agent artifacts aligned with the default shipped to newly initialized projects.

**In scope**

- Change the Anthropic `standard` tier in `ConfigFactory.CreateDefaultModels()`.
- Update the repository's `dydo.json` Anthropic `standard` tier.
- Recompile the three standard-tier Claude worker agent definitions.
- Update focused model-cap tests whose expectations or representative fallback use that standard
  tier identifier.

**Out of scope**

- Anthropic `strong`, `light`, or fallback bindings.
- OpenAI bindings and generated Codex agents.
- Historical issue, inquisition, audit, decision, or backlog records that accurately describe the
  model used or requested at the time.
- Adding a migration that rewrites existing downstream projects' customized model tiers.

**Acceptance criteria**

1. Newly initialized projects bind Anthropic `standard` to `claude-opus-5`.
2. This repository binds Anthropic `standard` to the same identifier.
3. Generated Claude definitions for `code-writer`, `test-writer`, and `docs-writer` use that
   identifier, while strong-tier definitions remain unchanged.
4. Active source, configuration, generated-agent, and test code contains no stale
   `claude-opus-4-8` reference; historical Records are unchanged.
5. Focused tests, the full isolated test suite, coverage gate, Record checks, and scoped diff checks
   pass.

**Questions & answers**

- Does this change only the local generated agents? No. The user explicitly included definitions
  shipped to downstream projects, so `ConfigFactory` is the authoritative source of truth.
- Should existing downstream projects be silently migrated? No. Model tiers are user-configurable;
  this request changes shipped defaults and this repository's own binding only.
- Should historical provenance be rewritten? No. Those records describe past execution and must
  remain factually accurate.
- What exact requested identifier is used? `claude-opus-5`, following the repository's existing
  versioned Anthropic alias convention.

## 2. Prior art

- `Services/ConfigFactory.cs` owns shipped model-tier defaults used by `dydo init`.
- `dydo.json` binds this repository's model tiers.
- `Commands/SyncCommand.cs` resolves role → tier → vendor identifier and emits Claude agent
  frontmatter; generated worker definitions live under `.claude/agents/`.
- `DynaDocs.Tests/Services/ModelCapServiceTests.cs` asserts that capping the strong tier leaves the
  standard tier untouched and uses the standard model as an explicit fallback in two scenarios.
- Decision 028 makes vendor lineup changes mapping edits rather than role or workflow edits.

Rejected alternatives: editing only `.claude/agents/` would be overwritten by `dydo sync`, while
changing only `ConfigFactory` would leave this repository's committed configuration and generated
artifacts stale.

## 3. Design

Replace the Anthropic standard-tier value in the shipped default and repository configuration, then
regenerate or make the equivalent surgical update to the three standard-tier Claude agents. Update
the model-cap test values that represent the standard tier. Do not touch roles, tier semantics,
resolution code, migration behavior, or historical Records.

The shared `dydo.json` is already dirty with unrelated user changes. Preserve every existing byte
outside the single standard-tier value. Rollback consists only of restoring the five owned
implementation/configuration paths and their focused test expectations.

## 4. Slice map

| # | slice file | files touched (disjoint) | deps | gate |
|---|---|---|---|---|
| 1 | `anthropic-standard-tier-refresh-1-refresh.md` | `Services/ConfigFactory.cs`, `DynaDocs.Tests/Services/ModelCapServiceTests.cs`, `dydo.json`, `.claude/agents/code-writer.md`, `.claude/agents/docs-writer.md`, `.claude/agents/test-writer.md` | — | focused tests; isolated full suite; coverage; Record checks; scoped diff |

## 5. Ordering & isolation

Run one serial in-tree lane. The lane owns only the six paths in the Slice map; `dydo.json` is a
shared hot file and must be patched surgically so unrelated working-tree edits survive unchanged.
The Sprint and Slice Records are planning artifacts, not implementation scope.

## 6. Watch-outs

- Do not edit generated Codex agents; the OpenAI tier map is unchanged.
- Do not use a broad replacement across historical Records.
- Do not add a legacy-tier migration; customized downstream configurations remain user-owned.
- Verify that strong-tier Claude agents retain their current model binding.
- Do not normalize or rewrite the rest of the already-dirty `dydo.json`.

## Plan review

**PASS** (2026-08-18, fresh-eyes review).

The Slice's active-scope zero-match gate closes the stale-reference gap. The refresh proved the
old default red, then passed the focused model-cap suite (28/28), existing coverage inspection,
build, Record checks, and scoped diff validation without changing strong, light, or OpenAI tiers.
