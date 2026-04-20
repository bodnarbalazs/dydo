---
area: platform
name: revive-watchdog-on-release
status: human-reviewed
created: 2026-04-18T22:38:54.8417271Z
assigned: Brian
updated: 2026-04-20T12:06:52.9921308Z
---

# Task: revive-watchdog-on-release

Issue #102: dydo agent release should revive a dead watchdog. One-line fix in Commands/AgentLifecycleHandlers.cs ExecuteRelease.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review one-line fix for issue #102 (dead watchdog not revived on release). Changes:

- Commands/AgentLifecycleHandlers.cs:80 -- added 'try { WatchdogService.EnsureRunning(); } catch { /* best-effort */ }' between 'Status: free' println and DequeueIfActive, per Grace's brief at dydo/agents/Grace/brief-revive-watchdog-on-release.md (Option A).
- DynaDocs.Tests/Integration/AgentLifecycleTests.cs -- added Release_RevivesWatchdogWhenDead integration test using WatchdogService.StartProcessOverride to count EnsureRunning invocations. Confirmed red before fix (0), green after (1).

Verification: python DynaDocs.Tests/coverage/run_tests.py -- --filter 'FullyQualifiedName~AgentLifecycleTests' -> 67/67 passed.

Heads-up: gap_check surfaces 2 pre-existing failures in PhantomUnreadInboxTests (Guard_NotifyUnreadMessages_*). These are unrelated to this task -- they reproduce without my change and live in files owned by another in-flight agent (PhantomUnreadInboxTests.cs, GuardCommand.cs, AgentRegistry.cs), which Brian's dispatch explicitly marked off-limits. User and Brian have been notified. Do not block review on those.

## Code Review

- Reviewed by: Grace
- Date: 2026-04-20 13:03
- Result: PASSED
- Notes: LGTM. One-line fix at Commands/AgentLifecycleHandlers.cs:80 is correct and minimal. EnsureRunning is idempotent; best-effort try/catch keeps release from failing on watchdog revival. Integration test (Release_RevivesWatchdogWhenDead) uses the existing StartProcessOverride seam and confirms red->green. 67/67 AgentLifecycleTests pass locally. 2 failing PhantomUnreadInboxTests are Charlie's in-flight phantom-unread-inbox work (off-limits per Henry's dispatch); Brian authorized pass on that basis.

Awaiting human approval.