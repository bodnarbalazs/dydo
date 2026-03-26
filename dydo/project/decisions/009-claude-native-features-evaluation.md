---
area: general
type: decision
status: accepted
date: 2026-03-25
participants: [balazs, Emma]
---

# 009 — Claude Code Native `.claude/` Features Evaluation

## Context

dydo integrates with Claude Code through three touchpoints: a PreToolUse hook in `.claude/settings.local.json`, a CLAUDE.md pointer to `dydo/index.md`, and terminal spawn via `claude 'AgentName --inbox'` with environment variables. Claude Code has expanded its native `.claude/` features significantly (rules, skills, native subagents, auto-memory, 20+ hook events, MCP servers, worktree isolation). We evaluated whether any of these should be adopted.

## Decision

**No changes.** dydo's current integration surface is sufficient. None of the native features warrant adoption.

## Evaluation Summary

| Native Feature | Verdict | Reason |
|---|---|---|
| `.claude/rules/` (path-conditional instructions) | Skip | Complements must-reads in theory, but adds maintenance overhead. Must-read enforcement is a feature, not a limitation. |
| `.claude/agents/` (native subagents) | Skip | Stateless in-process task runners. Fundamentally different from dydo's stateful agents with identity, lifecycle, inbox, messaging, dispatch, and audit. |
| `.claude/skills/` | Skip | Conflicts with staged enforcement. The guard wants agents to go through steps; skills would shortcircuit that. |
| Auto-memory | Skip | Risk of stale state contradicting dydo's docs-as-memory. Docs are deliberately curated; auto-memory is unstructured. |
| Additional hook events (SessionStart, SessionEnd, PreCompact, etc.) | Skip | SessionEnd for stale-agent cleanup was the strongest candidate, but the failure mode is infrequent and manual cleanup suffices. Implementation and edge-case cost exceeds benefit. |
| Native worktree isolation | Skip | Claude Code's worktrees are fire-and-forget: no merge, no review enforcement, no agent registry sharing. dydo's worktree system (junctions, init-settings, merge dispatch, review enforcement, coordinated cleanup) is significantly more capable. |
| Dynamic permission rewriting | Skip | Guard already handles fine-grained file-level permissions. Native allow/deny is coarser (tool-level). Two enforcement layers means two places to debug. |
| MCP servers | Skip | CLI interface is sufficient. Second interface doubles maintenance for no gain. |

## Key Insight

dydo's value is in the features Claude Code *doesn't* provide: agent identity and lifecycle, multi-agent orchestration (dispatch, inbox, messaging, wait), role-based file-level enforcement via the guard, staged onboarding, and audit trails. The native features that don't overlap with dydo solve different problems. The ones that partially overlap are weaker.

## Risks Noted

- **Auto-memory** could silently inject stale info that contradicts dydo docs. Not blocking, but worth awareness.
- **Future Claude Code changes** to the hook system could affect the guard. Low risk — the PreToolUse contract is stable.

## Revisit When

- Claude Code adds native features that complement (not duplicate) the guard/orchestration layer.
- A specific failure mode (e.g., context loss, stale agents) becomes frequent enough to justify hooking into additional events.
