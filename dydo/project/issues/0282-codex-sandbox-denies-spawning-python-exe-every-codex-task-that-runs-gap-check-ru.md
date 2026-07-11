---
title: Codex sandbox denies spawning python.exe - every codex task that runs gap_check/run_tests needs a manual escalation approval (per-task swarm friction)
id: 282
area: backend
type: issue
severity: medium
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-11
---

# Codex sandbox denies spawning python.exe - every codex task that runs gap_check/run_tests needs a manual escalation approval (per-task swarm friction)

Observed repeatedly 2026-07-11 (Henry, Sam, likely all codex code tasks): a dispatched codex agent's workspace-write sandbox DENIES spawning the Python process for the test/coverage gates - 'sandboxed py failed with Windows access denied creating the Python process' - so the agent must ESCALATE (a manual approval) to run python DynaDocs.Tests/coverage/gap_check.py or run_tests.py. Auto mode covers file edits + normal commands, but the sandbox blocks python.exe process creation, so EVERY codex task that runs the gates hits one approval prompt. Per-task friction that adds up across a swarm (also seen: escalation for outbound network fetch of the codex manual - missing x-content-sha256). Root cause: codex workspace-write sandbox process-spawn policy for python.exe (and possibly other interpreters/tools). FIX DIRECTIONS: (1) codex sandbox config - allow python (and the repo's test toolchain) as trusted spawn targets in workspace-write, or add the test dir to the writable/executable roots; (2) determine whether the elevated sandbox (0273 now fixed) should permit interpreter spawns and why it still denies; (3) document the trusted-command set codex needs for this repo's gates as part of the codex-host setup. Related: 0273 (sandbox), 0281 (trust). Should be reduced before the swarm so codex tasks self-gate without per-run approvals. Found by balazs+Adele.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)