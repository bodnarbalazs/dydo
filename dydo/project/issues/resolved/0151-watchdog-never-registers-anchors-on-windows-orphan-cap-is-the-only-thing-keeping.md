---
title: Watchdog never registers anchors on Windows; orphan-cap is the only thing keeping it alive
area: backend
fix-release: 
needs-human: false
resolution: 
severity: high
status: resolved
work-type: 
id: 151
type: issue
found-by: inquisition
date: 2026-05-01
resolved-date: 2026-07-04
---

# Watchdog never registers anchors on Windows; orphan-cap is the only thing keeping it alive
Watchdog anchor registration silently fails on Windows because `FindAncestorProcess` exact-matches `claude` while Claude Code actually runs as `node`, so dispatchers always pass a null anchor and the anchors directory stays empty. The 24h orphan-cap is the only thing keeping the watchdog alive; once it expires, auto-resume silently stops until the next `dispatch --auto-close` re-spawns the watchdog.
## Description
Root cause for #0150 on Windows. WatchdogService.EnsureRunning calls RegisterAnchor(dydoRoot, ProcessUtils.FindAncestorProcess(``claude``)) (Services/WatchdogService.cs:107). FindAncestorProcess exact-matches the process basename via MatchesProcessName (Services/ProcessUtils.Ancestry.cs:60-63, comment cites #0128). On Windows, claude ships as a Node script and runs as node (the watchdog's own kill-target whitelist documents this asymmetry: ClaudeProcessNames = { ``claude``, ``node`` } at Services/WatchdogService.cs:19-22). The single-token search misses node, so Windows dispatchers always pass a null anchor to RegisterAnchor (which returns immediately at :184). The anchors directory stays empty; ScanAnchors returns 0; hasSeenLiveAnchor never flips true. The orphaned-watchdog cap kicks in at 24h (MaxOrphanAge, :66). Until then auto-resume works; after 24h the watchdog exits with exitReason = max_orphan_age and any subsequent crash silently fails to resume until the next dispatch --auto-close re-spawns a fresh watchdog. ResolveClaimedPid (Services/AgentRegistry.cs:187-189) shares the same Windows blind spot, but its parent-pid fallback partly hides the symptom. Existing tests inject anchors via FindAncestorProcessOverride (e.g. WatchdogServiceTests:1172), which masks the production gap. Suggested fix: a platform-aware FindClaudeAncestor() helper that knows about node on Windows (mirroring ClaudeProcessNames) and is the single source of truth for both anchoring and ClaimedPid capture.
## Reproduction
(Steps to reproduce, if applicable)
## Augmented 2026-05-06 — `claude.exe.old.<unix-ms>` post-update case (inquisition Finding #3)
A third `MatchesProcessName` blind spot surfaced in the same family: after a Claude Code self-update on Windows, the running claude process's image name becomes `claude.exe.old.<unix-ms>` (Claude Code renames the prior `claude.exe` on disk and drops the new binary in place; the OS retains the old image name for the running process's lifetime).
Concrete observation in `dydo/_system/.local/watchdog.log`:
```
{"ts":"2026-05-06T21:09:35Z","event":"start","anchor_pid":57332,"anchor_name":"claude.exe.old.1777935765627",...}
```
Walking `MatchesProcessName` (`Services/ProcessUtils.Ancestry.cs:95-98`):
- `Path.GetFileNameWithoutExtension("claude.exe.old.1777935765627")` strips the last `.<token>` segment → `"claude.exe.old"`.
- `"claude.exe.old".Equals("claude")` → false. `"claude.exe.old".Equals("node")` → false.
Concrete consequences:
- Any new `dydo agent claim` whose claude ancestor is the post-update process fails `FindClaudeAncestor` and either falls back to a non-claude PID or returns null. (Existing anchors written before the rename keep working — the file is keyed by PID, not name.)
- `KillClaudeProcesses` (`Services/WatchdogService.cs:621-622`) filters by `ClaudeProcessNames.Contains(procName)`. A `claude.exe.old.<ts>` target fails the whitelist and the auto-close kill silently no-ops.
Resolution should broaden the matcher to recognise the rename pattern, e.g. a regex equivalent of `^claude(\.exe(\.old\.\d+)?)?$` plus `^node(\.exe)?$`. The Linux/Mac matcher is unaffected (no analogous rename behaviour).
## Resolution
Fixed at HEAD: ProcessUtils.FindClaudeAncestor accepts 'node' on Windows plus the claude.exe.old.<unix-ms> rename regex, and is the single anchoring/ClaimedPid source (ProcessUtils.Ancestry.cs:67-117; WatchdogService.cs:116). Goes live with the 2.0 install. Triage sweep 2026-07-04 (Brian, CoS).