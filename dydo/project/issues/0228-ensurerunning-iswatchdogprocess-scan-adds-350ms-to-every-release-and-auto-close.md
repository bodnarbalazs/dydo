---
title: EnsureRunning IsWatchdogProcess scan adds ~350ms to every release and auto-close dispatch
id: 228
area: backend
type: issue
severity: medium
status: open
found-by: manual
found-by-agent: Emma
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-07
---

# EnsureRunning IsWatchdogProcess scan adds ~350ms to every release and auto-close dispatch

EnsureRunning verifies the pidfile PID via a full process command-line scan on its fast path, costing ~350ms on every dydo agent release and every --auto-close dispatch; replace with an in-process command-line read (P/Invoke PEB via ReadProcessMemory, matching the existing NtQueryInformationProcess usage) so the common watchdog-alive case pays no subprocess-spawn cost.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)