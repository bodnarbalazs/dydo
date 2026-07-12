---
title: EnsureRunning IsWatchdogProcess scan adds ~350ms to every release and auto-close dispatch
id: 228
area: backend
type: issue
severity: medium
status: resolved
found-by: manual
found-by-agent: Emma
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-07
resolved-date: 2026-07-12
---

# EnsureRunning IsWatchdogProcess scan adds ~350ms to every release and auto-close dispatch

EnsureRunning verifies the pidfile PID via a full process command-line scan on its fast path, costing ~350ms on every dydo agent release and every --auto-close dispatch; replace with an in-process command-line read (P/Invoke PEB via ReadProcessMemory, matching the existing NtQueryInformationProcess usage) so the common watchdog-alive case pays no subprocess-spawn cost.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

RESOLVED 2026-07-12: fix landed 28255081 (second swarm fix). WatchdogService.IsWatchdogProcess now reads ONLY the pidfile PID's command line (Windows: PEB via NtQueryInformationProcess + ReadProcessMemory, OS-guarded; non-Windows: existing ProcessUtils.GetProcessCommandLine /proc|ps) instead of scanning ALL processes - saves ~350ms per release/auto-close. Same case-insensitive 'watchdog run' criterion; fails SAFE (unreadable -> not-watchdog -> respawn, never a false 'alive' that disables auto-close). Codex Quinn (gpt-5.6-terra, 2 rounds: r1 impl empirically verified correct/leak-free by the reviewer but FAILED on test-coverage - the new tests stubbed the native path; r2 ~5min added self-read smoke + null-path tests exercising the REAL code). Claude-reviewed PASS (gates 4764/175 green). Non-blocking residual: the P/Invoke extern is duplicated with ProcessUtils.Ancestry (dedup candidate, part of 0263's territory).