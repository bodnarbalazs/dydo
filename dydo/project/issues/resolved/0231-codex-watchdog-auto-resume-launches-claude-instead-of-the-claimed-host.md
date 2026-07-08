---
title: Codex watchdog auto-resume launches Claude instead of the claimed host
id: 231
area: backend
type: issue
severity: high
status: resolved
found-by: inquisition
found-by-agent: Adele
found-by-vendor: unknown
found-by-model: unknown
date: 2026-07-07
resolved-date: 2026-07-08
---

# Codex watchdog auto-resume launches Claude instead of the claimed host

Watchdog recovery reads Codex sessions but drops session.Host and calls claude --resume on every platform.

## Description

Inquisition finding: TryReadResumeContext reads the .session file, but ResumeContext only carries AgentName and SessionId, not Host. PollAndResumeForAgent then calls TerminalLauncher.LaunchResumeTerminal without a host argument. WindowsTerminalLauncher, LinuxTerminalLauncher, and MacTerminalLauncher hardcode claude --resume in their resume bodies. Consequence: a crashed Codex agent is resumed as Claude, or fails under Codex-only setups, so Codex is not first-class for auto-resume. Evidence: Services/WatchdogService.cs ResumeContext/TryReadResumeContext/PollAndResumeForAgent; Services/TerminalLauncher.cs LaunchResumeTerminal; Services/*TerminalLauncher.cs resume command builders.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in de0d63f: ResumeContext carries the session's Host (normalized from the .session file); the watchdog threads it to LaunchResumeTerminal and all three platform resume builders emit the claimed host - claude keeps 'claude --resume <id>', codex uses the documented subcommand form 'codex resume <id> <prompt>' (the Codex CLI has no root-level --resume flag, per the official CLI reference). CAVEAT: whether live Codex hook payloads deliver a codex-resumable session_id remains unverified on this workstation (codex install is the broken WindowsApps alias); the resume path rides on the 0233 post-release live-Codex smoke test, per CoS decision 2026-07-08.
