---
id: 128
area: backend
type: issue
severity: low
status: open
found-by: inquisition
date: 2026-04-28
---

# FindAncestorProcess substring match too broad (claude vs claudia)

## Description

**Mechanism.** `ProcessUtils.FindAncestorProcess` (Services/ProcessUtils.Ancestry.cs:49) does `name.Contains('claude', StringComparison.OrdinalIgnoreCase)`. Any third-party process whose name contains 'claude' would be picked as the anchor: `claudia.exe`, `claude-dev.exe`, future tooling.

Same broad-match concern at the env-var read path (Services/WatchdogService.cs:206): `int.TryParse` accepts `DYDO_WATCHDOG_ANCHOR_PID` of `1` (init/System on Linux/macOS), which would never appear 'gone' — the watchdog would run forever (functionally equivalent to finding #6/#126).

**Suggested fix.** Replace `Contains('claude', OrdinalIgnoreCase)` with exact-match against `'claude'` (or `StartsWith('claude', OrdinalIgnoreCase)` if you need to allow Windows extensions). Reject `DYDO_WATCHDOG_ANCHOR_PID <= 1` at parse time.

**Found by inquisition:** dydo/project/inquisitions/agent-deaths.md (Finding #6 — broad match).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)