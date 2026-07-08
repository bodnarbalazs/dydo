---
title: Wait host-liveness uses bare IsProcessRunning and ClaimedPid can be null for a live claim
id: 229
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

# Wait host-liveness uses bare IsProcessRunning and ClaimedPid can be null for a live claim

WaitCommand.ResolveHostLivenessPid checks host liveness with a bare IsProcessRunning PID check, so a recycled host PID can keep a wait alive forever -- the same PID-reuse class fixed for the watchdog in issue 228; additionally ClaimedPid can be null for a live claim (not only legacy sessions) when both the ancestor walk and parent-PID probe fail, which re-enters the fresh-ancestry-walk fallback the 224 fix moved away from. Harden the wait host-liveness the same way as the watchdog pidfile check.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)