---
title: dispatch does not validate --role against defined roles (accepted 'planner', roster showed co-thinker)
id: 240
area: backend
type: issue
severity: medium
status: open
found-by: manual
found-by-agent: Mia
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-08
---

# dispatch does not validate --role against defined roles (accepted 'planner', roster showed co-thinker)

dydo dispatch accepts an undefined --role string without validation; the target agent silently lands on a different role instead of the dispatch failing fast.

## Description

## Observed

`dydo dispatch --to Olivia --role planner ...` was ACCEPTED (2026-07-08), even though `planner` is not a defined role — it is not one of the 7 base roles (`dydo roles list`) and no custom `.role.json` defines it. The agent roster subsequently showed Olivia's role as `co-thinker`.

## Defect

Dispatch does not validate the `--role` string against defined roles (base + custom `.role.json` under `dydo/_system/roles/`). An undefined role is silently accepted at dispatch time and the target agent ends up with some other role (fallback or stale), so the dispatcher's intent is silently rewritten instead of failing fast.

## Expected

Dispatch with an undefined `--role` should fail fast with an actionable message listing the defined roles (same fail-fast principle as the DR 037 §6 vendor-override hardening, issue 0239).

## Related

- Issue 0237 — dispatch role validation reads the *dispatcher's* role as "unknown"; same validation path likely at fault for both sides (caller role and target role). Consider fixing together.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)