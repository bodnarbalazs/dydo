---
title: Codex-hosted agents cannot satisfy the guard's stateful protocols - read-tracking, general wait, and release are Claude-tool-coupled
id: 254
area: backend
type: issue
severity: high
status: resolved
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

Resolved jointly across sprint C1 slices c1-1 (read verb) and c1-2 (durable wait), the two halves
of the stateful-protocol fix:

**c1-1 — host-agnostic read tracking (items 1, 2):** `dydo read <file-or-message-id>` prints the
content AND registers the read (display-equals-ack) from any host, reusing the existing
`GuardCommand.TrackReadCompletion` logic extracted into `Services/ReadTrackingService.cs`. A codex
agent registers reads without Claude's Read tool, so must-reads and inbox items clear, unwedging
`dydo inbox clear` and release.

**c1-2 — durable wait registration (item 3):** `dydo wait --register` writes a durable general-wait
marker keyed to the claimed session's host-liveness PID (via `WaitCommand.ResolveHostLivenessPid`)
rather than the wait process's lifetime, and returns immediately. `--register` is the required
form on a codex host under the guard: a plain foreground `dydo wait` is H20-blocked there before it
runs (the codex hook input carries no run_in_background field, so it can never satisfy the
backgrounding rule). Durable mode is also auto-selected at the CLI level when the caller's session
host cannot hold a foreground wait (resolved from `AgentSession.Host`), covering the ungated path.
The guard's
`MissingGeneralWait` check accepts it (liveness is encoded in the marker's Pid); a dead host makes
the marker stale and it is cleaned by the same self-heal sweep that removes a dead foreground wait;
`dydo agent release` and `dydo wait --cancel` remove it. Claude-host foreground behavior is
unchanged.

**Items 4 (sandbox setup fail-fast), 5 (whoami host/model, manual-claim onboarding prose)** are
routed to other C1 slices (c1-4 preflight, c1-6 provenance display, c1-1/c1-2 onboarding docs) per
the sprint plan and are tracked there, not here.

Message delivery on codex hosts stays poll-based (`dydo inbox show` / `dydo read`); the durable
marker satisfies the guard protocol without simulating push (push parity is explicitly
out-of-scope, deferred to `backlog/codex-mcp-delegation-experiment.md`).