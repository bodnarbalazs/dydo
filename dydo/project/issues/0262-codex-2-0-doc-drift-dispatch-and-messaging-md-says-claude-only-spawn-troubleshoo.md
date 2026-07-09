---
title: Codex/2.0 doc-drift: dispatch-and-messaging.md says claude-only spawn; troubleshooting.md says 'claude --resume' and greps removed audit files
id: 262
area: guides
type: issue
severity: low
status: open
found-by: inquisition
found-by-agent: Leo
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-09
---

# Codex/2.0 doc-drift: dispatch-and-messaging.md says claude-only spawn; troubleshooting.md says 'claude --resume' and greps removed audit files

Three doc surfaces the in-range 0232 codex sweep and 2.0 audit-removal missed: the concept doc claims claude-only dispatch (code is host-aware), and the recovery guide tells a human to run claude --resume for Codex agents and to grep the removed dydo/_system/audit/*.events files as a safety check before a destructive force-clean.

## Description

Three doc surfaces mislead operators/agents about host-aware dispatch and post-2.0 recovery. The in-range 0232 codex doc-surface sweep (dc1333b9) and the 2.0 audit-removal both missed these.

**1. `dydo/understand/dispatch-and-messaging.md:41` — claude-only spawn** (also Key options list :20-29 omits `--codex`/`--claude`)
States: "Dispatch launches a new terminal with `claude \"<agentName> --inbox\"`". Code is host-aware: `DispatchService.ResolveLaunchHost` (`DispatchService.cs:74-85`) returns codex or claude (override > caller-session host > claude fallback), threaded through `TerminalLauncher.GetLaunchExecutable` per platform (de0d63f6). Flags exist (`DispatchCommand.cs:70-77`) and are documented in `dydo-commands.md:289-292`, so the concept doc contradicts both code and the reference doc. Pre-existing drift (codex launch landed pre-v2.0.5) but exactly the surface class dc1333b9 was meant to close.

**2. `dydo/guides/troubleshooting.md:172` — `claude --resume` wrong for Codex** (cause text :152 also assumes "the agent's Claude process")
Recovery step 1 for a stuck-Working agent says "run `claude --resume` in it". de0d63f6 made watchdog resume host-aware precisely because Codex has no root-level `--resume` flag — the correct form is `codex resume <session-id>` (`TerminalLauncher.cs:141-149`, doc comment cites #0231). A human recovering a crashed Codex agent runs a nonexistent flag form and recovery fails.

**3. `dydo/guides/troubleshooting.md:161-168` — greps removed audit-trail files**
The "Verify release really didn't run" recipe (a safety check BEFORE the destructive `dydo agent clean <name> --force`, :177) greps `dydo/_system/audit/2026/*.events` for Claim/Release events. The audit writer was removed in 2.0 (Decision 024; `architecture.md:79`: "the dydo/_system/audit/ tree is legacy"). No post-2.0 session writes `.events` files, so the documented safety check silently finds nothing (or falsely matches a stale pre-2.0 session for a reused agent name) before a destructive action. Pre-existing relative to v2.0.5 and outside the four in-scope bodies, but reported because it gates a destructive operation on a now-impossible verification.

Found by the v2.0.6 campaign inquisition (doc-drift lens); adversarially verified.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)