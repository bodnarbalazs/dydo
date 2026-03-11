---
area: general
name: fix-project-sync
status: human-reviewed
created: 2026-03-11T17:40:02.1969180Z
assigned: Dexter
---

# Task: fix-project-sync

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Completed all 4 fixes: (1) Synced dydo-commands template with Role Commands section, updated roles list, updated Role Permissions table. (2) Made TemplateGenerator.GetAllTemplateNames() and GetModeNames() dynamic from RoleDefinitionService.GetBaseRoleDefinitions(). (3) TemplateCommand.ExecuteUpdate() now auto-cleans stale templates, prunes hashes, and regenerates agent workspaces. (4) All 26 agents regenerated with 9 mode files. 122 targeted tests pass.

## Code Review (2026-03-11 19:30)

- Reviewed by: Jack
- Result: FAILED
- Issues: The 4 stated items (template sync, dynamic names, stale cleanup, agent regeneration) are correctly implemented. 148 tests pass, build is clean. FAIL reason: bundled refactoring introduced 2 behavior regressions: (1) CleanAll no longer lists which agents are blocking (was: name/status/task per agent, now: just count), (2) wait marker audit reporting silently dropped from CleanAgent and CleanAll.

Requires rework.

## Code Review

- Reviewed by: Jack
- Date: 2026-03-11 19:52
- Result: PASSED
- Notes: LGTM. Both regressions fixed: (1) CleanAll properly enumerates working agents with name/status/task, (2) wait marker audit restored in CleanAgent and CleanAll with all three helpers. Extraction from CleanCommand to WorkspaceCleaner is faithful — all behavior preserved. 7/7 tests pass, 2 pre-existing FixCommand failures unrelated.

Awaiting human approval.