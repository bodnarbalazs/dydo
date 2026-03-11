---
area: general
name: fix-template-generation
status: human-reviewed
created: 2026-03-11T16:58:19.3096740Z
assigned: Charlie
---

# Task: fix-template-generation

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed hardcoded template/mode lists missing 3 roles (orchestrator, inquisitor, judge). (1) TemplateGenerator.GetModeNames() now derives from RoleDefinitionService.GetBaseRoleDefinitions(). (2) TemplateGenerator.GetAllTemplateNames() likewise derives from role definitions. (3) TemplateCommand.FrameworkTemplateFiles derives from GetAllTemplateNames(). (4) FolderScaffolder adds project/inquisitions to scaffold spec. Updated 2 tests to be data-driven instead of hardcoded counts. All 1951 tests pass.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-11 17:15
- Result: PASSED
- Notes: LGTM. Clean refactoring: all hardcoded mode/template lists now derive from RoleDefinitionService.GetBaseRoleDefinitions(). Tests properly converted to data-driven [MemberData]. All 101 template-related tests pass. No unnecessary complexity, no slop.

Awaiting human approval.