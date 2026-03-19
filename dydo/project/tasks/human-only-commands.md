---
area: general
name: human-only-commands
status: human-reviewed
created: 2026-03-19T18:55:22.1350032Z
assigned: Dexter
---

# Task: human-only-commands

Add H28 guardrail: block agents from running human-only dydo commands (task approve, task reject, roles reset, guard lift, guard restore)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented H28: agents are blocked from running human-only dydo commands (task approve/reject, roles reset, guard lift/restore). Added regex, IsHumanOnlyDydoCommand method, and check in HandleDydoBashCommand between HandleClaimSessionStorage and IsDydoWaitCommand per plan. 14 integration tests added. Also fixed a variable scoping bug (eventType collision) from the parallel guard-lift task. Note: guardrails.md H28 entry needs a docs-writer dispatch. Build is broken by parallel task's GuardLiftCommand.cs (ExitCodes missing), not by this change.

## Code Review

- Reviewed by: Dexter
- Date: 2026-03-19 19:23
- Result: PASSED
- Notes: LGTM. Clean H28 implementation: source-generated regex, correct placement in HandleDydoBashCommand (after claim, before wait), 14 meaningful tests. gap_check exits non-zero but failures are pre-existing (CheckBashFileOperation 0% cov, RoleDefinitionService, RoleConstraintEvaluator, WatchdogService) — none caused by H28.

Awaiting human approval.