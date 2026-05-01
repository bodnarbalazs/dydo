---
id: 154
area: backend
type: issue
severity: medium
status: open
found-by: inquisition
date: 2026-05-01
---

# Linux/Mac watchdog dies via anchor_gone when all dispatchers exit while leaf dispatched agents are still alive

## Description

Finding 4 from auto-resume inquisition. On Linux/Mac the binary name is claude so FindAncestorProcess(``claude``) succeeds and the dispatcher's claude is registered as an anchor. But RegisterAnchor is called only from EnsureRunning (Services/WatchdogService.cs:107), and EnsureRunning is invoked from only two sites: Services/DispatchService.cs:213 (only when --auto-close is set on dispatch) and Commands/AgentLifecycleHandlers.cs:80 (release). Neither fires on a dispatched agent's claim — only on its dispatcher's actions. Dispatched agents do not anchor themselves. Common scenario: orchestrator dispatches a leaf code-writer with --auto-close. Watchdog starts, anchor = orchestrator's claude. Orchestrator releases and exits before the leaf finishes. Anchor count → 0 → WatchdogService.cs:312 anchor_gone exit. Leaf crashes → no watchdog → no resume. (On Windows this finding adds nothing because Finding 1 already disables anchoring; this is real and silent on Linux/Mac.) Suggested fix: also register an anchor when dydo agent claim succeeds for a dispatched agent — plumb EnsureRunning (or just RegisterAnchor) into SetupAgentWorkspace for the dispatched-or-queued path, so watchdog lifetime is bound to the population of working agents, not just dispatchers.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)