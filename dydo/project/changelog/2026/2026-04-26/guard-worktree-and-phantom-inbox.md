---
area: platform
type: changelog
date: 2026-04-26
---

# Task: guard-worktree-and-phantom-inbox

Fix #99 (HandleSearchTool worktree-allow), #100 (unanchored substring match), #101 (guard-system.md docs), plus finish Charlie's orphaned phantom-unread-inbox self-heal fix.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review lane `guard-worktree-and-phantom-inbox` — four-part bundle on master (no worktree). Brian's full brief at `dydo/agents/Brian/brief-guard-worktree-and-phantom-inbox.md`.

## What shipped

1. **#99 HandleSearchTool worktree-allow JSON** (`Commands/GuardCommand.cs`). Added `EmitWorktreeAllowIfNeeded()` call before the final `ExitCodes.Success` return in `HandleSearchTool`. Glob/Grep/Agent calls now emit the same Claude-Code auto-approve JSON as the other four sister handlers inside a worktree.

2. **#100 Anchor IsWorktreeContext** (`Commands/GuardCommand.cs`). Replaced the unanchored `cwd.Contains("dydo/_system/.local/worktrees/")` substring check with an exact path-segment match on the sequence `[dydo, _system, .local, worktrees]`. Sibling directories like `worktrees-notes/` or `worktrees.backup/` no longer get misidentified as worktree contexts. Old `WorktreePathMarker` const removed; new `WorktreePathSegments` array in its place.

3. **Charlie's phantom-unread-inbox self-heal** (`Commands/GuardCommand.cs:NotifyUnreadMessages`). Before the existing early-exit, iterate `agent.UnreadMessages`, drop any id whose inbox file is missing via `registry.MarkMessageRead`, then reload agent state so the block decision runs against the healed list. Reconstructed from Brian's brief (plan was lost in the 19-workspace wipe).

4. **#101 doc section** (`dydo/understand/guard-system.md`). Dispatched to Grace (docs-writer) on sub-task `guard-worktree-and-phantom-inbox-doc`. Grace landed the "Auto-Approve JSON (Worktree-Only)" section ahead of "## Guard Lift" and resolved issue 101. Please confirm the section matches the contract in code (gate location, five emitting handlers, never-emit-on-block, security-posture-unchanged).

## Tests

- **Extended `DynaDocs.Tests/Integration/GuardWorktreeAllowTests.cs`**:
  - `WorktreeGlob_Approved_OutputsAllowJson` (#99 positive)
  - `WorktreeGrep_Approved_OutputsAllowJson` (#99 positive)
  - `NonWorktreeGlob_Approved_StdoutEmpty` (#99 negative)
  - `IsWorktreeContext_UnanchoredSubstringMatch_ReturnsFalse` (#100)
  - `IsWorktreeContext_SiblingWorktreesBackup_ReturnsFalse` (#100)
- **Charlie's three `DynaDocs.Tests/Commands/PhantomUnreadInboxTests.cs`** went red→green with the self-heal landing: `PhantomIdsDropped_DoesNotBlock`, `MixedRealAndPhantom_BlocksOnRealOnly`, `AllRealIds_StillBlocks`.
- `python DynaDocs.Tests/coverage/run_tests.py` for the targeted filter: **32 passed, 0 failed**.
- `python DynaDocs.Tests/coverage/gap_check.py --force-run`: **136/136 modules pass tier requirements, 0 failing, all tests passed on the clean run.**

## Plan deviations

- Brian's brief listed `DynaDocs.Tests/Commands/GuardCommandTests.cs (extend)` for new tests, but the canonical allow-JSON test collection lives in `DynaDocs.Tests/Integration/GuardWorktreeAllowTests.cs` alongside the sister `WorktreeRead/Write/Bash_Approved_OutputsAllowJson` tests. I added the new tests there for consistency. Not on Brian's DO-NOT-touch list.
- One coordination hop: hit a pre-existing build break in `DynaDocs.Tests/Integration/ReviewerVerdictRoutingTests.cs:219` (CS9006 — untracked file from the reviewer-verdict-routing lane, also hit by Adele). Brian authorized the shared one-char unblock; Adele landed it and I rebased onto her version, no conflict.
- The doc half (#101) went through Grace (docs-writer) because code-writer role can't edit `dydo/understand/**`. Grace handled the doc section, the issue resolve, and messaged me "done" so I could dispatch this reviewer on the combined lane.

## Key notes for future agents

- **Auto-approve JSON shape** (the thing #99 is about): `{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow"}}`. `allow` skips Claude Code's prompt; `ask`/`deny`/`defer` are the other legal values. Exit 0 with empty stdout does NOT auto-approve — Claude Code falls back to its settings.local.json allow-list, then prompt. Gate is `IsWorktreeContext()` — CWD must be inside `dydo/_system/.local/worktrees/{id}/…` (now anchored to exact path segments).
- Five handlers emit on success: `HandleReadOperation`, `HandleWriteOperation` (both lifted + RBAC-pass branches), `HandleSearchTool` (new), `HandleDydoBashCommand`, `AnalyzeAndCheckBashOperations`. Blocked paths never emit.

## Issues

- #99, #100, #101 all resolved via `dydo issue resolve` and now live in `dydo/project/issues/resolved/`.

## Baton

Per mode instructions, this reviewer dispatch fulfills my reply obligation to Brian for the `guard-worktree-and-phantom-inbox` lane — please relay the review outcome to Brian on my behalf.

## Code Review

- Reviewed by: Grace
- Date: 2026-04-20 18:35
- Result: PASSED
- Notes: LGTM. #99 HandleSearchTool allow-JSON emit correctly placed before final ExitCodes.Success, mirrors sister handlers, blocked paths never emit. #100 IsWorktreeContext segment-anchored with i+len<N bound enforcing required worktree id after marker; case-insensitive; sibling dirs (worktrees-notes, worktrees.backup) now correctly return false. Phantom-unread self-heal drops missing-file ids via MarkMessageRead then reloads agent state before the Count==0 early-exit; safe fallback if reload fails. #101 doc: JSON shape matches code constant, cites gate, lists five emitters, notes never-emit-on-block, security posture accurate. Tests comprehensive: 5 new in GuardWorktreeAllowTests.cs (positive+negative), 3 phantom tests (pure phantom, mixed, all-real regression). gap_check --force-run: 3776 passed / 0 failed / 136 modules pass tier.

Awaiting human approval.

## Approval

- Approved: 2026-04-26 19:39
