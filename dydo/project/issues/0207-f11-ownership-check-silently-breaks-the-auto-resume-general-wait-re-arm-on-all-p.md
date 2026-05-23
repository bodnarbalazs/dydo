---
id: 207
area: backend
type: issue
severity: high
status: open
found-by: inquisition
date: 2026-05-21
---

# F11 ownership check silently breaks the auto-resume general-wait re-arm on all platforms

Slice A's #0195/F11 VerifyCallerOwnsAgent gate refuses the backgrounded 'dydo wait' the resume launchers spawn, so an auto-resumed agent comes up with no general wait and the failure is silently swallowed.

## Description

Regression introduced by identity-hijack Slice A (commit b9a94f6, WaitCommand.cs:47-56).

All three resume launchers spawn a backgrounded 'dydo wait' to re-arm the general wait for the resumed claude (WindowsTerminalLauncher.cs:98, LinuxTerminalLauncher.cs:61, MacTerminalLauncher.cs:63). Per Decision 022 (which depends on Decision 021) this backgrounded wait IS the resumed session's general wait — the resumed claude is told to 're-orient and continue', not to re-arm or re-claim.

That re-arm 'dydo wait' is a child of the resume shell; 'claude --resume' is a later sibling in the same command body, so ProcessUtils.FindClaudeAncestor() returns null for the wait process. Meanwhile the agent's .session.ClaimedPid still holds the dead pre-resume claude PID (RefreshClaimedPid only runs on the next ClaimAgent). The new gate WaitCommand.Execute -> AgentRegistry.VerifyCallerOwnsAgent -> IsOwnedByCaller therefore returns false, and WaitCommand returns ExitCodes.ToolError before WaitGeneral — no wait marker is registered. The launchers discard output (| Out-Null, >/dev/null 2>&1), so the 'Caller does not own agent' error is silent.

The resumed agent's own subsequent 'dydo wait' is refused for the same reason (ClaimedPid still dead) until it runs 'dydo agent claim' — which the Decision 022 resume flow does not prescribe. Net effect: auto-resume (Decision 022) is non-functional on every platform; a resumed agent is blocked by the guard's missing-general-wait rule and cannot self-recover without an out-of-flow re-claim.

Reproduced by test-writer Henry: DynaDocs.Tests/Services/AutoResumeRearmWaitGateTests.cs (2/2 pass; full suite 4264/4264). Invisible to Slice A's own tests because IntegrationTestBase.cs:54 pins FindAncestorProcessOverride => Environment.ProcessId, so every test caller trivially owns its agent.

Fix before merging Slice A to master. Likely fix: have the launchers pass DYDO_AGENT-ownership context the re-arm wait can trust, or exempt the launcher-spawned re-arm from the F11 gate while keeping the attacker case (non-claude shell, no resume context) blocked.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)