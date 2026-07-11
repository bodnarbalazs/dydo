---
title: codex-windows-sandbox-setup.exe not found - codex sandbox helper missing blocks Start-Process/rg/sandbox-mode commands in dispatched codex sessions
id: 273
area: backend
type: issue
severity: high
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-11
---

# codex-windows-sandbox-setup.exe not found - codex sandbox helper missing blocks Start-Process/rg/sandbox-mode commands in dispatched codex sessions

c1-8 acceptance smoke (2026-07-11, HEAD build): a dispatched codex session under the guard repeatedly failed sandbox-dependent commands with 'windows sandbox: orchestrator_helper_launch_failed: setup refresh failed to launch helper: helper=codex-windows-sandbox-setup.exe ... error=program not found' (log ~/.codex/.sandbox/sandbox.<date>.log). Blocked: Start-Process (the agent's background wait), rg, and any sandbox-mode command. This is codex-SIDE (the sandbox helper exe is absent from this codex install), not dydo - but it BLOCKS codex from doing real workhorse commands, so it is a hard prerequisite for the codex-as-workhorse goal. Also seen in Noah's 2026-07-09 MCP exploration (same helper-not-found). It is the concrete thing c1-4's DefaultSandboxPrerequisite pass-through must eventually probe (issue-adjacent). Fix directions: (1) determine whether the helper ships with the codex CLI and is installable/repairable (codex reinstall, a codex setup command, or a config pointing at the elevated-sandbox path - config shows [windows] sandbox='elevated'); (2) dydo's DefaultSandboxPrerequisite probe should detect the missing helper and fail-fast at DISPATCH with the fix instruction rather than letting the codex session discover it mid-run; (3) document as a codex-host prerequisite. Investigate whether [windows] sandbox setting or an admin-approved first-run resolves it. Found by balazs+Adele in c1-8.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)