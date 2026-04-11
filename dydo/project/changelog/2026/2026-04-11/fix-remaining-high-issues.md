---
area: general
type: changelog
date: 2026-04-11
---

# Task: fix-remaining-high-issues

Fixed 3 security issues: (1) #84 RemoveZombieDirectory now uses DeleteDirectoryJunctionSafe instead of Directory.Delete(recursive: true), preventing junction traversal. (2) #85 Added bash/sh/zsh to InlineInterpreterRegex so bash -c / sh -c are blocked as dangerous patterns. (3) #86 Added AnalyzeCommandSubstitutions to extract and analyze contents of $() and backticks, detecting write operations hidden inside substitutions. All 3659 tests pass, gap_check green. Two pre-existing false-positive tests for bash -c / sh -c were removed from GuardIntegrationTests since these are now correctly blocked.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\AuditEdgeCaseTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AuditService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\AuditCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\AuditCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\SnapshotCompactionService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCompatTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Serialization\DydoJsonContext.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardSecurityTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\BashCommandAnalyzer.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardIntegrationTests.cs — Modified


## Review Summary

Fixed 3 security issues: (1) #84 RemoveZombieDirectory now uses DeleteDirectoryJunctionSafe instead of Directory.Delete(recursive: true), preventing junction traversal. (2) #85 Added bash/sh/zsh to InlineInterpreterRegex so bash -c / sh -c are blocked as dangerous patterns. (3) #86 Added AnalyzeCommandSubstitutions to extract and analyze contents of $() and backticks, detecting write operations hidden inside substitutions. All 3659 tests pass, gap_check green. Two pre-existing false-positive tests for bash -c / sh -c were removed from GuardIntegrationTests since these are now correctly blocked.

## Code Review (2026-04-10 12:55)

- Reviewed by: Charlie
- Result: FAILED
- Issues: FAIL: (1) temp/nagyi.txt — empty unrelated file committed as part of this security fix. Must be removed. (2) InlineInterpreterRegex comment (BashCommandAnalyzer.cs:277-278) not updated to mention bash/sh/zsh. (3) Regex -[ceE] is over-broad for bash/sh/zsh — only -c is inline execution; -e (exit on error) and -E (inherit ERR traps) are not. Conservative false positive. The security fixes themselves are solid, tests pass (3659/3659), gap_check green.

Requires rework.

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-10 13:54
- Result: PASSED
- Notes: LGTM. All 3 review issues resolved correctly: (1) temp/nagyi.txt staged for deletion, (2) comment updated to mention bash/sh/zsh with explanation of -e/-E semantics, (3) regex split into two alternation groups — python/node/ruby/perl match -[ceE] while bash/sh/zsh match only -c. 7 new test cases verify -e/-E/-eE are not false-positived. 3666/3666 tests pass, gap_check green (136/136 modules).

Awaiting human approval.

## Approval

- Approved: 2026-04-11 19:34
