---
id: 132
area: backend
type: issue
severity: low
status: open
found-by: inquisition
date: 2026-04-28
---

# Watchdog CLI substring match has undocumented load-bearing invariant

## Description

**Mechanism.** `WatchdogService.PollAndCleanup` (Services/WatchdogService.cs:251) builds the kill pattern as `$"{agentName} --inbox"`. The trailing space + `--inbox` token is **load-bearing** for collision safety: it prevents prefix collisions like `Jack` matching `Jacky` (the space after `Jack` would have to be followed by space, but the cmdline has `y`).

If any future change alters the dispatch prompt format — e.g. adds a longer suffix, switches to `--no-inbox`, or removes the space — the implicit boundary disappears and cross-agent kill becomes possible. There is no comment at the construction site warning that the trailing token is collision-safety, and no test asserts it.

Cited by scout in Q1 of `dydo/agents/Brian/archive/20260428-150142/findings-watchdog-quality.md`.

**Suggested fix.** Either (a) add a comment at `WatchdogService.cs:251` documenting the invariant, or (b) replace substring match with a token-aware match (split on whitespace, look for exact `{agentName}` token followed by `--inbox` token). Token match would also harden against the prompt changing and against another future agent named `{agent}` with a similar prefix.

**Found by inquisition:** dydo/project/inquisitions/agent-deaths.md (Finding #6 — undocumented invariant).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)