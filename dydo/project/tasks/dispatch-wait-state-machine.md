---
area: platform
name: dispatch-wait-state-machine
status: human-reviewed
created: 2026-03-09T12:08:57.0995808Z
assigned: Charlie
updated: 2026-03-09T13:03:33.2300955Z
---

# Task: dispatch-wait-state-machine

Add listening/pid lifecycle to WaitMarker, guard pending-state enforcement, and self-healing PID checks

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented Slice 1 of the Dispatch-Wait State Machine.

## What was implemented

1. **WaitMarker model** — Added `Listening` (bool, default false) and `Pid` (int?, default null) properties with JSON serialization
2. **AgentRegistry** — Added `UpdateWaitMarkerListening`, `ResetWaitMarkerListening`, and `GetNonListeningWaitMarkers` methods
3. **WaitCommand** — On task-specific wait startup, sets marker to `listening: true` with current PID before polling loop
4. **GuardCommand** — Pending-state enforcement in 4 locations:
   - Read operations path (blocks when non-listening markers exist)
   - Write/Edit operations path (same)
   - Glob/Grep search tools path (same)
   - Bash commands: non-dydo commands blocked; dydo commands blocked except `dydo dispatch` and `dydo wait` (any form including --cancel)
5. **Self-healing** — Before checking pending state, markers with `listening: true` but dead/null PID are flipped to `listening: false`

## Plan deviations

- Added `ResetWaitMarkerListening` method (not in plan) for clean self-healing — setting `listening: false` and `pid: null` without the `UpdateWaitMarkerListening` method which always sets `listening: true`
- Did not create a separate `ProcessUtils` method — used existing `ProcessUtils.IsProcessRunning` which already handles the PID liveness check

## Key decisions

- Self-healing happens inline in the guard (via `SelfHealAndGetPendingMarkers` helper) rather than as a separate step
- Pending-state check runs AFTER unread messages check but BEFORE must-read enforcement
- 30 tests written covering: model serialization/deserialization/backward compat, registry methods, guard blocking for Read/Write/Edit/Glob/Grep/Bash, dydo dispatch/wait allowlist, self-healing with dead/alive/null PIDs, cancel lifecycle

## Test results

All 1493 existing tests pass. 30 new tests added, all passing.

## Code Review

- Reviewed by: Leo
- Date: 2026-03-09 15:05
- Result: PASSED
- Notes: LGTM. Clean implementation of WaitMarker listening/PID lifecycle, guard pending-state enforcement across all 5 tool paths, self-healing for dead/null PIDs. 30 tests covering model, registry, guard blocking, bash allowlist, self-healing, and cancel lifecycle. All 1656 tests pass.

Awaiting human approval.