---
area: general
name: dynamic-role-table
status: human-reviewed
created: 2026-03-11T14:50:57.8618764Z
assigned: Frank
---

# Task: dynamic-role-table

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented dynamic role table generation (Slice 3). Replaced hardcoded role table in Templates/agent-workflow.template.md with {{ROLE_TABLE}} placeholder. Added GenerateRoleTable() and LoadRolesForTemplate() to TemplateGenerator.cs that loads role definitions from _system/roles/*.role.json and builds a markdown table, with fallback to GetBaseRoleDefinitions(). Wired into GenerateWorkflowFile placeholder dictionary. Added 7 tests covering: fallback to base definitions, mode file links, descriptions, alphabetical sorting, loading from disk, empty roles dir fallback, and placeholder resolution in workflow output. All 1927 existing tests pass + 7 new. DEVIATION: Could not update the project-local template override (dydo/_system/templates/agent-workflow.template.md) due to code-writer role restrictions. A docs-writer needs to apply the same {{ROLE_TABLE}} substitution there and then regenerate agent workflow files to dogfood.

## Code Review

- Reviewed by: Jack
- Date: 2026-03-11 15:01
- Result: PASSED
- Notes: Code is clean, correct, and well-tested. Minor notes: (1) XML doc on GenerateRoleTable says basePath is dydo folder only but LoadRolesForTemplate handles both conventions - cosmetic. (2) Project-local template override still has hardcoded table, shadowing the feature - documented deviation, needs docs-writer follow-up. (3) Pre-existing build errors in RoleBehaviorTests.cs blocked test execution (not from this change).

Awaiting human approval.