---
area: general
type: changelog
date: 2026-05-04
---

# Task: fix-pwsh-wait-approval

Review commit 3808f37 for fix-pwsh-wait-approval (#0145).

Origin brief: dydo/agents/Yara/inbox/44c9a8b8-fix-pwsh-wait-approval.md (from Brian).
Investigation: dydo/agents/Dexter/notes-investigate-pwsh-wait-approval.md.
Issue: dydo/project/issues/0145-...md.

Verify in the commit:
1. Commands/InitCommand.cs — DydoAllowEntry scalar replaced with DydoAllowEntries array containing Bash(dydo:*) AND PowerShell(dydo:*); ConfigureAllowList iterates+dedupes.
2. DynaDocs.Tests/Integration/InitCommandTests.cs — four new tests parallel to lines 272-336 Bash assertions: AddsPowerShellDydoAllowEntry, PowerShellAllowMergesWithExistingEntries, DoesNotDuplicatePowerShellAllowEntry, BothShellEntriesWhenAllowArrayMissing.

Build: clean (0 warnings, 0 errors). gap_check: 137/137 modules.
InitCommandTests in isolation: 35/35 pass.

Two items to flag in your review:
A) The brief asked for a parallel edit to .claude/settings.local.json (live checked-in copy). That path is outside the code-writer writable set; the guard correctly blocks. Deferred to user/other role. Commit message says so. Verify this is acceptable to ship without.
B) Full-suite test runs showed flaky failures unrelated to this change (different tests each run: InboxServiceTests.PrintInboxItem_TaskItem_IncludesFilePath in run 1; WaitGeneral_SkipsMessage_AlreadyInUnreadAtStart + Message_ToInactiveAgent_WithForce_DoesNotUpdateUnreadMessages in run 2). All pass in isolation. Likely the test parallelization race that 0a6e930 partially addressed. Not a regression from this PR.

Approve or reject.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit 3808f37 for fix-pwsh-wait-approval (#0145).

Origin brief: dydo/agents/Yara/inbox/44c9a8b8-fix-pwsh-wait-approval.md (from Brian).
Investigation: dydo/agents/Dexter/notes-investigate-pwsh-wait-approval.md.
Issue: dydo/project/issues/0145-...md.

Verify in the commit:
1. Commands/InitCommand.cs — DydoAllowEntry scalar replaced with DydoAllowEntries array containing Bash(dydo:*) AND PowerShell(dydo:*); ConfigureAllowList iterates+dedupes.
2. DynaDocs.Tests/Integration/InitCommandTests.cs — four new tests parallel to lines 272-336 Bash assertions: AddsPowerShellDydoAllowEntry, PowerShellAllowMergesWithExistingEntries, DoesNotDuplicatePowerShellAllowEntry, BothShellEntriesWhenAllowArrayMissing.

Build: clean (0 warnings, 0 errors). gap_check: 137/137 modules.
InitCommandTests in isolation: 35/35 pass.

Two items to flag in your review:
A) The brief asked for a parallel edit to .claude/settings.local.json (live checked-in copy). That path is outside the code-writer writable set; the guard correctly blocks. Deferred to user/other role. Commit message says so. Verify this is acceptable to ship without.
B) Full-suite test runs showed flaky failures unrelated to this change (different tests each run: InboxServiceTests.PrintInboxItem_TaskItem_IncludesFilePath in run 1; WaitGeneral_SkipsMessage_AlreadyInUnreadAtStart + Message_ToInactiveAgent_WithForce_DoesNotUpdateUnreadMessages in run 2). All pass in isolation. Likely the test parallelization race that 0a6e930 partially addressed. Not a regression from this PR.

Approve or reject.

## Code Review

- Reviewed by: Adele
- Date: 2026-05-01 17:10
- Result: PASSED
- Notes: Code clean, surgical, idiomatic. 4 tests parallel Bash counterparts. Test runner: 4017/4017 (foreground); gap_check: 137/137. Yara's items A+B addressed (acceptable). Pre-existing dydo check errors (13: inquisition type-enum + template-addition headings) are out of fix scope and being addressed by separate docs-cleanup track (b92e1b3, 756bedb). See dydo/agents/Adele/notes-review-fix-pwsh-wait-approval.md.

Awaiting human approval.

## Approval

- Approved: 2026-05-04 21:52
