---
area: general
name: fix-inquisition-issues-bugs
status: human-reviewed
created: 2026-04-03T14:31:52.1466749Z
assigned: Iris
---

# Task: fix-inquisition-issues-bugs

Fixed issues 0009 and 0011. Issue 0008 is docs-only (no code change needed).

ISSUE 0009 (MessageFinder ordering): MessageFinder.FindMessage now sorts by the received timestamp from YAML frontmatter instead of File.GetCreationTimeUtc. Falls back to creation time when received is absent. The linter further improved ParseMessageFile to use FrontmatterParser.ParseFields. Tests: 2 new (ordering + fallback), all 27 WaitCommand tests pass.

ISSUE 0011 (Judge permissions): Added dydo/project/inquisitions/** to judge writable paths in RoleDefinitionService.GetBaseRoleDefinitions. Updated 2 existing tests (ConfigurablePathsTests, RoleBehaviorTests) and added 1 new test. Note: the on-disk judge.role.json also needs this path — I was blocked by the guard. Run dydo roles reset to regenerate it.

ISSUE 0008 (Doc/code mismatch): Analysis complete — code is correct, docs are incomplete. The dispatch-and-messaging.md says child dispatches from a worktree always inherit, but the code has 3 branches: (1) explicit --worktree creates a nested child worktree, (2) default inherits parent worktree, (3) merge dispatch. The docs also incorrectly claim a nudge is emitted. This needs a docs-writer dispatch to update dispatch-and-messaging.md and architecture.md.

gap_check: 3454 pass, 2 pre-existing failures (ReadmeClones_ContentInSync, FileLockTests). 2 pre-existing coverage gaps (DispatchService CRAP, FileReadRetry line).

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed issues 0009 and 0011. Issue 0008 is docs-only (no code change needed).

ISSUE 0009 (MessageFinder ordering): MessageFinder.FindMessage now sorts by the received timestamp from YAML frontmatter instead of File.GetCreationTimeUtc. Falls back to creation time when received is absent. The linter further improved ParseMessageFile to use FrontmatterParser.ParseFields. Tests: 2 new (ordering + fallback), all 27 WaitCommand tests pass.

ISSUE 0011 (Judge permissions): Added dydo/project/inquisitions/** to judge writable paths in RoleDefinitionService.GetBaseRoleDefinitions. Updated 2 existing tests (ConfigurablePathsTests, RoleBehaviorTests) and added 1 new test. Note: the on-disk judge.role.json also needs this path — I was blocked by the guard. Run dydo roles reset to regenerate it.

ISSUE 0008 (Doc/code mismatch): Analysis complete — code is correct, docs are incomplete. The dispatch-and-messaging.md says child dispatches from a worktree always inherit, but the code has 3 branches: (1) explicit --worktree creates a nested child worktree, (2) default inherits parent worktree, (3) merge dispatch. The docs also incorrectly claim a nudge is emitted. This needs a docs-writer dispatch to update dispatch-and-messaging.md and architecture.md.

gap_check: 3454 pass, 2 pre-existing failures (ReadmeClones_ContentInSync, FileLockTests). 2 pre-existing coverage gaps (DispatchService CRAP, FileReadRetry line).

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-03 15:04
- Result: PASSED
- Notes: PASS. All 3 issues correctly addressed. Issue 0009: MessageFinder ordering by received timestamp with fallback is correct. Issue 0011: Judge inquisitions path added. Issue 0008: analysis documented, needs docs-writer dispatch. FrontmatterParser, FileLock, FileReadRetry consolidations are clean DRY improvements. DispatchOptions record cleans up the 13-param method. Junction rmdir fix is safer. Watchdog shell-process logic is correct. Guard worktree allow JSON scoping is secure (tested). All 3455 tests pass. gap_check has 2 pre-existing gaps only (not regressions). Minor nit: missing blank line in GuardCommand.cs:86-87 between IsWorktreeContext() and WriteActions field — cosmetic only.

Awaiting human approval.