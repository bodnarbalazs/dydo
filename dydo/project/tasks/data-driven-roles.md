---
area: general
name: data-driven-roles
status: human-reviewed
created: 2026-03-10T19:01:46.8080151Z
assigned: Charlie
updated: 2026-03-10T22:20:16.5504511Z
---

# Task: data-driven-roles

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented Slice 1: RoleDefinition model + file-based loading (decision 008).

Created: RoleDefinition.cs, RoleConstraint.cs, IRoleDefinitionService.cs, RoleDefinitionService.cs, RolesCommand.cs.
Modified: AgentRegistry.cs (loads from role files with fallback), FolderScaffolder.cs (scaffolds role files during init), DydoJsonContext.cs (registered new types), PathsConfig.cs (added PathSets property), Program.cs (registered roles command), CommandSmokeTests.cs (added RolesCommand).
Tests: RoleDefinitionServiceTests.cs (20 tests), ConstraintEvaluationTests.cs (12 tests). All 115 behavioral tests pass unchanged. Full suite: 1900 pass, 2 pre-existing failures.

No plan deviations. Key decisions: GetRoleRestrictionMessage changed from static to instance method to support data-driven lookup. CanTakeRoleFallback is instance method (needs AgentNames for judge panel-limit).

## Code Review (2026-03-10 19:37)

- Reviewed by: Brian
- Result: FAILED
- Issues: Solid implementation overall. One issue: CommandSmokeTests second test (RootCommand_CanBeBuilt_WithAllSubcommands) missing RolesCommand.Create() — inconsistent with first test. Dispatched fix to Emma.

Requires rework.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-10 22:23
- Result: PASSED
- Notes: LGTM. All 32 new tests pass (20 RoleDefinitionService + 12 ConstraintEvaluation + 2 smoke tests). Full suite 1900 pass, 2 pre-existing failures unrelated. Emma's fix correct: RolesCommand.Create() in both smoke tests, assertion count >= 23 matches actual. Code is clean, minimal, follows standards. No security issues.

Awaiting human approval.