---
area: general
type: changelog
date: 2026-04-11
---

# Task: fix-audit-backend-issues

Fixed all 8 issues. #70: baseline filter in ListSessionFiles. #71: sidecar append for O(1) cross-process writes (CompactJsonContext for JSONL). #72: vis-network pinned with SRI. #74: ResolveSessionSnapshot for compacted refs. #81: stderr warnings on corrupt audit files. #39/#40: deleted dead PathPermissionChecker. #90: GenerateBash/PsJunctionScript centralizes junction list. Tests: 3678/3678 pass. gap_check: 1 pre-existing CRAP failure on GuardCommand.cs (not modified), confirmed tracked by orchestrator.

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


## Review Summary

Fixed all 8 issues. #70: baseline filter in ListSessionFiles. #71: sidecar append for O(1) cross-process writes (CompactJsonContext for JSONL). #72: vis-network pinned with SRI. #74: ResolveSessionSnapshot for compacted refs. #81: stderr warnings on corrupt audit files. #39/#40: deleted dead PathPermissionChecker. #90: GenerateBash/PsJunctionScript centralizes junction list. Tests: 3678/3678 pass. gap_check: 1 pre-existing CRAP failure on GuardCommand.cs (not modified), confirmed tracked by orchestrator.

## Code Review

- Reviewed by: Emma
- Date: 2026-04-11 18:34
- Result: PASSED
- Notes: LGTM. All 8 issues cleanly fixed. Tests comprehensive (3685/3685 pass). gap_check green (135/135 modules). Sidecar design for O(1) cross-process writes is well-structured. SRI pinning correct. Dead code removal complete. Minor note: LoadSessionFile's mergeSidecar param unused.

Awaiting human approval.

## Approval

- Approved: 2026-04-11 19:34
