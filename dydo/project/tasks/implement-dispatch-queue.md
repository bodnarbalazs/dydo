---
area: general
name: implement-dispatch-queue
status: human-reviewed
created: 2026-03-25T22:24:43.9683678Z
assigned: Brian
updated: 2026-03-26T13:03:11.2741463Z
---

# Task: implement-dispatch-queue

Implemented dispatch queue feature: --queue flag on dydo dispatch defers terminal launch when queue has active item. QueueService manages named queues under dydo/_system/.local/queues/. Dequeue triggers on agent release. Watchdog extended for stale active detection and transient queue cleanup. One known deviation: dydo/reference/dydo-commands.md needs --queue documented (template updated, reference doc needs docs-writer). One test fails: AllOptions_AppearInReferenceDoc due to the missing doc update. dydo.json queues section not added (guard blocked) — code defaults to merge as built-in persistent queue.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented dispatch queue feature: --queue flag on dydo dispatch defers terminal launch when queue has active item. QueueService manages named queues under dydo/_system/.local/queues/. Dequeue triggers on agent release. Watchdog extended for stale active detection and transient queue cleanup. One known deviation: dydo/reference/dydo-commands.md needs --queue documented (template updated, reference doc needs docs-writer). One test fails: AllOptions_AppearInReferenceDoc due to the missing doc update. dydo.json queues section not added (guard blocked) — code defaults to merge as built-in persistent queue.

## Code Review (2026-03-26 12:35)

- Reviewed by: Charlie
- Result: FAILED
- Issues: Wrong PID in queue active entries: Environment.ProcessId captures the short-lived dispatch/release CLI PID, not the launched terminal PID. Watchdog sees it as stale within ~10 seconds and prematurely dequeues, breaking queue serialization. Fix: change IProcessStarter.Start() to return int (PID), thread it through LaunchNewTerminal, use it in SetActive calls. Minor: .queued marker missing from preserved paths in AgentRegistry/WorkspaceArchiver.

Requires rework.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-26 13:10
- Result: PASSED
- Notes: LGTM. PID fix correctly threaded through all launchers and SetActive call sites. .queued marker preserved in both SystemManagedEntries arrays. 3238 tests pass, gap_check 131/131 modules pass. Known deviation: AllOptions_AppearInReferenceDoc fails (needs docs-writer for --queue). Minor note: dotnet-run nudge pattern missing queue subcommand.

Awaiting human approval.