---
area: platform
type: changelog
date: 2026-04-20
---

# Task: worktree-merge-safety-ignore

Add configurable ignore list (dydo.json worktree.mergeSafety.ignore) to pre-merge safety check, with junk/suspicious classification and improved error messaging

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review worktree merge-safety ignore implementation. New: Models/WorktreeConfig.cs + MergeSafetyConfig.cs (with DydoConfig.Worktree property, registered in DydoJsonContext). New: Services/WorktreeMergeSafety.cs pure classifier using GlobMatcher for ignore globs + category assignment (TaskFile/Source/Test/Other). Commands/WorktreeCommand.cs: CheckMergeSafety now takes DydoConfig, emits new error with suspicious listing, task-file annotation, targeted 'git add -- <paths>' recipe, junk summary. Added dydo worktree status [--all] subcommand. ConfigFactory.DefaultNudges updated --force nudge message. Tests: 17 classifier unit tests, 8 integration (OnlyJunkFiles_ReturnsNull, SuspiciousAndJunk_BlocksWithAnnotation, TaskFileUncommitted_BlocksWithTaskAnnotation), 4 status subcommand tests, 2 config round-trip tests. CommandDocConsistencyTests: added 'worktree status' to ExcludedPaths for agent-facing quick-reference (consistent with other worktree subcommands). Pending docs-writer follow-up: (1) dydo.json in this repo needs worktree.mergeSafety block + nudge text sync (code-writer role can't edit dydo.json at repo root), (2) dydo/reference/configuration.md new section on worktree.mergeSafety, (3) dydo/reference/dydo-commands.md entry for worktree status. Plan deviations: error builder always emits recovery recipe (not gated on suspicious count) to preserve existing 0-commits-ahead UX per existing test Merge_BranchNotAdvanced_Blocks. gap_check has 5 pre-existing failures in AgentRegistryTests.*Stale* and PhantomUnreadInboxTests (stale-dispatch-double-claim and phantom-unread-inbox tasks being handled by other agents per original brief); my own tests all green.

## Code Review

- Reviewed by: Emma
- Date: 2026-04-18 19:07
- Result: PASSED
- Notes: LGTM. New classifier is clean (pure function, well-tested with 17 unit + 8 integration + 4 status + 2 config tests). CheckMergeSafety refactor preserves existing behavior while adding suspicious/junk breakdown + task-file annotation + targeted git add recipe. Coverage tier gate passes 136/136 modules. The 5 failing tests (AgentRegistryTests.*Stale*, PhantomUnreadInboxTests) are unrelated in-flight work on stale-dispatch-double-claim and phantom-unread-inbox — not on any code path touched by this task. Deferred items acknowledged in brief: dydo.json worktree.mergeSafety block + nudge text sync, dydo/reference/configuration.md section, dydo/reference/dydo-commands.md worktree status entry — all need docs-writer follow-up.

Awaiting human approval.

## Approval

- Approved: 2026-04-20 16:03
