---
id: 99
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-18
resolved-date: 2026-04-20
---

# HandleSearchTool never emits worktree allow JSON, prompting users for Glob/Grep/Agent inside worktrees

## Description

In `Commands/GuardCommand.cs`, four guard handlers call `EmitWorktreeAllowIfNeeded()` on their success paths so that Claude Code skips the permission prompt when the agent is operating inside a dispatch worktree:

- `HandleReadOperation` (line 373)
- `HandleWriteOperation` (lines 279 lifted, 304 RBAC-pass)
- `HandleDydoBashCommand` (line 639)
- `AnalyzeAndCheckBashOperations` (line 795)

`HandleSearchTool` (lines 400–437), which serves Glob, Grep, and the Agent sub-agent tool, returns `ExitCodes.Success` at line 436 without emitting. Result: even inside a worktree, Glob/Grep/Agent invocations fall back to Claude Code's normal permission flow and may surface a prompt that the other tools would not.

The same gap was flagged by Dexter on 2026-04-09 in the review of `fix-guard-worktree-allow` and explicitly deferred as out-of-scope/pre-existing. The companion gap noted in that review (AnalyzeAndCheckBashOperations) has since been closed; HandleSearchTool remains.

Filed by inquisition `auto-accept-edits-behavior` (2026-04-18 — Frank), finding 1.

## Reproduction

Inside a dispatch worktree, run a Glob or Grep tool call from a Stage-2 agent on a path not covered by `settings.local.json` `permissions.allow[]`. Claude Code prompts. The same operation in HandleReadOperation (a direct Read on the same path) does not prompt.

## Resolution

HandleSearchTool now emits the worktree-allow JSON on its success path (Commands/GuardCommand.cs), matching the four sister handlers. Glob/Grep/Agent calls inside a worktree no longer surface Claude Code's permission prompt. Covered by WorktreeGlob/Grep_Approved_OutputsAllowJson and NonWorktreeGlob_Approved_StdoutEmpty.