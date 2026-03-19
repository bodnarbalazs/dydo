---
area: general
type: changelog
date: 2026-03-19
---

# Task: docs-batch-d

Fill in remaining reference placeholders and update outdated pages per the docs-upgrade-sprint plan.

## Progress

- [x] `reference/configuration.md` — Filled with dydo.json schema, env vars, hook config, customization points
- [x] `reference/audit-system.md` — Filled with audit format, events, compaction, commands, use cases
- [x] `reference/about-dynadocs.md` — Fixed role table (removed interviewer, added orchestrator/inquisitor/judge), updated workflow example, added messaging/issues/roles commands
- [x] `understand/architecture.md` — Added guard system, role system, dispatch/messaging, audit trail sections
- [x] `glossary.md` — Added 16 real project terms (agent, audit trail, claim, dispatch, guard, etc.)
- [x] `welcome.md` — Replaced boilerplate with DyDo-specific content and descriptions
- [x] `understand/about.md` — Expanded with project description, what DyDo does, tech stack
- [x] `project/v1.3-release.md` — Updated all slice statuses (A-H mostly complete)
- [x] `project/future-features/coverage.py-update.md` — Fixed missing title
- [x] `reference/dydo-commands.md` — Added missing inquisition coverage command
- [x] `project/docs-upgrade-sprint.md` — Marked all batch D items as done

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Services\IFileCoverageService.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Models\FileCoverageOptions.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Models\FileCoverageReport.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Models\FolderCoverage.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Models\FileCoverageEntry.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\FileCoverageServiceTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\RoleConstraintEvaluator.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\RoleDefinitionService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\RoleDefinitionServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-reviewer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\FileCoverageTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\FileCoverageService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\InquisitionCommand.cs — Modified


## Review Summary

Documentation batch D complete. Filled reference placeholders (configuration.md, audit-system.md), updated outdated pages (about-dynadocs.md role table, architecture.md new sections, glossary.md real terms, welcome.md, about.md, v1.3-release.md statuses), fixed dydo check error (coverage.py-update.md title), added missing inquisition command to dydo-commands.md. 11 files changed. dydo check passes with 0 new errors.

## Code Review (2026-03-16 21:12)

- Reviewed by: Brian
- Result: FAILED
- Issues: Test ReferenceDocAndTemplate_HaveSameOptions fails: '### dydo inquisition coverage' added to dydo-commands.md but not to template. Template must be updated to match. All other content is solid.

Requires rework.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-16 21:23
- Result: PASSED
- Notes: LGTM. Template now matches reference doc — Inquisition Commands section correctly added to Templates/dydo-commands.template.md. Test ReferenceDocAndTemplate_HaveSameOptions passes. Coverage gap check passes (121/121). All 11 doc files reviewed, content quality is high. One pre-existing unrelated test failure (LicenseSection_ConsistentAcrossAllReadmes) due to LF/CRLF mismatch in about-dynadocs.md — not introduced by this task.

Awaiting human approval.

## Approval

- Approved: 2026-03-19 13:03
