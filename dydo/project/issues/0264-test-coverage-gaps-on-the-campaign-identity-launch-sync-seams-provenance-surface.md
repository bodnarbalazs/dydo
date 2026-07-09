---
title: Test-coverage gaps on the campaign identity/launch/sync seams (provenance surfaces, reset wiring, vanished-doc fallback, watchdog resume resolver, ancestry real-walk)
id: 264
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

# Test-coverage gaps on the campaign identity/launch/sync seams (provenance surfaces, reset wiring, vanished-doc fallback, watchdog resume resolver, ancestry real-walk)

Five confirmed low-severity coverage gaps where a regression passes the suite: issue-create/review provenance hijack (only task-create pinned), notion reset command wiring, shadow-promotion vanished-doc fallback, watchdog resume with unresolvable executable, and the FindClaude/CodexAncestor real classification walk.

## Description

Five low-severity test-coverage gaps sit on the identity/launch/sync seams this campaign hardened. Each leaves a real behavior unpinned; a regression would pass the suite. Verify-only — reproduce hypotheses, do not land fixes here.

**1. Provenance ownership soft gate — only task-create has a hijack regression test** (`IssueCreateHandler.cs:127-132`, `ReviewCommand.cs:69-71`)
f7e87516 gated all three provenance surfaces (issue/task/review) on `GetCurrentOwnedAgent`, but `IdentityHijackMutatingCommandTests.cs:149` pins the unowned-shared-context behavior for TaskCreate only. No test that an unowned caller produces unstamped found-by provenance on `dydo issue create` or unattributed `dydo review`. A regression at either site (back to `GetCurrentAgent`, or `GetSessionContext`→`GetAmbientSessionContext` swap — the latter DOES re-open the hole, since `GetAmbientSessionContext`'s file fallback at `AgentRegistry.cs:1295` has no ownership gate) fails no test. Also: no test pins the nested-foreign-worker case for ANY provenance surface, so the intended asymmetry (`GetCurrentOwnedAgent` lacks the nearest-host check msg/dispatch got) is undocumented.

**2. `dydo notion reset` command wiring (`--yes` vs confirm prompt) — no e2e test** (`Commands/NotionCommand.cs:163-175`)
`NotionResetTests` test `NotionReset.Execute` with an injected confirm delegate, but the command layer binding `--yes`→auto-confirm and default→`ConfirmYesNo` is untested (`NotionCommandTests.cs:119` only asserts options exist). The analogous reveal-token guard IS wired-tested e2e (`NotionCommandTests.cs:171/187`). An inverted/dropped confirm binding on `RunReset` would auto-confirm a destructive board wipe with zero test failures; the fail-closed vault `ResolveToken` path is also never covered via reset.

**3. Shadow-promotion vanished-doc fallback branch untested** (`Sync/Notion/DocsTreeSync.cs:152-155`)
The fallback restoring a doc that vanished from the tree while its resolution was pending (stem-derived path, `pathByLocalId.GetValueOrDefault` miss) is never evaluated: every `DocsShadowConflictTests` promotion test keeps the canonical file on disk, so the map always hits. `File.Delete(shadowFile)` at line 164 runs unconditionally after the write, so a regressed fallback (wrong separator, dropped branch) writes a human's completed resolution to a junk path or loses it silently. No test deletes a non-root doc with a resolved shadow pending and asserts restoration to `dydo/<stem>.md`.

**4. Watchdog resume with an unresolvable launch executable untested** (`Services/WatchdogService.cs:571-581`)
Cross-campaign seam between de0d63f (platform launch resolution — WindowsApps codex alias throws) and host-aware resume. DispatchService has a no-mutation pre-flight AND a test (`DispatchCommandTests.cs:200`); `PollAndResumeForAgent` calls `LaunchResumeTerminal` with no resolution pre-flight, and every `WatchdogServiceTests` resume test injects `LaunchResumeOverride` — the real launcher path with a codex host that resolves only to the rejected alias is never exercised. The composed behavior (resolution throw → catch → pid 0 → attempts already incremented at `WatchdogService.cs:554` → cap burned → gave_up) is plausible-by-reading but pinned by no test; a regression that instead wedged the poll loop or double-emitted outcomes would be invisible. `PollAndResumeCrashedAgents` (`:473-479`) has no per-agent isolation, so a propagating regression skips remaining agents every tick.

**5. `FindClaudeAncestor`/`FindCodexAncestor` real-walk vendor discrimination + unreadable-node acceptance untested** (`Services/ProcessUtils.Ancestry.cs:114-119, 183-188`)
The 0250 claim-time walks are tested only for the happy shim-skip path. Untested: (a) a node ancestor whose command line names the OTHER vendor must be skipped, not returned (only `NoForeignHostNearerThanClaimedHost` has the codex-by-cmdline test, not the ClaimedPid-capturing walk); (b) `AmbiguousNode` acceptance — an unreadable node cmdline above the launcher position returned as the claimed host. `ClaimedPid` feeds ownership gates, the watchdog kill whitelist, and crash-resume liveness (#0151 shared source of truth), so a regression in either branch mis-anchors identity with no failing test.

Found by the v2.0.6 campaign inquisition (coverage lens); adversarially verified.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)