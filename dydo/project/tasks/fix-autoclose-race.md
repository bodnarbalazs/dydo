---
area: general
name: fix-autoclose-race
status: human-reviewed
created: 2026-03-27T18:14:11.8961154Z
assigned: Charlie
updated: 2026-03-27T18:39:07.2477453Z
---

# Task: fix-autoclose-race

Fixed the auto-close race condition in WatchdogService.PollAndCleanup. Removed the two-poll deferral that allowed agent re-dispatch between polls to leave old sessions alive (the phantom close issue it guarded against was already fixed separately). Added a test reproducing the exact race scenario from the investigation. All 43 watchdog tests pass, gap_check clean.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed the auto-close race condition in WatchdogService.PollAndCleanup. Removed the two-poll deferral that allowed agent re-dispatch between polls to leave old sessions alive (the phantom close issue it guarded against was already fixed separately). Added a test reproducing the exact race scenario from the investigation. All 43 watchdog tests pass, gap_check clean.

## Code Review

- Reviewed by: Dexter
- Date: 2026-03-27 18:43
- Result: PASSED
- Notes: LGTM. Clean fix — deferral removal eliminates the race window, regression test reproduces the exact scenario. Net -72 lines, 43/43 tests pass, gap_check clean.

Awaiting human approval.