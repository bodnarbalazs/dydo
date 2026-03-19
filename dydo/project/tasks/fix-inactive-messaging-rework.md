---
area: general
name: fix-inactive-messaging-rework
status: human-reviewed
created: 2026-03-19T19:04:48.1118743Z
assigned: Brian
---

# Task: fix-inactive-messaging-rework

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed three review issues in MessageService.cs: (1) Added subject filtering to reply-pending check so markers filter by both To and Task fields when subject is provided, (2) eliminated duplicate GetAgentState call in BuildInactiveTargetMessage by passing targetState from CheckTargetActive, (3) renamed misleading test to OnlySenderActive_ShowsActiveList and removed stream-of-consciousness comments. All 9 inactive-agent tests pass. No plan deviations.

## Code Review

- Reviewed by: Dexter
- Date: 2026-03-19 19:32
- Result: PASSED
- Notes: LGTM. Three review issues fixed cleanly: subject filtering on reply-pending markers, duplicate GetAgentState eliminated, test renamed. 9 integration tests comprehensive and passing. gap_check failures are pre-existing/other tasks, not regressions from this work.

Awaiting human approval.