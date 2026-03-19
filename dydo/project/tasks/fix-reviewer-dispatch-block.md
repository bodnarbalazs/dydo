---
area: general
name: fix-reviewer-dispatch-block
status: human-reviewed
created: 2026-03-19T20:50:54.5676332Z
assigned: Dexter
updated: 2026-03-19T21:12:45.7322961Z
---

# Task: fix-reviewer-dispatch-block

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed DispatchedByRole not persisting through AgentRegistry.WriteStateFile. Root cause: AgentRegistry has its own duplicate WriteStateFile (line 1311) and StateFieldParsers (line 1378), both missing dispatched-by-role. The fix was previously applied only to AgentStateStore but not to AgentRegistry. Added dispatched-by-role to both WriteStateFile template and StateFieldParsers in AgentRegistry. Also added from_role to InboxItemParser.StringFieldSetters. 3 new tests in AgentRegistryTests verify persistence and round-trip. 3 new tests in InboxItemParserTests verify from_role parsing. All 2868 existing tests pass.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-19 21:16
- Result: PASSED
- Notes: LGTM. dispatched-by-role correctly added to AgentRegistry WriteStateFile template and StateFieldParsers, mirroring AgentStateStore. from_role correctly wired through InboxItemParser. 6 focused tests cover persistence, round-trip, and null cases. gap_check failures are all pre-existing (0 overlap with changed files). One unrelated SystemManagedEntries change noted but benign.

Awaiting human approval.