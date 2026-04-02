---
area: general
name: fix-watchdog-concurrency
status: human-reviewed
created: 2026-04-01T09:54:57.6417914Z
assigned: Charlie
updated: 2026-04-01T10:08:18.8823185Z
---

# Task: fix-watchdog-concurrency

Fixed TOCTOU race in WatchdogService.EnsureRunning. On Linux, unlink() succeeds on open files, so concurrent threads could delete each other's valid PID files between the stale check and FileMode.CreateNew. Added a static Lock to serialize in-process callers. The FileMode.CreateNew still handles cross-process atomicity. All 43 watchdog tests pass, coverage gate green.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed TOCTOU race in WatchdogService.EnsureRunning. On Linux, unlink() succeeds on open files, so concurrent threads could delete each other's valid PID files between the stale check and FileMode.CreateNew. Added a static Lock to serialize in-process callers. The FileMode.CreateNew still handles cross-process atomicity. All 43 watchdog tests pass, coverage gate green.

## Code Review

- Reviewed by: Grace
- Date: 2026-04-01 10:28
- Result: PASSED
- Notes: LGTM. Lock correctly serializes in-process TOCTOU window. FileMode.CreateNew retains cross-process atomicity. Concurrency test validates fix. Coverage gate green (132/132 modules pass).

Awaiting human approval.