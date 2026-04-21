---
id: 101
area: understand
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-18
resolved-date: 2026-04-20
---

# Worktree-only auto-approve gating is undocumented in guard-system.md

## Description

The guard emits a Claude-Code-specific JSON envelope —
`{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow"}}` — to skip the per-tool permission prompt, but only when CWD lies inside a dispatch worktree (`Commands/GuardCommand.cs:73-92`). This conditional auto-approve has user-visible behaviour: dispatched agents inside worktrees are silent, while sessions started at the project root see prompts.

Searched user-facing docs:
- `dydo/understand/guard-system.md` — no mention of `permissionDecision`, the worktree gating, or the auto-approve emission.
- `dydo/reference/guardrails.md` — enumerates blocking guardrails but not the allow envelope or its scope.
- `dydo/understand/architecture.md` — describes worktree dispatch lifecycle but not the prompt behaviour difference.
- `dydo/understand/worktree-system.md` — does not exist; worktree material lives in `architecture.md` and changelog entries only.

The only trace is `dydo/project/changelog/2026/2026-04-09/fix-guard-worktree-allow.md`, which describes the fix, not the contract.

Effect: users (and inquiring agents) discover the behaviour by code-diving and are surprised when a project-root session prompts for the same operation a worktree session would not.

Filed by inquisition `auto-accept-edits-behavior` (2026-04-18 — Frank), finding 3.

## Reproduction

Read `dydo/understand/guard-system.md` looking for an explanation of why Claude Code does not prompt inside worktrees but does at the project root. The document does not describe the mechanism.

## Resolution

Added 'Auto-Approve JSON (Worktree-Only)' section to dydo/understand/guard-system.md documenting the IsWorktreeContext + EmitWorktreeAllowIfNeeded gate, why it exists, and user-visible behavior at worktree vs project root.