---
title: v223-1 Pending Patch Consolidation
blocked-by:
due:
needs-human: false
priority: High
sprint: v2-2-3-upgrade-compatibility
status: done
work-type: bugfix
area: backend
type: context
---

# v223-1 Pending Patch Consolidation

Review and finish the existing non-sync dirty-tree implementation for issues 0300-0305 as one patch unit. `SyncCommand.cs`, its tests, generated-role compatibility, issue-record edits, and issue 0307 implementation are outside this slice.

## Task

1. Verify each non-sync production change against its issue record and corresponding tests.
2. Confirm init/join records integrations and supports `all`; leave consumption/backward compatibility to v223-2.
3. Confirm retired-diagram cleanup deletes only hash-clean framework copies and preserves modified copies.
4. Confirm Notion page archival serializes `in_trash` and the live-test hygiene cannot affect recent concurrent smoke pages.
5. Repair incomplete release-owned changes in place. Do not modify the unrelated task-record normalization or issue 0307's implementation scope.

## Files

- `Commands/GuardCommand.cs`, `Commands/HelpCommand.cs`, `Commands/InitCommand.cs`, `Commands/TemplateCommand.cs`
- `Services/CompletionProvider.cs`, `Services/TemplateGenerator.cs`
- `Sync/Notion/Dtos/NotionPageUpdateRequestConverter.cs`
- `Templates/Assets/dydo-diagram.svg` (deletion), `Templates/dydo-commands.template.md`
- `DynaDocs.Tests/Commands/CompleteCommandTests.cs`
- `DynaDocs.Tests/Integration/GuardIntegrationTests.cs`, `InitCheckIntegrationTests.cs`, `InitCommandTests.cs`, `TemplateCommandTests.cs`
- `DynaDocs.Tests/Services/FolderScaffolderTests.cs`, `TemplateGeneratorTests.cs`
- `DynaDocs.Tests/Sync/Notion/Live/NotionLiveTestBase.cs`, `DynaDocs.Tests/Sync/Notion/NotionClientTests.cs`
- `dydo.json`, `dydo/_assets/dydo-diagram.svg` (deletion), `dydo/reference/dydo-commands.md`, `dydo/understand/templates-and-customization.md`

## Success Criteria

- Focused gate passes:
  `py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~GuardIntegrationTests|FullyQualifiedName~InitCommandTests|FullyQualifiedName~TemplateCommandTests|FullyQualifiedName~NotionClientTests|FullyQualifiedName~FolderScaffolderTests|FullyQualifiedName~TemplateGeneratorTests"`
- The reviewed patch has no unexplained production or test change.
- No LC file is touched.
