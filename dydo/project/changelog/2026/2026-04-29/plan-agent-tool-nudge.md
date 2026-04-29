---
area: general
type: changelog
date: 2026-04-29
---

# Task: plan-agent-tool-nudge

Plan a Stage-2 soft-nudge for Claude Code's built-in 'Agent' tool. Brian's prior investigation + planning work was deleted by 'agent clean --force', so this is a fresh dispatch but the verified findings are summarized below.

## Verified context (no need to re-investigate)

- Agent IS guarded today via Stage-2 lockout (Commands/GuardCommand.cs:69 SearchTools → HandleSearchTool :217-220, body :417-457). NOT a regression.
- Hook matcher includes Agent: Commands/InitCommand.cs:355 and .claude/settings.local.json:28. Matcher: Edit|Write|Read|Bash|Glob|Grep|Agent|EnterPlanMode|ExitPlanMode.
- Models/ToolInputData.cs does NOT deserialize Agent-specific fields (subagent_type, description, prompt). Currently treats Agent like Glob/Grep with null path.
- Configurable nudge engine (CheckNudges in GuardCommand.cs:514-574) is bash-only — invoked only from HandleBashCommand. Cannot match against Agent without extension.
- Decision 009 already documents the philosophical stance (Claude subagents stateless ≠ dydo stateful agents). Never wired to runtime.

## Your job

1. Read these in full first: dydo/agents/Adele/notes-regression-verify.md (orchestrator's full context), Commands/GuardCommand.cs (especially HandleSearchTool, S1/S2/N1 hardcoded patterns, RequireIdentityAndRole), Models/ToolInputData.cs, DynaDocs.Tests/Integration/GuardCommandTests.cs (or equivalent — find the existing test patterns for HandleSearchTool).
2. Answer the kicker: CAN we cleanly detect heavyweight subagent_type vs lightweight (Explore, claude-code-guide)? Cost = extending ToolInputData + small allowlist. Robust enough? Subagent_type is free-form string — what about user-defined ones?
3. Recommend ONE: A) always-nudge (simplest, no ToolInputData change); B) differentiated (heavyweight=warn, lightweight=quiet, requires ToolInputData extension); C) DITCH if cost not worth it.
4. If A or B: detailed plan — exact file:line edits, exact test cases mirroring existing S1/S2 nudge tests, estimated added LOC, risks + mitigations. Use ONLY existing patterns (S1/S2 marker pattern, existing test harness). No new abstractions.

## Hard rules
- Reuse existing patterns. No new helper classes.
- Minimize LOC. User explicitly does not want code slop.
- Tests must be extensive. Specify cases: happy path + edge cases (missing subagent_type, empty input, marker exists/missing).
- Mirror the structure of Dexter's plan at dydo/agents/Dexter/plan-orchestrator-nowait-nudge.md (good template for what a clean plan looks like in this repo).

## Deliverable
- Plan at dydo/agents/<you>/plan-agent-tool-nudge.md
- msg back to Adele with subject 'plan-agent-tool-nudge': recommendation (A/B/C), estimated LOC, test count, key risks, decision rationale.
- Then release.

Do NOT write code. Plan only.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

(Pending)

## Approval

- Approved: 2026-04-29 12:04
