---
area: general
name: fix-workflow-roles
status: human-reviewed
created: 2026-04-07T14:24:20.7744873Z
assigned: Emma
updated: 2026-04-07T17:43:34.6179683Z
---

# Task: fix-workflow-roles

Fixed 3 workflow/role issues: (1) Added 'inquisitor' to reviewer dispatch-restriction RequiredRoles in RoleDefinitionService.cs, breaking the circular deadlock where reviewers dispatched by inquisitors couldn't dispatch code-writers for merges. (2) Judge role source already has correct writable paths (issues + inquisitions); on-disk judge.role.json is stale — human must run 'dydo roles reset' to regenerate. (3) Added GenerateRoleFilesIfMissing to InitCommand.cs ScaffoldProject and PerformJoin, so 'dydo init' auto-generates role files when _system/roles/ is empty. All 7 new tests pass. 2 pre-existing failures in WorktreeCommandTests (Merge_CleanMerge_AutoFinalizes, Cleanup_WorktreeHold_CountsAsReference) are unrelated to these changes. gap_check passes (exit 0, all 135 modules pass).

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed 3 workflow/role issues: (1) Added 'inquisitor' to reviewer dispatch-restriction RequiredRoles in RoleDefinitionService.cs, breaking the circular deadlock where reviewers dispatched by inquisitors couldn't dispatch code-writers for merges. (2) Judge role source already has correct writable paths (issues + inquisitions); on-disk judge.role.json is stale — human must run 'dydo roles reset' to regenerate. (3) Added GenerateRoleFilesIfMissing to InitCommand.cs ScaffoldProject and PerformJoin, so 'dydo init' auto-generates role files when _system/roles/ is empty. All 7 new tests pass. 2 pre-existing failures in WorktreeCommandTests (Merge_CleanMerge_AutoFinalizes, Cleanup_WorktreeHold_CountsAsReference) are unrelated to these changes. gap_check passes (exit 0, all 135 modules pass).

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-07 17:56
- Result: PASSED
- Notes: LGTM. All 3 fixes are clean and correct: (1) inquisitor added to reviewer dispatch RequiredRoles — breaks the deadlock, (2) judge writable paths verified in source with round-trip test, (3) GenerateRoleFilesIfMissing is minimal and well-placed. 7 new tests are meaningful. 3477 tests pass, gap_check 135/135 green.

Awaiting human approval.