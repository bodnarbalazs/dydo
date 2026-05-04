---
area: general
type: changelog
date: 2026-05-04
---

# Task: fix-wait-guard-deadlock

Code-writer task implementing Zelda's plan for issue #0141: align `WaitCommand.WaitGeneral`'s snapshot with the inbox-dir scanner (so the wait stops auto-exiting on already-known unreads) and fix the multi-writer race on the wait marker. Includes a user-directed override on Step 3 — duplicate registration must fail with a NONZERO exit code instead of returning Success — to break the agent habit of defensively re-registering waits.

# Brief: fix-wait-guard-deadlock (#0141)

You are a code-writer. Today is 2026-04-30. Implement Zelda's plan exactly as written, with one user-directed override (see below). Build, run tests, commit, dispatch reviewer, message Brian, release. No worktrees.

## Plan to follow

`dydo/agents/Zelda/plan-fix-wait-guard-deadlock.md`

Read end to end. Two-bug fix: divergence between `state.md.UnreadMessages` snapshot and inbox-dir scanner (causes wait to auto-exit on already-known messages), plus multi-writer race on the marker (causes process leak). 7 steps, 4 named tests + 1 helper unit test.

## User-directed override on Step 3

Zelda's Step 3 has the idempotency check return `ExitCodes.Success` on duplicate registration. **The user explicitly directed otherwise**: the duplicate must fail with a NONZERO exit code so wrappers/scripts (and Claude itself) notice. The error message stays stderr, but the exit code changes:

```csharp
var existing = registry.GetWaitMarkers(agentName)
    .FirstOrDefault(m => m.Task == GeneralWaitMarker);
if (existing is { Listening: true, Pid: { } pid }
    && ProcessUtils.IsProcessRunning(pid))
{
    Console.Error.WriteLine(
        $"A general wait is already active for {agentName} (PID {pid}). Refusing to register a duplicate.");
    return ExitCodes.Failure; // or whatever the project's nonzero convention is — check ExitCodes
}
```

Update Test C accordingly:

```csharp
// Was: Assert: #2 exits Success quickly...
// Now: Assert: #2 exits with NONZERO code, stderr contains the duplicate-warning message;
//      marker.Pid still equals #1's PID; #1 still alive.
```

