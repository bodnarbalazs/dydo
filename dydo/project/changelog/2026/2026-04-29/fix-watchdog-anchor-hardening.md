---
area: general
type: changelog
date: 2026-04-29
---

# Task: fix-watchdog-anchor-hardening

Review commit 762eeda for fix-watchdog-anchor-hardening (#0126/#0127/#0128/#0131/#0132). Plan: dydo/agents/Frank/plan-watchdog-anchor-hardening.md. Brief: dydo/agents/Brian/brief-fix-watchdog-anchor-hardening.md (or Kate's inbox copy at dydo/agents/Kate/inbox/8060bde9-fix-watchdog-anchor-hardening.md). Files: Services/ProcessUtils.Ancestry.cs (exact-match + MatchesProcessName extraction), Services/WatchdogService.cs (anchors directory + RegisterAnchor + ScanAnchors + ResolveAnchors + 24h cap + extended ShellProcessNames + --inbox collision-safety comment + GetParentPidOverride deletion), Services/WatchdogLogger.cs (LogStart gained additive anchor_count), DynaDocs.Tests/Services/ProcessUtilsTests.cs (MatchesProcessName_ExactBasename theory + ParsePsEoPidArgs_PrefixCollision_NotMatched), DynaDocs.Tests/Services/WatchdogServiceTests.cs (3 Run_* tests rewritten + 7 new tests + Logger_* updates for anchors-dir contract + anchor_count assertion). Out-of-plan touch: DynaDocs.Tests/Services/WatchdogRunLivenessContractTests.cs — Run_Source_ContainsParentLivenessCheck became Run_Source_ContainsAnchorLivenessCheck (direct consequence of anchor-mechanism migration; surfaced to Brian). Verify: env-var → file-based migration is complete (no DYDO_WATCHDOG_ANCHOR_PID anywhere in src/tests), exact-match anchor selection, 24h MaxOrphanAge with MaxOrphanAgeOverride hook mirroring PollIntervalOverride, ShellProcessNames extension, --inbox collision-safety comment + regression test, anchor_count is additive (existing JSONL fields preserved). Approve or reject.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit 762eeda for fix-watchdog-anchor-hardening (#0126/#0127/#0128/#0131/#0132). Plan: dydo/agents/Frank/plan-watchdog-anchor-hardening.md. Brief: dydo/agents/Brian/brief-fix-watchdog-anchor-hardening.md (or Kate's inbox copy at dydo/agents/Kate/inbox/8060bde9-fix-watchdog-anchor-hardening.md). Files: Services/ProcessUtils.Ancestry.cs (exact-match + MatchesProcessName extraction), Services/WatchdogService.cs (anchors directory + RegisterAnchor + ScanAnchors + ResolveAnchors + 24h cap + extended ShellProcessNames + --inbox collision-safety comment + GetParentPidOverride deletion), Services/WatchdogLogger.cs (LogStart gained additive anchor_count), DynaDocs.Tests/Services/ProcessUtilsTests.cs (MatchesProcessName_ExactBasename theory + ParsePsEoPidArgs_PrefixCollision_NotMatched), DynaDocs.Tests/Services/WatchdogServiceTests.cs (3 Run_* tests rewritten + 7 new tests + Logger_* updates for anchors-dir contract + anchor_count assertion). Out-of-plan touch: DynaDocs.Tests/Services/WatchdogRunLivenessContractTests.cs — Run_Source_ContainsParentLivenessCheck became Run_Source_ContainsAnchorLivenessCheck (direct consequence of anchor-mechanism migration; surfaced to Brian). Verify: env-var → file-based migration is complete (no DYDO_WATCHDOG_ANCHOR_PID anywhere in src/tests), exact-match anchor selection, 24h MaxOrphanAge with MaxOrphanAgeOverride hook mirroring PollIntervalOverride, ShellProcessNames extension, --inbox collision-safety comment + regression test, anchor_count is additive (existing JSONL fields preserved). Approve or reject.

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-29 16:45
- Result: PASSED
- Notes: LGTM. Commit 762eeda matches Frank's plan and Brian's brief end-to-end. Verified: (1) env-var migration complete — no DYDO_WATCHDOG_ANCHOR_PID in src/tests; (2) FindAncestorProcess uses MatchesProcessName exact basename match, theory covers claudia/claude-dev/CLAUDE/null; (3) MaxOrphanAge=24h + MaxOrphanAgeOverride hook mirrors PollIntervalOverride; (4) ShellProcessNames extended with fish/dash/tcsh/csh/nu/ksh; (5) load-bearing --inbox comment at PollAndCleanupForAgent + ParsePsEoPidArgs_PrefixCollision_NotMatched regression; (6) WatchdogLogger.LogStart anchor_count additive (StartEvent JSONL order preserved, only added). RegisterAnchor runs before live-watchdog short-circuit (line 89, before pidFile check) so a 2nd dispatcher's claude is registered even when the watchdog already runs. ResolveAnchors picks lowest-PID live for the start log. Out-of-plan touch to WatchdogRunLivenessContractTests was already surfaced to Brian and is a direct consequence of the migration. gap_check: 137/137 modules pass. Tests: 3916/3916 pass on a worktree-isolated run.

Awaiting human approval.

## Approval

- Approved: 2026-04-29 16:50
