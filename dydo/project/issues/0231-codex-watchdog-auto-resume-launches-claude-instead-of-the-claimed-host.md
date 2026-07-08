---
title: Codex watchdog auto-resume launches Claude instead of the claimed host
id: 231
area: backend
type: issue
severity: high
status: in-flight
found-by: inquisition
found-by-agent: Adele
found-by-vendor: unknown
found-by-model: unknown
date: 2026-07-07
---

# Codex watchdog auto-resume launches Claude instead of the claimed host

Watchdog recovery reads Codex sessions but drops session.Host and calls claude --resume on every platform.

## Description

Inquisition finding: TryReadResumeContext reads the .session file, but ResumeContext only carries AgentName and SessionId, not Host. PollAndResumeForAgent then calls TerminalLauncher.LaunchResumeTerminal without a host argument. WindowsTerminalLauncher, LinuxTerminalLauncher, and MacTerminalLauncher hardcode claude --resume in their resume bodies. Consequence: a crashed Codex agent is resumed as Claude, or fails under Codex-only setups, so Codex is not first-class for auto-resume. Evidence: Services/WatchdogService.cs ResumeContext/TryReadResumeContext/PollAndResumeForAgent; Services/TerminalLauncher.cs LaunchResumeTerminal; Services/*TerminalLauncher.cs resume command builders.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)
