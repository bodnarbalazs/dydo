---
area: general
name: fix-merge-diagnostics
status: review-failed
created: 2026-03-22T17:57:43.3225905Z
assigned: Brian
updated: 2026-03-22T17:59:51.9223419Z
---

# Task: fix-merge-diagnostics

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Added temporary Console.WriteLine diagnostics to the worktree merge dispatch path across 3 files: DispatchService.cs (WriteAndLaunch after senderWorktreeId, before CopyWorktreeMetadataForMerger, and inside CopyWorktreeMetadataForMerger), WorktreeCommand.cs (ExecuteMerge entry), and ReviewCommand.cs (worktree marker check in review complete). No logic changed — purely additive debug output. No plan deviations.

## Code Review (2026-03-22 18:04)

- Reviewed by: Charlie
- Result: FAILED
- Issues: FAIL. Temporary Console.WriteLine debug output does not belong in committed code. Issues: (1) 14 raw Console.WriteLine calls with [*-debug] prefixes across 3 files violate core doctrine: 'every line must justify its existence' — removing them changes nothing. (2) Inconsistent output pattern: rest of codebase uses ConsoleOutput helpers, not raw Console.WriteLine. (3) No toggle mechanism — debug noise always visible to users. (4) WorktreeCommand.cs:366 introduces worktreeBasePath variable used only for debug output — dead code once lines are removed. (5) WorktreeCommand.cs:364-365,368-369 add File.ReadAllText calls purely for debug output — unnecessary I/O with TOCTOU risk. If diagnostics are genuinely needed long-term, implement them behind a --verbose flag using ConsoleOutput. If they were just for one-time debugging, remove them entirely.

Requires rework.