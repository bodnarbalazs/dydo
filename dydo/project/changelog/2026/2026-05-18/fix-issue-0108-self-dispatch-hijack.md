---
area: general
type: changelog
date: 2026-05-18
---

# Task: fix-issue-0108-self-dispatch-hijack

Fix for issue #0108 (self-dispatch identity hijack). Two changes in Services/AgentSelector.cs: (1) TryReserveFromPool now filters senderName from candidates (mirrors TryReserveOrigin's origin==senderName guard); (2) SelectExplicit now has a senderName parameter and rejects to==senderName with a clear error before any reservation. DispatchService.SelectTargetAgent threads senderName through. Regression tests in DynaDocs.Tests/Services/AgentSelectorTests.cs: SelectAutomatic_DoesNotPickSenderFromPool (stale-working Brian with dead pid + busy others → expects no-free-agents error, not Brian) and SelectExplicit_RejectsSelfDispatch (asserts null result + 'yourself' error + Brian stays Free). Pre-existing Dispatch_ToBusyAgent_Fails was repurposed (it had been using Adele→Adele as a 'busy' setup which my self-guard now intercepts) to mark Brian busy via dispatch and then have Adele dispatch to him; intent preserved. gap_check.py green: 4196/4196 pass, 141/141 modules pass tier. Issue #108 resolved with --summary referencing this change.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fix for issue #0108 (self-dispatch identity hijack). Two changes in Services/AgentSelector.cs: (1) TryReserveFromPool now filters senderName from candidates (mirrors TryReserveOrigin's origin==senderName guard); (2) SelectExplicit now has a senderName parameter and rejects to==senderName with a clear error before any reservation. DispatchService.SelectTargetAgent threads senderName through. Regression tests in DynaDocs.Tests/Services/AgentSelectorTests.cs: SelectAutomatic_DoesNotPickSenderFromPool (stale-working Brian with dead pid + busy others → expects no-free-agents error, not Brian) and SelectExplicit_RejectsSelfDispatch (asserts null result + 'yourself' error + Brian stays Free). Pre-existing Dispatch_ToBusyAgent_Fails was repurposed (it had been using Adele→Adele as a 'busy' setup which my self-guard now intercepts) to mark Brian busy via dispatch and then have Adele dispatch to him; intent preserved. gap_check.py green: 4196/4196 pass, 141/141 modules pass tier. Issue #108 resolved with --summary referencing this change.

## Code Review

- Reviewed by: Charlie
- Date: 2026-05-18 18:57
- Result: PASSED
- Notes: LGTM. AgentSelector now symmetrically guards self-dispatch on both paths: SelectExplicit rejects to==senderName up-front before any reservation (no orphaned Dispatched state); TryReserveFromPool filters senderName from candidates, mirroring TryReserveOrigin's existing guard. senderName threaded cleanly through DispatchService.SelectTargetAgent. Regression tests SelectAutomatic_DoesNotPickSenderFromPool (stale-working+dead-pid scenario matching Frank's marathon trigger) and SelectExplicit_RejectsSelfDispatch (asserts null+'yourself'+Brian stays Free) cover both attack vectors from the issue. Dispatch_ToBusyAgent_Fails repurposing is justified — old Adele→Adele setup is now intercepted by the new self-guard, so it was rewritten minimally to dispatch-then-claim, preserving the 'not free' assertion. gap_check 141/141 modules pass tier; dydo check clean for all files touched by this commit (only remaining error is pre-existing agent-crashes.md:1070, unmodified here).

Awaiting human approval.

## Approval

- Approved: 2026-05-18 19:00
