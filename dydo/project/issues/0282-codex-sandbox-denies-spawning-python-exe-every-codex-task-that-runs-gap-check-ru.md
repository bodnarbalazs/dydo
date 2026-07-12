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

**Diagnosis confirmed (Henry, codex, 2026-07-11):** the elevated workspace-write sandbox cannot
read/execute the real interpreter at `C:\Users\User\AppData\Local\Programs\Python\Python313\python.exe`
(outside the workspace), so `gap_check.py`/`run_tests.py` fail with Windows access denied. The
documented codex mechanism to allow it: add the Python dir as a `writable_roots` entry under
`[sandbox_workspace_write]` in `~/.codex/config.toml` (keeps `windows.sandbox = "elevated"`, does NOT
enable danger-full-access). Codex's OWN auto-approval-review DENIED the edit pending explicit human
authorization (correctly — it is a persistent host-wide security-setting change).

**Decision (balazs, 2026-07-11):** [PENDING — record his choice]

**CoS analysis / recommendation:** the change is correctly diagnosed but is a persistent, HOST-WIDE
(all projects) *write* grant to the Python install dir — a directory OUTSIDE the dydo guard's reach
(the guard governs the repo, not ~/.codex or system dirs), so a compromised/misbehaving codex task
could tamper the interpreter with no guard visibility. It is also broader than the need (python needs
read+execute, `writable_roots` grants write). CRUCIALLY it is NOT a hard swarm gate: the Claude
reviewer already runs the full suite + gap_check on every review (verified — the 0277-r2 reviewer ran
4760/4760 + gap_check independently), so codex not running python only loses SELF-verification (a
broken fix costs one review round, caught by the reviewer). **Recommendation: DECLINE the expansion —
let the reviewer be the gate; keep the sandbox tight.** If self-verification efficiency is wanted at
scale, ACCEPT with narrowing: (a) project-scope it (`[projects.'...dynadocs'.sandbox_workspace_write]`)
not global, and (b) read-only/exec-roots if codex supports it, not `writable_roots`.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)