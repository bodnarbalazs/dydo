---
title: FindAncestorProcess substring match too broad (claude vs claudia)
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 128
type: issue
found-by: inquisition
date: 2026-04-28
resolved-date: 2026-04-30
---

# FindAncestorProcess substring match too broad (claude vs claudia)
Resolved low-severity correctness bug: `FindAncestorProcess` matched anything containing "claude" (e.g., `claudia`, `claude-dev`), and the anchor PID parser accepted `1` (init/System) which never appears gone. Fixed in commit `762eeda` by switching to exact basename match without extension and rejecting `PID <= 1` at parse time.
## Description
**Mechanism.** `ProcessUtils.FindAncestorProcess` (Services/ProcessUtils.Ancestry.cs:49) does `name.Contains('claude', StringComparison.OrdinalIgnoreCase)`. Any third-party process whose name contains 'claude' would be picked as the anchor: `claudia.exe`, `claude-dev.exe`, future tooling.
Same broad-match concern at the env-var read path (Services/WatchdogService.cs:206): `int.TryParse` accepts `DYDO_WATCHDOG_ANCHOR_PID` of `1` (init/System on Linux/macOS), which would never appear 'gone' — the watchdog would run forever (functionally equivalent to finding #6/#126).
**Suggested fix.** Replace `Contains('claude', OrdinalIgnoreCase)` with exact-match against `'claude'` (or `StartsWith('claude', OrdinalIgnoreCase)` if you need to allow Windows extensions). Reject `DYDO_WATCHDOG_ANCHOR_PID <= 1` at parse time.
**Found by inquisition:** dydo/project/inquisitions/agent-deaths.md (Finding #6 — broad match).
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed by 762eeda (FindAncestorProcess.MatchesProcessName uses exact basename match without extension; rejects PID <= 1 at parse time).