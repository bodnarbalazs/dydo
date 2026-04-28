---
id: 126
area: backend
type: issue
severity: medium
status: open
found-by: inquisition
date: 2026-04-28
---

# Watchdog leaks forever when FindAncestorProcess('claude') returns null

## Description

**Mechanism.** `WatchdogService.EnsureRunning` (Services/WatchdogService.cs:120-122) resolves the anchor PID via `ProcessUtils.FindAncestorProcess('claude')` and passes it as `DYDO_WATCHDOG_ANCHOR_PID`. `FindAncestorProcess` (Services/ProcessUtils.Ancestry.cs:37-56) walks up to 10 ancestors looking for a process whose name contains 'claude'; returns null if none found.

When null, the env var is not set. `Run()` (Services/WatchdogService.cs:204-206) parses an unset env var as `null`. The liveness check at line 215 — `if (anchorPid.HasValue && !ProcessUtils.IsProcessRunning(anchorPid.Value)) break;` — short-circuits to `false` when `anchorPid` is null, so the break is **never** taken. The only remaining exits are `ProcessExit` and `CancelKeyPress`, which do not fire reliably on detached background processes.

**Impact.** Any dispatch path without claude in the ancestry leaks a watchdog forever (until manual `dydo watchdog stop` or reboot): CLI tests, manual `dydo dispatch` from a plain shell, scheduled jobs, CI runs.

**Suggested fix.** On null anchor, log + fall back to a max-watchdog-age timeout exit (e.g. 24 h) so an orphaned watchdog cannot leak indefinitely. Or refuse to start a watchdog without an anchor and let the next dispatch retry.

**Found by inquisition:** dydo/project/inquisitions/agent-deaths.md (Finding #6 — anchor-null orphan).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)