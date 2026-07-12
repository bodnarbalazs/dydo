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

## Attempted + deferred — needs claim-time signature (2026-07-13)

Batch 3 swarm attempt: harden `ResolveHostLivenessPid` like #0228 by requiring the ClaimedPid's command line to `Contains(session.Host)`. Adversarial review FAILED it: `ClaimedPid` is BY DESIGN sometimes NOT the host binary — `AgentRegistry.ResolveClaimedPid` (AgentRegistry.cs:210-216) falls back to the immediate PARENT SHELL PID (pwsh/bash) when no host ancestor is found, and `Host` normalizes to `"unknown"` for unrecognized hosts. A `pwsh`/`bash` parent's cmdline does not contain "codex"/"claude" → liveness resolves DEAD → durable registration always errors → a GENUINE live dispatched codex session (the exact population the durable wait serves) is WEDGED (guard demands a wait it can never register). The watchdog's #0228 fix worked only because its pidfile PID is always a `dydo watchdog run` process — that guarantee does NOT transfer here. Also broke 3+ unmodified E2E tests deterministically (CodexClaimE2ETests, CodexReleaseLoopE2ETests, Wait_CodexHost_*) — the implementer only ran its 6 new tests, not the suite. REVERTED.
KEEP (were correct): the fail-safe direction (unverifiable → dead/reclaimable) and the 4 new override-driven tests.
REDESIGN NEEDED: persist a claim-TIME cmdline/signature of the actual claimed process and compare against THAT (not a naive host-name Contains), handling the parent-shell fallback distinctly; OR explicitly redefine + migrate the ClaimedPid-fallback contract. Update the affected E2Es to the new contract. Residual (separate follow-up): post-registration liveness (GuardCommand durable-marker check, WaitCommand IsDead) is still bare IsProcessRunning — a PID recycled AFTER registration still reads alive.

## Resolution

(Filled when resolved)