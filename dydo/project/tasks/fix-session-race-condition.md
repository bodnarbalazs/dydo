---
area: general
name: fix-session-race-condition
status: human-reviewed
created: 2026-03-18T18:05:34.3020638Z
assigned: Emma
updated: 2026-03-18T20:26:05.6349175Z
---

# Task: fix-session-race-condition

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented DYDO_AGENT env var injection in all three terminal launchers (Windows, Linux, Mac) and added env var fast paths to AgentRegistry.GetSessionContext() and GetCurrentAgent(). Added 5 new tests (3 terminal launcher, 3 AgentRegistry). All 2689 tests pass; 1 pre-existing license test failure unrelated. No plan deviations.

## Code Review (2026-03-18 18:54)

- Reviewed by: Emma
- Result: FAILED
- Issues: 3 issues: (1) dead code ReviewDispatchedMarker.cs not deleted, (2) duplicated release constraint logic between AgentRegistry and RoleConstraintEvaluator, (3) coverage gap check fails on AgentRegistry.cs CRAP 30.6 and RoleDefinitionService.cs CRAP 32.0 (T1 threshold <=30). Dispatched to Frank for fixes.

Requires rework.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-18 20:46
- Result: PASSED
- Notes: LGTM. All 3 review issues addressed cleanly: dead code removed, constraint logic deduplicated via RoleConstraintEvaluator delegation, CRAP regressions fixed. 15 new tests, all meaningful. No new issues found.

Awaiting human approval.