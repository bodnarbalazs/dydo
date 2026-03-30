---
type: decision
status: accepted
date: 2026-03-30
area: cli
---

# 014 — Rename `dydo clean` to `dydo agent clean`

## Context

`dydo clean` is a top-level CLI command that exclusively operates on agent workspaces (state files, session markers, inbox, dispatch markers, worktree markers). All related agent lifecycle commands (`claim`, `role`, `release`, `status`) already live under the `agent` subcommand.

The current placement is inconsistent and confusing — the human operator already reflexively types `dydo agent clean`.

## Decision

Move `clean` under the `agent` subcommand group as `dydo agent clean`. No alias for the old name. No deprecation period.

## Rationale

- The command is 100% agent-scoped — every code path goes through `AgentRegistry` and operates on agent workspaces
- No external users yet, so no backwards compatibility concern
- Aligns with the existing `agent *` command grouping

## Implementation Notes

- Move command registration from root to agent subcommand in `Program.cs`
- Update `CompletionProvider` to list `clean` under `agent` subcommands instead of top-level
- Update docs: command reference, troubleshooting guide, help text
- Update any templates that reference `dydo clean`
