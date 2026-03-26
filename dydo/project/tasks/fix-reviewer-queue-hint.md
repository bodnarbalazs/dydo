---
area: general
name: fix-reviewer-queue-hint
status: human-reviewed
created: 2026-03-26T13:53:26.1869044Z
assigned: Brian
updated: 2026-03-26T14:44:34.1100383Z
---

# Task: fix-reviewer-queue-hint

One-line change in Templates/mode-reviewer.template.md:106 — added --queue merge to the worktree merge dispatch example. Also fixed AgentRegistry.cs:484 where the release-block nudge was missing --queue merge and --auto-close. ReviewCommand.cs was already correct. All three hints now emit the same canonical command. dydo template update regenerated all agent mode files. 3239 tests pass, coverage gate clear.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

One-line change in Templates/mode-reviewer.template.md:106 — added --queue merge to the worktree merge dispatch example. Also fixed AgentRegistry.cs:484 where the release-block nudge was missing --queue merge and --auto-close. ReviewCommand.cs was already correct. All three hints now emit the same canonical command. dydo template update regenerated all agent mode files. 3239 tests pass, coverage gate clear.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-26 14:49
- Result: PASSED
- Notes: LGTM. All three merge dispatch hints (ReviewCommand.cs:136, AgentRegistry.cs:484, mode-reviewer.template.md:106) now emit the same canonical command with --queue merge --auto-close. All 25 agent mode files regenerated correctly. 3239 tests pass, coverage gate 131/131.

Awaiting human approval.