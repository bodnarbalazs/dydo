---
area: general
name: inbox-indicator
status: human-reviewed
created: 2026-03-10T13:12:21.5458404Z
assigned: Dexter
updated: 2026-03-10T13:34:29.8646229Z
---

# Task: inbox-indicator

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented inbox-indicator plan from Brian. Added HasPendingInbox to IAgentRegistry/AgentRegistry, 9-char name limit in CreateAgent/RenameAgent, asterisk suffix in agent list output, dispatch auto-select inbox prioritization. 11 new tests across 4 files. No plan deviations. All tests pass.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-10 13:53
- Result: PASSED
- Notes: LGTM. All 3 requirements implemented correctly: asterisk indicator, dispatch inbox prioritization, 9-char name limit. 11 tests pass. Code is clean and minimal.

Awaiting human approval.