---
area: general
type: changelog
date: 2026-05-21
---

# Task: identity-hijack-slice-a

Review Slice A of the identity-hijack fix on branch fix/identity-hijack-slice-a (commit b9a94f6). Implements Dexter's plan-identity-hijack-fix.md verbatim — 7 issues bundled: #0183 #0189 #0193 #0194 #0195 #0196 #0197.

What landed:
- F1 (#0183): IsOwnedByCaller helper in AgentRegistry.cs (PID/claude-ancestor verification vs .session.ClaimedPid); env fast-paths in GetSessionContext + GetCurrentAgent gated on it. Extracted TryResolveCurrentAgentFromEnvVar from GetCurrentAgent so its CC stays under the T1 CRAP gate (was 30/CRAP 31.5, now passing).
- #0189: rewrote the two F4 tests in AgentRegistryTests that encoded the buggy contract; added paired _RejectedWhenCallerDoesNotOwnAgent contrast tests + direct IsOwnedByCaller unit tests.
- F8 (#0193): ExecuteClaim refuses stale DYDO_AGENT mismatch with an actionable error.
- F11 (#0195): WaitCommand.Execute verifies VerifyCallerOwnsAgent before wait-marker register/cancel; new IdentityHijackWaitDoSTests.
- F12 (#0196): AgentSessionManager.GetSessionContext discards legacy single-line format; phase-1 single-line write dropped from GuardCommand.HandleDydoBashCommand.
- F13 (#0197): DYDO_AGENT scrubbed on WatchdogService.EnsureRunning's ProcessStartInfo, pinned on the three launchers' child ProcessStartInfo.
- F10 (#0194): closed transitively by F1; pinned via GuardCommandHijackAuditTests.

Key decisions / deviations: #0196 discarding the legacy single-line .session-context cascaded into ~83 test failures (ClaimAuto/ClaimAgent fallback tests + IntegrationTestBase.StoreSessionContext + several separate-session test helpers all depended on the single-line shape). Fixed by updating those test scaffolds to the verified two-line format and adding a SeedVerifiedSessionContext helper; IntegrationTestBase now also pins ProcessUtils.FindAncestorProcessOverride so the F1/F11 ownership checks resolve in CI (no real claude ancestor). No production-behavior deviations from the plan.

Note: F13's psi.Environment pin is a no-op on Windows where UseShellExecute=true (runtime ignores psi.Environment) — worth a look; the in-shell $env:DYDO_AGENT export remains the effective mechanism there, and the pin tests assert the property is set, not its OS effect. Plan §Risks treats the pin as belt-and-suspenders; flagging for your judgment.

Verification: full suite 4257/4257 green, gap_check 140/140 modules pass. Live CLI recipe skipped per Adele (active-session conflict) — automated tests pin the same surfaces. Branch only, no worktree, off master. You inherit the reply baton — report back to Adele on task identity-hijack-slice-a.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review Slice A of the identity-hijack fix on branch fix/identity-hijack-slice-a (commit b9a94f6). Implements Dexter's plan-identity-hijack-fix.md verbatim — 7 issues bundled: #0183 #0189 #0193 #0194 #0195 #0196 #0197.

What landed:
- F1 (#0183): IsOwnedByCaller helper in AgentRegistry.cs (PID/claude-ancestor verification vs .session.ClaimedPid); env fast-paths in GetSessionContext + GetCurrentAgent gated on it. Extracted TryResolveCurrentAgentFromEnvVar from GetCurrentAgent so its CC stays under the T1 CRAP gate (was 30/CRAP 31.5, now passing).
- #0189: rewrote the two F4 tests in AgentRegistryTests that encoded the buggy contract; added paired _RejectedWhenCallerDoesNotOwnAgent contrast tests + direct IsOwnedByCaller unit tests.
- F8 (#0193): ExecuteClaim refuses stale DYDO_AGENT mismatch with an actionable error.
- F11 (#0195): WaitCommand.Execute verifies VerifyCallerOwnsAgent before wait-marker register/cancel; new IdentityHijackWaitDoSTests.
- F12 (#0196): AgentSessionManager.GetSessionContext discards legacy single-line format; phase-1 single-line write dropped from GuardCommand.HandleDydoBashCommand.
- F13 (#0197): DYDO_AGENT scrubbed on WatchdogService.EnsureRunning's ProcessStartInfo, pinned on the three launchers' child ProcessStartInfo.
- F10 (#0194): closed transitively by F1; pinned via GuardCommandHijackAuditTests.

Key decisions / deviations: #0196 discarding the legacy single-line .session-context cascaded into ~83 test failures (ClaimAuto/ClaimAgent fallback tests + IntegrationTestBase.StoreSessionContext + several separate-session test helpers all depended on the single-line shape). Fixed by updating those test scaffolds to the verified two-line format and adding a SeedVerifiedSessionContext helper; IntegrationTestBase now also pins ProcessUtils.FindAncestorProcessOverride so the F1/F11 ownership checks resolve in CI (no real claude ancestor). No production-behavior deviations from the plan.

Note: F13's psi.Environment pin is a no-op on Windows where UseShellExecute=true (runtime ignores psi.Environment) — worth a look; the in-shell $env:DYDO_AGENT export remains the effective mechanism there, and the pin tests assert the property is set, not its OS effect. Plan §Risks treats the pin as belt-and-suspenders; flagging for your judgment.

Verification: full suite 4257/4257 green, gap_check 140/140 modules pass. Live CLI recipe skipped per Adele (active-session conflict) — automated tests pin the same surfaces. Branch only, no worktree, off master. You inherit the reply baton — report back to Adele on task identity-hijack-slice-a.

## Code Review (2026-05-20 13:38)

- Reviewed by: Brian
- Result: FAILED
- Issues: FAIL. (1) F13/#0197 BLOCKER: WindowsTerminalLauncher.PinDydoAgent sets psi.Environment on UseShellExecute=true ProcessStartInfo - Process.Start throws InvalidOperationException (verified empirically). Breaks dydo dispatch + watchdog auto-resume on Windows. Tests pass only via mocked RecordingProcessStarter. Fix: drop PinDydoAgent from WindowsTerminalLauncher; rely on in-shell export. (2) F8/#0193 BLOCKER: ExecuteClaim auto branch runs ClaimAuto before stale-env check, leaking a claimed agent on abort - must check before mutating or roll back. Passed: gap_check 4258/4258, 140/140 modules; F1/#0189/F11/F12 sound.

Requires rework.

## Code Review (2026-05-20 17:33)

- Reviewed by: Brian
- Result: FAILED
- Issues: FAIL — 1 blocker. Both round-1 blockers correctly fixed: F8/#0193 (ExecuteClaimAuto refuses stale DYDO_AGENT pre-ClaimAuto, no leak, selection matches ClaimAuto) and F13/#0197 (-NoProfile + ProfileReSource, UseShellExecute=true untouched, psi.Environment never set; both GetArguments+GetResumeArguments; live wt spike recorded). BLOCKER: WindowsTerminalLauncher.cs:217 and TerminalLauncherTests.cs comments reference dydo/agents/Brian/plan-f13-windows.md — a path that does not exist in the repo (plan was never committed; now only in archive/). Drop that path ref from both comments; the #0197 citation already covers traceability. Verification: build clean, 4262/4262 tests, gap_check 140/140; 12 dydo check errors all pre-existing doc-link issues, branch touches only .cs.

Requires rework.

## Code Review

- Reviewed by: Brian
- Date: 2026-05-20 21:08
- Result: PASSED
- Notes: Round 3 PASS. Round-2 comment blocker fixed in 3842000 (comment-only: dead dydo/agents/Brian/plan-f13-windows.md path dropped from WindowsTerminalLauncher.cs + TerminalLauncherTests.cs; #0197 citation kept; zero .cs grep hits remain). Only commit since round 2, all other surfaces untouched and carry forward the round-2 PASS. Build clean (0 warn), 4262/4262 tests, gap_check 140/140 exit 0. dydo check 12 errors are pre-existing dydo/project doc-link issues — branch changes 21 files, all .cs, zero markdown — out of scope.

Awaiting human approval.

## Approval

- Approved: 2026-05-21 19:06
