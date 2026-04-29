---
area: general
type: changelog
date: 2026-04-29
---

# Task: fix-orchestrator-wait-deadlock-v2

Review fix-orchestrator-wait-deadlock-v2 (6 commits on worktree/fix-orchestrator-wait-deadlock-v2). Two bugs fixed per Charlie's plan at dydo/agents/Charlie/archive/20260428-142549/plan-orchestrator-wait-deadlock.md.

BUG A — orchestrator general-wait deadlock:
- Services/MessageFinder.cs: optional excludeIds parameter + filename-id regex.
- Commands/WaitCommand.cs: WaitGeneral snapshots agent.UnreadMessages at startup, passes to FindMessage on every poll. Pre-existing unreads no longer pop the wait → marker stays → orchestrator can Read → TrackReadCompletion clears unread → inbox clear works.

BUG B — PowerShell tool bypass:
- Commands/InitCommand.cs:355 + .claude/settings.local.json:28: matcher gains |PowerShell.
- Models/HookInputExtensions.cs: ActionMap powershell -> execute.
- Commands/GuardCommand.cs: ShellTools HashSet + ShouldRouteToShellHandler helper. PowerShell now routes through HandleBashCommand identically to Bash.
- dydo/reference/configuration.md:106: example matcher synced to current value.

REFACTOR (cac1434): extracted ShouldRouteToShellHandler to keep Execute under T1 CRAP threshold (was 30.2 with Bug B inline). No behavior change.

TESTS (7 new):
Bug A: WaitGeneral_SkipsMessage_AlreadyInUnreadAtStart, WaitGeneral_PopsOnNewMessage_EvenWhenStartupUnreadExists, MessageFinder_FindMessage_ExcludesIdsInExcludeSet, Guard_Orchestrator_CanReadInboxFile_WithUnreadAndTaskWait (deadlock-recovery e2e).
Bug B: Init_Claude_MatcherIncludesPowerShell, BugB_PowerShell_DangerousPattern_IsBlocked, BugB_PowerShell_DydoWaitForeground_IsBlocked.

DEVIATIONS:
- Plan's Guard_PowerShell_BlocksWithoutIdentity replaced with BugB_PowerShell_DydoWaitForeground_IsBlocked — the without-identity test would no-op (benign cmds aren't blocked even via Bash without claim); the dydo-wait variant proves the dydo-command branch routing.
- Skipped optional A.5 (inbox clear e2e) — A.4 already proves recovery once Read clears the unread.
- Added refactor commit cac1434 to reduce Execute CC; pre-existing CRAP overrun would have failed gap_check otherwise.

VERIFICATION: dotnet build clean; full suite 3860/3860; gap_check all 136 modules pass.

Files outside Charlie's normal writable scope (.claude/settings.local.json, dydo/reference/configuration.md) committed under the narrow guard lift Adele granted at 17:37 UTC.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review fix-orchestrator-wait-deadlock-v2 (6 commits on worktree/fix-orchestrator-wait-deadlock-v2). Two bugs fixed per Charlie's plan at dydo/agents/Charlie/archive/20260428-142549/plan-orchestrator-wait-deadlock.md.

BUG A — orchestrator general-wait deadlock:
- Services/MessageFinder.cs: optional excludeIds parameter + filename-id regex.
- Commands/WaitCommand.cs: WaitGeneral snapshots agent.UnreadMessages at startup, passes to FindMessage on every poll. Pre-existing unreads no longer pop the wait → marker stays → orchestrator can Read → TrackReadCompletion clears unread → inbox clear works.

BUG B — PowerShell tool bypass:
- Commands/InitCommand.cs:355 + .claude/settings.local.json:28: matcher gains |PowerShell.
- Models/HookInputExtensions.cs: ActionMap powershell -> execute.
- Commands/GuardCommand.cs: ShellTools HashSet + ShouldRouteToShellHandler helper. PowerShell now routes through HandleBashCommand identically to Bash.
- dydo/reference/configuration.md:106: example matcher synced to current value.

REFACTOR (cac1434): extracted ShouldRouteToShellHandler to keep Execute under T1 CRAP threshold (was 30.2 with Bug B inline). No behavior change.

TESTS (7 new):
Bug A: WaitGeneral_SkipsMessage_AlreadyInUnreadAtStart, WaitGeneral_PopsOnNewMessage_EvenWhenStartupUnreadExists, MessageFinder_FindMessage_ExcludesIdsInExcludeSet, Guard_Orchestrator_CanReadInboxFile_WithUnreadAndTaskWait (deadlock-recovery e2e).
Bug B: Init_Claude_MatcherIncludesPowerShell, BugB_PowerShell_DangerousPattern_IsBlocked, BugB_PowerShell_DydoWaitForeground_IsBlocked.

DEVIATIONS:
- Plan's Guard_PowerShell_BlocksWithoutIdentity replaced with BugB_PowerShell_DydoWaitForeground_IsBlocked — the without-identity test would no-op (benign cmds aren't blocked even via Bash without claim); the dydo-wait variant proves the dydo-command branch routing.
- Skipped optional A.5 (inbox clear e2e) — A.4 already proves recovery once Read clears the unread.
- Added refactor commit cac1434 to reduce Execute CC; pre-existing CRAP overrun would have failed gap_check otherwise.

VERIFICATION: dotnet build clean; full suite 3860/3860; gap_check all 136 modules pass.

Files outside Charlie's normal writable scope (.claude/settings.local.json, dydo/reference/configuration.md) committed under the narrow guard lift Adele granted at 17:37 UTC.

## Code Review

- Reviewed by: Emma
- Date: 2026-04-28 17:49
- Result: PASSED
- Notes: PASS. Bug A and Bug B fixes both correctly diagnosed and surgically applied. Bug A: WaitGeneral snapshots agent.UnreadMessages at startup and excludes those IDs via the new MessageFinder.FindMessage excludeIds parameter — verified via 3 unit/integration tests + end-to-end Guard_Orchestrator_CanReadInboxFile_WithUnreadAndTaskWait that proves TrackReadCompletion clears the unread once the deadlock is broken. Bug B: matcher (template + live settings) + ActionMap[powershell]=execute + ShellTools HashSet routing through HandleBashCommand — verified by Init_Claude_MatcherIncludesPowerShell, BugB_PowerShell_DangerousPattern_IsBlocked, BugB_PowerShell_DydoWaitForeground_IsBlocked. Refactor (cac1434) extracts ShouldRouteToShellHandler with no behavior change to keep Execute under T1 CRAP. Doc and live settings edits committed under Adele's narrow guard lift as documented. gap_check fresh full run: 3860/3860 tests pass, 136/136 modules tier-compliant.

Awaiting human approval.

## Approval

- Approved: 2026-04-29 12:04
