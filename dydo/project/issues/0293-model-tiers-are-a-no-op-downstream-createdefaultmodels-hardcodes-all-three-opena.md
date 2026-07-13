---
title: Model tiers are a no-op downstream - CreateDefaultModels hardcodes all three OpenAI tiers to gpt-5.5
id: 293
area: backend
type: issue
severity: high
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-13
---

# Model tiers are a no-op downstream - CreateDefaultModels hardcodes all three OpenAI tiers to gpt-5.5

The shipped default config maps strong/standard/light all to gpt-5.5; the real sol/terra/luna mapping lives only in this repo's dydo.json and was never promoted, so every downstream project runs every role on one model.

## Description

## Observed

A downstream project (`dydo init`-scaffolded) dispatches codex agents on **`gpt-5.5`** for every role, regardless of the role's declared tier. The `strong` / `standard` / `light` tier system — the whole point of Decision 028's model-tier indirection — is a **no-op** for every project except this repo.

## Root cause

`Services/ConfigFactory.cs:146-148`, `CreateDefaultModels()`:

```csharp
["strong"]   = "gpt-5.5",
["standard"] = "gpt-5.5",
["light"]    = "gpt-5.5"
```

All three OpenAI tiers are hardcoded to the same model in the **shipped defaults**. The real tier mapping (`strong: gpt-5.6-sol`, `standard: gpt-5.6-terra`, `light: gpt-5.6-luna`) exists ONLY in this repo's own `dydo.json` (lines 115-117) — a local edit that was never promoted into the default config. So a downstream project inherits three names for one model.

Supporting: `Services/TerminalLauncher.cs:11` `DefaultCodexModel = "gpt-5.5"`, and `Commands/SyncCommand.cs:233` writes `model = "gpt-5.5"` into generated codex agent TOMLs when no tier binding resolves.

Also stale: the display-name table at `ConfigFactory.cs:34-35` maps `gpt-5-codex` -> `"Gpt-5.6 Sol"` (a bogus alias) and has NO entries for the real `gpt-5.6-sol` / `-terra` / `-luna` ids, so `dydo model status` shows raw ids for them.

## Impact

Every downstream project silently runs every role on one mid-tier model: reviewers/planners never get the strong model, cheap roles never get the light one. Cost and quality are both wrong, invisibly. Confirmed live in the user's main project.

## Fix

- Promote the real tier mapping into `CreateDefaultModels()` so it ships by default.
- Fix the display-name table (add the three real ids; drop/repair the `gpt-5-codex` alias).
- **Determine and handle the upgrade path**: does `dydo init` WRITE the `models` section into `dydo.json`, or does `ConfigFactory` supply it at runtime for an absent section? If it is written into the file, changing the default fixes only NEW projects — EXISTING projects (e.g. the user's main project) keep the stale block and need a documented migration or a `dydo template update` path. This must be answered, not assumed.
- Reconcile the `gpt-5.5` fallbacks (`TerminalLauncher.DefaultCodexModel`, `SyncCommand`'s TOML default) with the new defaults, updating the tests that pin them.

## Acceptance

- A fresh `dydo init` yields `strong: gpt-5.6-sol`, `standard: gpt-5.6-terra`, `light: gpt-5.6-luna`.
- `dydo sync` emits the tier-correct model into each generated codex agent TOML (reviewer/planner -> sol, code-writer/docs-writer -> terra).
- `dydo model status` shows real display names for all three.
- The upgrade path for an existing project is documented and verified.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)