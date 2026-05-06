---
id: 151
area: backend
type: issue
severity: high
status: open
found-by: inquisition
date: 2026-05-01
---

# Watchdog never registers anchors on Windows; orphan-cap is the only thing keeping it alive

Watchdog anchor registration silently fails on Windows because `FindAncestorProcess` exact-matches `claude` while Claude Code actually runs as `node`, so dispatchers always pass a null anchor and the anchors directory stays empty. The 24h orphan-cap is the only thing keeping the watchdog alive; once it expires, auto-resume silently stops until the next `dispatch --auto-close` re-spawns the watchdog.

## Description

Root cause for #0150 on Windows. WatchdogService.EnsureRunning calls RegisterAnchor(dydoRoot, ProcessUtils.FindAncestorProcess(``claude``)) (Services/WatchdogService.cs:107). FindAncestorProcess exact-matches the process basename via MatchesProcessName (Services/ProcessUtils.Ancestry.cs:60-63, comment cites #0128). On Windows, claude ships as a Node script and runs as node (the watchdog's own kill-target whitelist documents this asymmetry: ClaudeProcessNames = { ``claude``, ``node`` } at Services/WatchdogService.cs:19-22). The single-token search misses node, so Windows dispatchers always pass a null anchor to RegisterAnchor (which returns immediately at :184). The anchors directory stays empty; ScanAnchors returns 0; hasSeenLiveAnchor never flips true. The orphaned-watchdog cap kicks in at 24h (MaxOrphanAge, :66). Until then auto-resume works; after 24h the watchdog exits with exitReason = max_orphan_age and any subsequent crash silently fails to resume until the next dispatch --auto-close re-spawns a fresh watchdog. ResolveClaimedPid (Services/AgentRegistry.cs:187-189) shares the same Windows blind spot, but its parent-pid fallback partly hides the symptom. Existing tests inject anchors via FindAncestorProcessOverride (e.g. WatchdogServiceTests:1172), which masks the production gap. Suggested fix: a platform-aware FindClaudeAncestor() helper that knows about node on Windows (mirroring ClaudeProcessNames) and is the single source of truth for both anchoring and ClaimedPid capture.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)