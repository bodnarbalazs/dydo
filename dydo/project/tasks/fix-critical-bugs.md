---
area: general
name: fix-critical-bugs
status: review-failed
created: 2026-03-19T20:39:46.2460102Z
assigned: Charlie
updated: 2026-03-19T20:58:54.1824857Z
---

# Task: fix-critical-bugs

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed three bugs: (1) Guard lift marker moved from dydo/_system/.local/guard-lifts/ to dydo/agents/<name>/.guard-lift.json so it resolves through worktree junctions — fixes cross-directory lift. Added UTC test confirming ExpiresAt round-trips as DateTimeKind.Utc. (2) WatchdogService.TryCloseWindow now resolves full wt.exe path via LocalAppData/Microsoft/WindowsApps before falling back to PATH — fixes 0x80070002 error in background processes.

## Code Review (2026-03-19 21:05)

- Reviewed by: Brian
- Result: FAILED
- Issues: Three issues: (1) 'task compact' missing from Program.cs help text — fails AllCommands_AppearInHelpText. (2) 'task compact' missing from about-dynadocs.md and about-dynadocs.template.md — fails AboutQuickReference_IncludesAllCommands. (3) TaskCompactHandler.cs at 75% line coverage, T1 requires >= 80%.

Requires rework.