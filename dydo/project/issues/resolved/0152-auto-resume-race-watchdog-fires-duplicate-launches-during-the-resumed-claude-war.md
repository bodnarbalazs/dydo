---
title: Auto-resume race: watchdog fires duplicate launches during the resumed-claude warmup gap, exhausting the resume-attempts cap
area: backend
fix-release: 
needs-human: false
resolution: 
severity: high
status: resolved
work-type: 
id: 152
type: issue
found-by: inquisition
date: 2026-05-01
resolved-date: 2026-07-04
---

# Auto-resume race: watchdog fires duplicate launches during the resumed-claude warmup gap, exhausting the resume-attempts cap
Between the watchdog's `LaunchResume` and the resumed claude's first `dydo agent claim`, `state.ClaimedPid` still references the dead pre-crash PID, so each subsequent poll re-detects "process gone" and fires another resume launch. A single noisy crash can saturate the resume-attempts cap before the agent ever recovers, silencing every future crash on that session.
## Description
Finding 2 from auto-resume inquisition. RefreshClaimedPid (#0143 fix) runs only after the resumed claude calls dydo agent claim, inside the same-sessionId branch of HandleExistingSession (Services/AgentRegistry.cs:321-329). Between the watchdog's LaunchResume (Services/WatchdogService.cs:415-484) and that first claim, .session.ClaimedPid still references the dead pre-crash PID. claude --resume rehydration takes tens of seconds on real conversations, much longer than the 10s default poll interval. During the gap each tick re-evaluates: status=working ✓, IsProcessRunning(deadPid)=false ✓, attempts < cap ✓ → another IncrementResumeAttempts + LaunchResume. Test PollAndResumeForAgent_RepeatedPolls_StopAtCap (DynaDocs.Tests/Services/WatchdogServiceTests.cs:1454-1470) directly demonstrates the loop: 6 polls with a stuck dead PID produce 3 launches and ResumeAttempts=3. Visible symptom: 1-3 redundant terminals (the symptom #0143 was filed for, only partially mitigated). Silent symptom: resume-attempts reaches the cap on a single noisy crash, so subsequent unrelated crashes never resume — compounding Finding 3 (#0152). Suggested fix: have LaunchResumeTerminal (or its caller) write a placeholder ClaimedPid (the spawned wt.exe / shell PID) immediately so the next tick sees a live PID until the resumed claude refreshes it. Alternative: IncrementResumeAttempts writes a last-resume-launched-at field; PollAndResumeForAgent skips agents whose last launch is within ~60s.
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed at HEAD via the suggested mitigation: IncrementResumeAttempts stamps LastResumeLaunchedAt and PollAndResumeForAgent skips agents inside the ResumeWarmupGate window (AgentRegistry.cs:186-194, 1398), preventing duplicate launches during resumed-claude warmup. Goes live with the 2.0 install. Triage sweep 2026-07-04 (Brian, CoS).