The behavioral training point: agents (including this Brian's session today) get conditioned by the original deadlock to defensively re-register a wait before every tool block. A silent Success-on-duplicate rewards that habit; a nonzero exit + stderr message breaks it. That's the whole reason for the visible failure.

## Step 7 — Issue body fill-in

Zelda flagged that the planner role can't write to issue files, so #0141's body is empty. **Code-writer can write to `dydo/project/issues/**` per role permissions** — fill in the issue body as part of this commit. Use the plan's Approach + Reproduction sections as the source. Include:

- Description: the divergence between `state.md.UnreadMessages` and inbox-dir scanner; secondary multi-writer race.
- Reproduction: claim agent, drop `*-msg-*.md` file in inbox, run Read on it to deplete state.md.UnreadMessages, then `dydo wait` — observe exit in <1s instead of blocking.
- Resolution: link the commit hash + summary of the two-part fix.

Include the LC project bug report link (`C:\Users\User\Desktop\LC\dydo\agents\Brian\dydo-bug-report-wait-guard-deadlock.md`) as the original external reporter source.

## What "done" means

1. **Build clean**: `dotnet build` zero warnings.
2. **All existing tests pass**: full `dotnet test` green (4000-ish + your 5 new tests).
3. **gap_check passes**: `python DynaDocs.Tests/coverage/gap_check.py` exit 0.
4. **All 5 new tests added** per Zelda's plan + the override on Test C.
5. **Issue #0141 body filled** per Step 7 above.
6. **Single commit**: `fix(wait): align general-wait snapshot with inbox dir + idempotent registration (#0141)`. Body summarizes both bugs + the user override on duplicate-exit code.
7. **Dispatch the reviewer YOURSELF before release**:
   ```bash
   dydo dispatch --no-wait --auto-close --role reviewer --task fix-wait-guard-deadlock --brief "Review commit <hash> for fix-wait-guard-deadlock (#0141). Plan: dydo/agents/Zelda/plan-fix-wait-guard-deadlock.md. Brief: dydo/agents/Brian/brief-fix-wait-guard-deadlock.md. Verify: (1) WaitCommand.WaitGeneral snapshots from inbox dir not state.md, (2) idempotency guard refuses duplicate with nonzero exit + stderr message (per user override), (3) all 5 new tests pass + suite is green, (4) gap_check green, (5) issue #0141 body is filled. Approve or reject."
   ```
8. **Then** message Brian and release:
   ```bash
   dydo msg --to Brian --subject fix-wait-guard-deadlock --body "Done. Commit: <hash>. Tests: <pass/total> (5 new). gap_check: 137/137. Issue #0141 body filled. <one-line note>."
   dydo inbox clear --all
   dydo agent release
   ```

## Hard constraints

- **No worktrees.**
- **No `git --no-verify`, no `git push`** — Brian/the user owns release.
- **Don't expand scope.** Zelda's "Out of Scope" list is the correct boundary — stick to it. If you find a third bug, file a separate issue and surface to Brian.
- **Don't change the cap, the marker on-disk schema, or the `MessageFinder.FindMessage` signature.** Zelda's plan called all three out as out-of-scope.
- **Match the project's exit-code convention** for the duplicate-registration failure. Look at `Models/ExitCodes.cs` (or equivalent) and pick the right nonzero constant. Don't invent a new one.

## Reference

- Zelda's plan: `dydo/agents/Zelda/plan-fix-wait-guard-deadlock.md`.
- LC bug report: `C:\Users\User\Desktop\LC\dydo\agents\Brian\dydo-bug-report-wait-guard-deadlock.md`.
- Issue: `dydo/project/issues/0141-wait-guard-deadlock-dydo-wait-auto-exits-on-already-unread-inbox-state-guard-the.md`.
- Today's session log for live in-vivo reproductions: `dydo/agents/Brian/log-orchestrator-handoff.md`.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit 65705e0 for fix-wait-guard-deadlock (#0141). Plan: dydo/agents/Zelda/plan-fix-wait-guard-deadlock.md. Brief: dydo/agents/Emma/inbox-archive (originally from Brian via inbox 96f6325a). Verify: (1) WaitCommand.WaitGeneral snapshots from inbox dir not state.md (Step 2), (2) idempotency guard refuses duplicate with NONZERO exit + stderr message per user override (Step 3 — ExitCodes.ToolError), (3) all 5 new tests pass + suite is green (3999/3999), (4) gap_check 137/137 modules green, (5) BLOCKER: issue #0141 body NOT filled — code-writer role lacks dydo/project/issues/** write permission (brief was wrong about this). Approve or reject.

## Code Review

- Reviewed by: Adele
- Date: 2026-04-30 22:40
- Result: PASSED
- Notes: PASS. Fix is sound. (1) WaitGeneral now snapshots from MessageFinder.GetInboxMessageIds(inboxPath), aligned with FindMessage's source of truth — divergence-deadlock root cause closed. (2) Idempotency guard refuses duplicate registration with ExitCodes.ToolError + stderr ('A general wait is already active...'), per user override; PID-clobber/zombie leak closed. (3) Tests: 3999/3999 green on rerun (one flaky StaleDispatchDoubleClaimTests.ReserveAgent_StaleAndNoLauncher_Succeeds failed once on first run, passed on rerun — pre-existing flake using static IsLauncherAliveOverride, unrelated to this fix). (4) gap_check 137/137 modules pass. (5) New PollIntervalMs test hook is acceptable — internal static, only used to shorten test poll loops. Three pre-existing tests updated to drop messages mid-flight via IsProcessRunningOverride, matching new post-snapshot-only semantics — mechanically correct. Comments explain WHY (#0141 reference, divergence rationale). Issue #0141 body unfilled is genuinely outside code-writer permissions (readOnlyPaths includes dydo/**) — workflow item for orchestrator/human, not a code-quality blocker.

Awaiting human approval.

## Approval

- Approved: 2026-05-04 21:52
