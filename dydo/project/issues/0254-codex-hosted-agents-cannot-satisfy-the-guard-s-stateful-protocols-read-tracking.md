---
title: Codex-hosted agents cannot satisfy the guard's stateful protocols - read-tracking, general wait, and release are Claude-tool-coupled
id: 254
area: backend
type: issue
severity: high
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-09
---

# Codex-hosted agents cannot satisfy the guard's stateful protocols - read-tracking, general wait, and release are Claude-tool-coupled

Codex dispatch smoke exit report (Iris, 2026-07-09): the simple path works (claim via codex hook, role, msg with vendor/model tags) but every STATEFUL guard protocol failed on a codex host. (1) Read tracking: dydo marks messages/must-reads read by observing Claude's Read tool via hook; codex reads files via shell (Get-Content), which never registers - unread state persists after genuine reads. (2) Consequence: dydo inbox clear --all permanently blocked ('read them first'), which blocks normal release - the agent is wedged and needs a human force-clean. (3) dydo wait is unusable: foreground dies to the codex tool timeout, Start-Process backgrounding creates no .waiting marker, so the guard then blocks reads with 'must keep a general wait active'. (4) Setup: codex-windows-sandbox-setup.exe not found, forcing approval escalation for even read-only commands (compounds 0253). (5) Minor: whoami prints no host/model field (0223-adjacent); dispatch briefs must state that claim is a manual onboarding step. Fix directions: a CLI ack path for reads (e.g. dydo inbox read <id> / dydo ack) usable from any host; a durable wait registration mode for codex (marker-file based, not process-lifetime based); whoami host/model display. Route: sprint C1 - this is THE blocker for codex-as-implementer under guard.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)