---
area: general
type: changelog
date: 2026-04-02
---

# Task: docs-v13-template-npm-fix

Updated Templates/about-dynadocs.template.md and npm/README.md to match the already-fixed reference docs (dydo/reference/about-dynadocs.md and root README.md). Changes: removed old amnesia framing, replaced with structured memory/context problem framing; removed platform-agnostic claims and non-Claude Code sections; updated role table from 7 to 9 roles; removed workflow flags section; added new sections (Stop Doing Agent Work, Template Additions, Multi-Agent Orchestration, etc.). Removed SVG image references from template that don't exist in fresh inits. Updated ContainsWorkflowFlags test to ContainsInboxFlag to match new template content. All template and integration tests pass.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Models\ConditionalMustRead.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Models\ConditionalMustReadCondition.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Templates\about-dynadocs.template.md — Created
C:\Users\User\Desktop\Projects\DynaDocs\npm\README.md — Created
C:\Users\User\Desktop\Projects\DynaDocs\Models\RoleDefinition.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Serialization\DydoJsonContext.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\RoleDefinitionService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MustReadTracker.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\MustReadTrackerTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\RoleDefinitionServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-reviewer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\ConditionalMustReadTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\InitCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\InitCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TemplateGeneratorTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified


## Review Summary

Updated Templates/about-dynadocs.template.md and npm/README.md to match the already-fixed reference docs (dydo/reference/about-dynadocs.md and root README.md). Changes: removed old amnesia framing, replaced with structured memory/context problem framing; removed platform-agnostic claims and non-Claude Code sections; updated role table from 7 to 9 roles; removed workflow flags section; added new sections (Stop Doing Agent Work, Template Additions, Multi-Agent Orchestration, etc.). Removed SVG image references from template that don't exist in fresh inits. Updated ContainsWorkflowFlags test to ContainsInboxFlag to match new template content. All template and integration tests pass.

## Code Review

- Reviewed by: Frank
- Date: 2026-03-30 19:19
- Result: PASSED
- Notes: LGTM. Template matches reference doc (minus fresh-init-absent SVGs). npm README matches root README. Role table updated to 9 roles. Workflow flags replaced with --inbox. Test updated. All 3348 tests pass, coverage gate green.

Awaiting human approval.

## Approval

- Approved: 2026-04-02 18:55
