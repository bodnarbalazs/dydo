---
area: general
name: guard-lift-command
status: review-failed
created: 2026-03-19T18:52:42.0196929Z
assigned: Dexter
---

# Task: guard-lift-command

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented dydo guard lift/restore commands per plan at agents/Dexter/plan-guard-lift.md. Created: GuardLiftMarker model, GuardLiftService, GuardLiftCommand. Modified: AuditEvent (added GuardLift/GuardRestore enum values + Lifted property), DydoJsonContext (registered GuardLiftMarker), GuardCommand (added lift checks in HandleWriteOperation and CheckBashFileOperation, registered subcommands, added IsGuardLifted helper), AgentRegistry (lift cleanup on release), CommandDocConsistencyTests (excluded hidden commands from doc checks). Added 18 integration tests (all pass). Note: could not edit dydo/files-off-limits.md (code-writer restriction) — need to add 'dydo/_system/.local/guard-lifts/**' pattern. Coverage gap_check has 4 pre-existing failures not caused by this task (GuardCommand CC was already over threshold from other uncommitted changes).

## Code Review (2026-03-19 19:32)

- Reviewed by: Charlie
- Result: FAILED
- Issues: FAIL. Bug: CheckBashFileOperation lift bypass at line 698 doesn't tag audit with Lifted=true (spec violation). Missing off-limits pattern for guard-lifts dir. Minor: Restore/ClearLift duplication.

Requires rework.