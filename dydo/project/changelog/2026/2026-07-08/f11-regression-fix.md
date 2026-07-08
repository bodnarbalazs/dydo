---
area: general
type: changelog
date: 2026-07-08
---

# Task: f11-regression-fix

Review the PART-1 CHECKPOINT of the #0207 F11 regression fix — commit fe9e551 on branch fix/identity-hijack-slice-a.

SCOPE — read carefully. This commit is part 1 + #0208 ONLY. You are reviewing whether THESE changes are correct as committed. You are NOT assessing whether #0207 is fully closed — it is not; part 2 completes it and is OUT OF SCOPE here.

WHAT'S IN THE COMMIT (7 files):
- Part 1: deletion of the shell-spawned 'dydo wait' re-arm from the resume bodies of WindowsTerminalLauncher / LinuxTerminalLauncher / MacTerminalLauncher (plain AND worktree resume branches). Rationale: that 'dydo wait' is a sibling of 'claude', never a descendant, so it can never pass the Slice A F11 ownership gate — it failed silently on every resume. Verify the deletion is complete in all 3 launchers incl. worktree paths, and that ResumeContinuationPrompt and the F11 gate (WaitCommand / VerifyCallerOwnsAgent / IsOwnedByCaller) are UNCHANGED.
- #0208: IsValidAgentName guard added to AgentRegistry.GetSessionContext, matching its sibling TryResolveCurrentAgentFromEnvVar.
- Tests: AutoResumeRearmWaitGateTests (no-claude-ancestor 'dydo wait' stays F11-REFUSED + honest-caller-passes); TerminalLauncherTests assert resume bodies no longer spawn 'dydo wait'; AgentRegistryTests invalid-name fall-through test.

DELIBERATE TEST SHAPE — sign off consciously, do NOT flag as a contradiction: AutoResumeRearmWaitGateTests.WaitWithoutClaudeAncestor_StaleClaimedPid_RefusedByF11Gate asserts a no-ancestor 'dydo wait' STAYS refused. That is intentional — it is the F11 wait-DoS attacker shape, and part 1 closes it by DELETING the doomed launcher caller rather than whitelisting it. An earlier judge note assumed a different fix shape (no-ancestor re-arm should register a marker); the chosen mechanism makes that caller cease to exist, so 'stays refused' is correct.

PLAN DEVIATION: the approved plan (plan-f11-regression-fix.md) specified a prompt-driven re-claim for part 2. After Spike S1 the user rejected that mechanism; part 2 was reverted and is being re-planned as a guard-side ClaimedPid auto-refresh (see dydo/agents/Brian/f11-guard-side-refresh-findings.md). Hence this commit carries part 1 + #0208 only.

GATES: full suite 4266/4266 green; gap_check 140/140 modules.

Baton-pass the verdict to Adele (orchestrator).

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review the PART-1 CHECKPOINT of the #0207 F11 regression fix — commit fe9e551 on branch fix/identity-hijack-slice-a.

SCOPE — read carefully. This commit is part 1 + #0208 ONLY. You are reviewing whether THESE changes are correct as committed. You are NOT assessing whether #0207 is fully closed — it is not; part 2 completes it and is OUT OF SCOPE here.

WHAT'S IN THE COMMIT (7 files):
- Part 1: deletion of the shell-spawned 'dydo wait' re-arm from the resume bodies of WindowsTerminalLauncher / LinuxTerminalLauncher / MacTerminalLauncher (plain AND worktree resume branches). Rationale: that 'dydo wait' is a sibling of 'claude', never a descendant, so it can never pass the Slice A F11 ownership gate — it failed silently on every resume. Verify the deletion is complete in all 3 launchers incl. worktree paths, and that ResumeContinuationPrompt and the F11 gate (WaitCommand / VerifyCallerOwnsAgent / IsOwnedByCaller) are UNCHANGED.
- #0208: IsValidAgentName guard added to AgentRegistry.GetSessionContext, matching its sibling TryResolveCurrentAgentFromEnvVar.
- Tests: AutoResumeRearmWaitGateTests (no-claude-ancestor 'dydo wait' stays F11-REFUSED + honest-caller-passes); TerminalLauncherTests assert resume bodies no longer spawn 'dydo wait'; AgentRegistryTests invalid-name fall-through test.

DELIBERATE TEST SHAPE — sign off consciously, do NOT flag as a contradiction: AutoResumeRearmWaitGateTests.WaitWithoutClaudeAncestor_StaleClaimedPid_RefusedByF11Gate asserts a no-ancestor 'dydo wait' STAYS refused. That is intentional — it is the F11 wait-DoS attacker shape, and part 1 closes it by DELETING the doomed launcher caller rather than whitelisting it. An earlier judge note assumed a different fix shape (no-ancestor re-arm should register a marker); the chosen mechanism makes that caller cease to exist, so 'stays refused' is correct.

PLAN DEVIATION: the approved plan (plan-f11-regression-fix.md) specified a prompt-driven re-claim for part 2. After Spike S1 the user rejected that mechanism; part 2 was reverted and is being re-planned as a guard-side ClaimedPid auto-refresh (see dydo/agents/Brian/f11-guard-side-refresh-findings.md). Hence this commit carries part 1 + #0208 only.

GATES: full suite 4266/4266 green; gap_check 140/140 modules.

Baton-pass the verdict to Adele (orchestrator).

## Code Review

- Reviewed by: Dexter
- Date: 2026-05-22 21:00
- Result: PASSED
- Notes: PASS. Commit fe9e551 (#0207 part 1 + #0208) is correct as committed. Part 1: launcher-spawned 'dydo wait' re-arm deleted in all 3 launchers, plain + worktree resume bodies (each platform feeds both paths from one resume-body builder). ResumeContinuationPrompt and F11 gate (WaitCommand/VerifyCallerOwnsAgent/IsOwnedByCaller) untouched. #0208: IsValidAgentName guard added to GetSessionContext, matching sibling TryResolveCurrentAgentFromEnvVar. Tests meaningful (AutoResumeRearmWaitGateTests, TerminalLauncherTests x3, AgentRegistry invalid-name). Force-run suite 4266/4266 green; gap_check 140/140. dydo check: 12 errors — all from untracked docs (issue files 0207-0210, inquisition verifications) created by concurrent agents, none in this 7-file code commit; pre-existing, out of scope.

Awaiting human approval.

## Approval

- Approved: 2026-07-08 10:15
