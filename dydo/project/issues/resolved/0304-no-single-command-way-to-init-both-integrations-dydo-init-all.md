---
title: No single-command way to init both integrations: dydo init all
id: 304
area: backend
type: issue
severity: low
status: resolved
found-by: manual
date: 2026-07-24
resolved-date: 2026-07-26
---

# No single-command way to init both integrations: dydo init all

dydo init claude; dydo init codex fails on the second command (already initialized) and the error only suggests --join phrased as joining as a team member; there is no all option.

## Description

dydo init takes exactly one integration and refuses when dydo.json exists, so setting up both Claude and Codex requires knowing the init X then init Y --join sequence. Add an 'all' integration value to init and join that wires both Claude and Codex in one run, and reword the already-initialized error to mention adding a second integration.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Resolved in v2.2.3. `dydo init all` and `dydo init all --join` now wire and record both
Claude Code and Codex, with help, completion, documentation, and integration coverage.
