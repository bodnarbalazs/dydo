---
area: general
type: changelog
date: 2026-04-02
---

# Task: docs-v13-fixes

Review documentation fixes across 8 files for three issues: (1) removed false platform-agnostic claims, added 'Built for Claude Code' notice, (2) reframed amnesia problem to context problem (memory is unstructured, not absent), (3) fixed judge role description to prioritize inquisition report evaluation over dispute arbitration. Files changed: dydo/reference/about-dynadocs.md, README.md, dydo/reference/roles/_roles.md, dydo/project/v1.3-release.md, dydo/understand/roles-and-permissions.md, dydo/understand/about.md, dydo/guides/getting-started.md, dydo/_system/roles/judge.role.json.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\README.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\BashCommandAnalyzerTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCompatTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\BashCommandAnalyzer.cs — Modified


## Review Summary

Review documentation fixes across 8 files for three issues: (1) removed false platform-agnostic claims, added 'Built for Claude Code' notice, (2) reframed amnesia problem to context problem (memory is unstructured, not absent), (3) fixed judge role description to prioritize inquisition report evaluation over dispute arbitration. Files changed: dydo/reference/about-dynadocs.md, README.md, dydo/reference/roles/_roles.md, dydo/project/v1.3-release.md, dydo/understand/roles-and-permissions.md, dydo/understand/about.md, dydo/guides/getting-started.md, dydo/_system/roles/judge.role.json.

## Code Review (2026-03-30 17:45)

- Reviewed by: Grace
- Result: FAILED
- Issues: Changes in the 8 files are correct and consistent. FAIL due to incomplete coverage: (1) Templates/about-dynadocs.template.md still has amnesia framing and dydo init none — new projects get stale docs, (2) npm/README.md still has amnesia framing and dydo init none — public package page is inconsistent, (3) Services/RoleDefinitionService.cs:147 hardcoded judge fallback still says 'Arbitrates disputes between agents', (4) DynaDocs.csproj PackageReleaseNotes still says 'platform-agnostic'. Items 1-2 are doc fixes within scope. Items 3-4 are code changes (need code-writer). Also: pre-existing test failure in WatchdogServiceTests.EnsureRunning_LivePid_DoesNotStartProcess (unrelated to this task). gap_check passes.

Requires rework.

## Approval

- Approved: 2026-04-02 18:55
