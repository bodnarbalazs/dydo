---
title: init --join does not record the integration; config.Integrations is write-only
id: 300
area: backend
type: issue
severity: medium
status: resolved
found-by: manual
date: 2026-07-24
resolved-date: 2026-07-26
---

# init --join does not record the integration; config.Integrations is write-only

ExecuteJoin wires hooks but never sets integrations.<name>=true in dydo.json (Pokercept ended with claude only after a codex join), and nothing in the codebase ever reads config.Integrations.

## Description

Two halves: (1) InitCommand.ExecuteJoin never updates or saves dydo.json, so a project inited with claude then joined with codex records only {"claude": true}. (2) The Integrations dictionary is written by init and referenced by tests, but no runtime code consumes it — dydo sync emits Claude and Codex outputs unconditionally. Either sync should gate its outputs on the recorded integrations (emit-all when the dict is empty, for back-compat) or the field should be dropped; and join must record the integration either way.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Resolved in v2.2.3. `init --join` now records newly wired integrations, `dydo sync`
consumes that state, and legacy configs that record neither Claude nor Codex retain the
old emit-both behavior. Regression coverage pins Claude-only, Codex-only, and legacy emission.
