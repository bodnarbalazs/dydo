---
title: Cross-rewrite dead code / dead-in-effect surfaces in TerminalLauncher, AgentRegistry, NotionMarkdownResponse, WatchdogService
id: 263
area: backend
type: issue
severity: low
status: open
found-by: inquisition
found-by-agent: Leo
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-09
---

# Cross-rewrite dead code / dead-in-effect surfaces in TerminalLauncher, AgentRegistry, NotionMarkdownResponse, WatchdogService

The codex-hardening launch/resume rewrite and DR-035 chain left five dead-in-effect API surfaces that read as load-bearing: GetClaudeResumeCommand (production-dead), GetClaudeCommand/GetCodexCommand (triplicated launch-format logic), GetCurrentOwnedAgent's now-unreachable ownership re-check, NotionMarkdownResponse.UnknownBlockIds (never surfaced despite its doc), and a stale watchdog anchor comment describing removed name-based node acceptance.

## Description

Cross-rewrite hygiene: the codex-hardening launch/resume rewrite (de0d63f6) and the DR-035 chain left dead-in-effect API surfaces that read as load-bearing. None is a runtime defect; each is a divergence trap.

**1. `GetClaudeResumeCommand` — production-dead, test-only** (`Services/TerminalLauncher.cs:149`)
`public static string GetClaudeResumeCommand(string sessionId) => GetBareResumeCommand(sessionId, "claude");`. At v2.0.5 its one production caller was the LaunchResume manual-fallback hint (old line 273); de0d63f6 replaced that callsite with `GetBareResumeCommand(sessionId, launchHost)` (now :388) but kept the public claude-hardcoded wrapper. Only remaining reference: `TerminalLauncherTests.cs:2983`. Its claude-only shape contradicts the host-aware resume the same commit introduced.

**2. `GetClaudeCommand` / `GetCodexCommand` — dead, triplicated logic** (`TerminalLauncher.cs:166-176`)
Both return `$"{host} \"{prompt}\""`. Already test-only at v2.0.5; de0d63f6 relocated them AND added private `GetBareLaunchCommand` (`:120-124`) that duplicates their exact logic host-parameterized (the only production-live copy, called at :350). Three copies of the launch-command format, two production-dead — a divergence trap; the same commit already forked the resume format by host, and known-open 0253 targets the bare-codex shape these dead helpers pin via tests. Only references: `TerminalLauncherTests.cs:109-122`.

**3. `GetCurrentOwnedAgent` ownership re-check is unreachable-false in production** (`AgentRegistry.cs:1101-1106`)
f7e87516 (0230) added the `VerifyCallerOwnsAgent` soft gate against the then-ungated `GetSessionContext`. 7805e004 (0250) then ownership-gated `GetSessionContext` itself. All three callsites (`ReviewCommand.cs:70-71`, `IssueCreateHandler.cs:129-130`, `TaskCreateHandler.cs:19-20`) feed it a sessionId from the now-gated `GetSessionContext`, and `GetCurrentAgent` resolves strictly by session-id match — so the inner check can no longer return false in steady state. Harmless today, but it reads as a load-bearing security gate while dead-in-effect. Either adopt the ambient+`TryGetCurrentOwnedAgent` pattern (as MessageService/DispatchService correctly do, where the refusal is live) or acknowledge/remove the redundant re-check. NOTE: this interacts with issue #256 — if the DYDO_AGENT env fix lands, revisit whether these callsites still need the nearest-host recheck.

**4. `NotionMarkdownResponse.UnknownBlockIds` never surfaced** (`Sync/Notion/Dtos/NotionMarkdownResponse.cs:26-27`, from 85674a76)
Deserialized from `GET /pages/{id}/markdown`; its XML doc (line 13) says it is "surfaced for diagnostics", but no production code reads it — `DocsPageAdapter.ReadPage` consumes only `Markdown` and `Truncated`; sole reference is a wire-shape assertion at `NotionClientTests.cs:219`. A page silently dropping unrenderable blocks loses content with no warning while the comment implies one exists. Either wire up the diagnostic or drop the field and claim.

**5. Watchdog anchor comment describes removed name-based node fallback** (`Services/WatchdogService.cs:119-123`)
Comment says `FindClaudeAncestor` "also accept[s] the node parent name on Windows (#0151)". 7805e004 replaced that name-based acceptance with command-line classification that deliberately skips a codex host (`ProcessUtils.Ancestry.cs:131-145`), so the code no longer does what the comment claims. Behavioral edge: pre-0250 a Windows codex-hosted dispatcher registered an anchor here; post-0250 `FindClaudeAncestor` returns null for it and dispatch-time registration is skipped. Bounded — claim-time `RegisterMainAnchor` uses host-aware `FindAgentHostAncestor`, so claimed codex agents still anchor — but this dispatch-time site was left claude-only when the two campaigns crossed, and its comment misdescribes the classification underneath it.

Found by the v2.0.6 campaign inquisition (dead-code lens); adversarially verified.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)