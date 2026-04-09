---
area: general
type: changelog
date: 2026-04-09
---

# Task: fix-issues-template-bugs

Fixed 5 template system bugs from inquisition: (1) StoreInitialFrameworkHashes now hashes doc and binary files alongside templates (#29), (2) Reanchor preserves insertion order for shared upper anchors by skipping past already-placed tags (#30), (3) GetOldStockContent falls back to onDisk instead of new embedded content when storedHash is null or irrecoverable (#31), (4) AccumulateResult counts warned files in summary output (#32), (5) FindLineIndex uses last occurrence for upper-only anchors to avoid frontmatter collision (#38). Also fixed CRLF double-encoding in test and added TerminalLauncher coverage tests for gap_check. All 3538 tests pass, 135/135 modules pass gap_check.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Commands\HelpCommand.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardSecurityTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardWorktreeAllowBashWriteTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Program.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\HelpCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CommandSmokeTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\dydo-commands.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\InitCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CommandDocConsistencyTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\OffLimitsService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\BashCommandAnalyzer.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\IBashCommandAnalyzer.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WorktreeCreationLockTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\IncludeReanchorTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\TemplateCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TemplateUpdateTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\FolderScaffolder.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\IncludeReanchor.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TemplateCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified


## Review Summary

Fixed 5 template system bugs from inquisition: (1) StoreInitialFrameworkHashes now hashes doc and binary files alongside templates (#29), (2) Reanchor preserves insertion order for shared upper anchors by skipping past already-placed tags (#30), (3) GetOldStockContent falls back to onDisk instead of new embedded content when storedHash is null or irrecoverable (#31), (4) AccumulateResult counts warned files in summary output (#32), (5) FindLineIndex uses last occurrence for upper-only anchors to avoid frontmatter collision (#38). Also fixed CRLF double-encoding in test and added TerminalLauncher coverage tests for gap_check. All 3538 tests pass, 135/135 modules pass gap_check.

## Code Review

- Reviewed by: Henry
- Date: 2026-04-08 18:50
- Result: PASSED
- Notes: PASS. All 5 template system bugs correctly fixed: (1) StoreInitialFrameworkHashes now hashes doc+binary files, (2) Reanchor preserves insertion order via skip-past-placed loop, (3) GetOldStockContent safely falls back to onDisk when storedHash null or irrecoverable, (4) AccumulateResult tracks warned count in summary, (5) FindLineIndexLast avoids frontmatter collision for upper-only anchors. Bonus: git command hardening with double-dash separators, RunProcess consolidation, dead code removal, DispatchService timeout. All 3538 tests pass, 135/135 modules pass gap_check.

Awaiting human approval.

## Approval

- Approved: 2026-04-09 22:49
