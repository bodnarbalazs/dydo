---
area: f11-guard-side-plan-audit
type: inquisition
---

# Inquisition — F11 guard-side `ClaimedPid` auto-refresh plan (Charlie's plan)

Design-phase, adversarial audit of `dydo/agents/Charlie/plan-f11-guard-side.md`
**before** any code is written. The user's bar: "flawless, beyond reproach, no
edge case unhandled." Findings below assume that bar — none of them break the
plan's core mechanism, but the "beyond reproach" gate wants them tightened.

---

## 2026-05-23 — Brian

### Scope

- **Entry point:** Feature audit — plan-phase inquisition of Charlie's
  `plan-f11-guard-side.md` (the redesigned #0207 part 2 after the user rejected
  the prompt-driven re-claim). Charlie's plan builds on the committed checkpoint
  `fe9e551` (#0207 part 1 launcher `dydo wait` deletion + #0208
  `IsValidAgentName` guard); those are NOT in audit scope.
- **Files cross-checked against the plan (read-only):**
  - `Services/AgentRegistry.cs` — full read of `ResolveClaimedPid`,
    `RefreshClaimedPid`, `ResetResumeBookkeeping`, `HandleExistingSession`,
    `IsOwnedByCaller`, `VerifyCallerOwnsAgent`, `TryResolveCurrentAgentFromEnvVar`,
    `GetCurrentAgent`, `GetSessionContext`, `GetAgentState`, `ParseStateFile`,
    `UpdateAgentState`, `WriteStateFile`, `TryAcquireLockAtPath`,
    `ReleaseLockAtPath`, `IncrementResumeAttempts`, `RecordResumeLaunch`,
    `SaturateResumeAttempts`, lock semantics.
  - `Commands/GuardCommand.cs` — full read of `Execute`, security-layer ordering,
    `MissingGeneralWait`, dydo-bash routing, `HandleDydoBashCommand`.
  - `Services/WatchdogService.cs` — full read of `PollAndResumeForAgent`,
    `TryReadResumeContext`, `TryReadGaveUpContext`, `IsBadSessionFailFast`,
    `IsLaunchedClaudeStillAlive`, lock-acquisition/release boundaries.
  - `Services/RecoveryClassifier.cs` — `ClassifyFreshSetup`, `EmitAutoRecovery`.
  - `Services/ProcessUtils.cs`, `Services/ProcessUtils.Ancestry.cs` —
    `IsProcessRunning`, `FindClaudeAncestor`, `MatchesProcessName`,
    `FindAncestorProcessOverride`.
  - `Models/AgentSession.cs`, `Models/AgentState.cs`.
  - `Services/TerminalLauncher.cs` — `ResumeContinuationPrompt` (confirmed: the
    short Decision-022 text is already on the checkpoint — no revert needed).
  - `Services/{Windows,Linux,Mac}TerminalLauncher.cs` — confirmed `#0207` comment
    is in place; launcher-spawned `dydo wait` deletion is committed.
  - `DynaDocs.Tests/Services/AutoResumeRearmWaitGateTests.cs` — the two existing
    F11 tests on the checkpoint.
- **Reference artifacts:** Charlie's plan; Brian's earlier `f11-guard-side-refresh-findings.md`;
  the Slice A verification (`identity-hijack-slice-a-verification.md`);
  Decision 022; architecture.md → Audit Trail / Watchdog.
- **Scouts dispatched:** none — full read access to the cited code and a
  one-agent audit footprint was sufficient given the plan-phase scope. (The
  inquisitor pattern allows scouts; none was needed here.)
- **Method:** every claim, edge case, and proof in the plan re-derived from the
  cited code under both the F11/F1/F13 properties and the resume mechanism;
  every "**provably**" / "**must**" / "**not widened**" statement re-tested.

### Verdict (single paragraph for Adele)

**The plan is safe to code from after ~4 small plan-text fixes and ~3 added
tests.** The core mechanism — trigger keyed on `(agent owns session) ∧ Working
∧ ClaimedPid dead ∧ live claude ancestor`, the new method
`RefreshResumedAgentSession` co-located with `ResetResumeBookkeeping` +
`EmitAutoRecovery` under `.claim.lock`, `WriteClaimedPid` extracted from
`RefreshClaimedPid` to write the validated `livePid` directly (TOCTOU fix),
guard wiring before Security Layer 1, and the companion `ResumeInFlight` clause
on the Decision-018 stale-working reclaim — is correct, preserves F11/F1/F13
end-to-end, and uses no new trust assumption beyond what the guard already has.
No 14th edge case found that breaks the design. **Required before code lands:**
(1) D1's stated mechanism is factually wrong (corrupt `state.md` does **not**
make `GetAgentState` return null — it returns a default `Free` state; outcome
is the same but the named mechanism must be corrected); (2) Proof A's
"provably zero" language overclaims past the F2 corner Charlie himself
acknowledges (`SaturateResumeAttempts` clears `LastResumeLaunchedAt`, so
`ResumeInFlight` falsely returns false during a slow `LaunchedPid`-null
recovery — Proof A needs to read "provably zero outside the F2 corner");
(3) B3 PID-reuse is a probability argument with a worse consequence than the
watchdog's pre-existing one (in the guard refresh case, the alive resumed
agent's `dydo wait` is silently F11-refused until the recycling process exits,
reproducing the Slice A regression shape for that window) — needs a stronger
argument or an explicit test; (4) the Decision 022 amendment direction needs
to call out that the F2 corner produces a **permanently unauditable** recovery
(the self-gate means no `recovery_kind=auto` event ever lands for that
episode, even if the user later runs a manual re-claim). **Tests to add:**
`pid-reuse-skips-refresh`, a worktree variant of `guard-refresh-on-resume`, and
`f2-corner-no-audit-after-saturate`. None of these block the design; all are
plan-text / test-list corrections. Recommend Charlie revise the plan in place
and re-dispatch the code-writer.

---

### Findings

#### 1. Proof A's "provably zero" overclaims past the F2 corner

- **Category:** plan-text / proof completeness
- **Severity:** medium
- **Type:** obvious (re-derive Proof A's predicate against `SaturateResumeAttempts`)
- **Evidence:**
  Companion change predicate (`plan-f11-guard-side.md` § Companion change):
  ```
  ResumeInFlight(state) ≙ state.LastResumeLaunchedAt is { } t &&
                          DateTime.UtcNow - t < WatchdogService.ResumeWarmupGate
  ```
  Two paths clear `state.LastResumeLaunchedAt` mid-recovery:
  - `SaturateResumeAttempts` (`AgentRegistry.cs:1731-1747`) — sets `LastResumeLaunchedAt = null`.
  - The guard refresh itself, via `ResetResumeBookkeeping` (`AgentRegistry.cs:211-220`).

  The watchdog calls `SaturateResumeAttempts` from `IsBadSessionFailFast`
  (`WatchdogService.cs:506-522`) when the wall-clock warmup has elapsed AND
  `LaunchedPid` is null (or dead). This is exactly the **F2 corner Charlie
  himself documents**: launcher returns `LaunchedPid=null` (#0173 corner) and
  the resume is >5 min slow — watchdog logs `failed`, clears
  `LastResumeLaunchedAt`. The resumed claude is *still alive and recovering*.

  During this window:
  - `state.LastResumeLaunchedAt` is null.
  - `ResumeInFlight(state)` returns **false**.
  - A concurrent `dydo agent claim <same agent>` enters
    `HandleExistingSession`'s stale-working branch (`AgentRegistry.cs:365-371`):
    `IsStaleWorking(state) ∧ !IsSessionPidAlive(agentName) ∧ !ResumeInFlight(state)`
    is all-true → returns `true` → `SetupAgentWorkspace` archives the
    recovering agent's workspace.

  Charlie's Proof A states this companion change makes the concurrent-claim
  window "**provably zero**". It does not — it shrinks it from "first tool
  call" (no companion) to "the F2 corner" (with companion). That is still a
  measurable improvement, but the framing is wrong.

  This is the same problem flagged by the Slice A verification: a proof that
  is really a probability argument is a proof failure even if it's practically
  fine. The user explicitly named this in the brief.

- **Independent verification:** Walked `IsBadSessionFailFast` and
  `SaturateResumeAttempts` end-to-end; confirmed both clear
  `LastResumeLaunchedAt`. Walked `HandleExistingSession` stale-working branch
  with the new companion clause; confirmed it falls through to archival when
  `ResumeInFlight=false`. The F2 corner is the only reachable case; outside it,
  the predicate behaves as Charlie claims.
- **Recommended plan fix:** rewrite Proof A's last paragraph as
  "**Window is provably zero outside the F2 corner.** During the F2 corner
  (`LaunchedPid=null` + warmup elapsed + watchdog has logged `failed`),
  `LastResumeLaunchedAt` is cleared and `ResumeInFlight` falsely returns
  false; a concurrent claim could archive the recovering agent. The F2 corner
  is acknowledged out of scope (root cause: launcher `LaunchedPid` reporting
  reliability, #0173-class). The companion change closes the *common*
  warmup-window race; the F2 corner is a separate, instrumentation-pinned
  defect."
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/AgentRegistry.cs` (lines 158-181 `IsEffectivelyFree`/`IsStaleWorking`, 211-220 `ResetResumeBookkeeping`, 235-243 `IsSessionPidAlive`, 332-382 `HandleExistingSession` stale-working branch, 384-422 `SetupAgentWorkspace` including the `ArchiveWorkspace` call on line 390, 1731-1747 `SaturateResumeAttempts`), `Services/WatchdogService.cs` (lines 480-560 `PollAndResumeForAgent`, 506-522 `IsBadSessionFailFast` path that invokes `SaturateResumeAttempts` at line 508).
- **Independent verification:** Walked the entire concurrent-claim chain in the F2 corner with the companion clause applied: `SaturateResumeAttempts` zeros `LastResumeLaunchedAt` on line 1739 → `ResumeInFlight(state)` returns false (the `is { } t` pattern fails on null) → `!ResumeInFlight(state)` is true → with `IsStaleWorking` true (>5 min) and `IsSessionPidAlive` false (`.session.ClaimedPid` still the dead pre-resume PID until guard refresh lands) → all three predicates true → branch falls through → `IsIdempotentReclaim` false (different `sessionId`) → `SetupAgentWorkspace` runs → `ArchiveWorkspace(workspace)` on line 390 destroys the recovering agent's workspace. The chain holds. The "provably zero" claim in Proof A's last paragraph is a probability argument dressed as a proof — exactly what the user's "beyond reproach" bar forbids.
- **Alternative explanations considered:** Could the F2 corner be unreachable in practice? No — it is the well-documented `LaunchedPid=null` (#0173) corner that the watchdog actively handles (the `IsBadSessionFailFast` branch exists *because* the corner is reachable). Could the inquisitor be misreading `SaturateResumeAttempts`? No — line 1739 sets `LastResumeLaunchedAt = null` unconditionally. Could the predicate be saved by some other check? No — `HandleExistingSession`'s stale-working branch is the only filter, and the companion clause `!ResumeInFlight` is the only proposed mitigation. The F2 corner is the lone reachable hole.

---

#### 2. D1 names the wrong mechanism — `ParseStateFile` never returns null

- **Category:** plan-text / factual accuracy
- **Severity:** low
- **Type:** obvious
- **Evidence:**
  Charlie's D1 (corrupt `state.md`) reads:
  > `ParseStateFile` returns null → `GetAgentState` null → `GetCurrentAgent` returns null (inquisition Finding 3) → step 2 no-op **before any write**.

  But the actual code (`AgentRegistry.cs:1893-1919`):
  ```csharp
  private AgentState? ParseStateFile(string agentName, string statePath)
  {
      try
      {
          var content = FileReadRetry.Read(statePath);
          if (content == null)
              return new AgentState { Name = agentName };

          var rawFields = FrontmatterParser.ParseFields(content);
          if (rawFields == null)
              return new AgentState { Name = agentName };

          var state = new AgentState { Name = agentName };
          foreach (var (key, value) in rawFields) { /* parse */ }
          return state;
      }
      catch
      {
          return new AgentState { Name = agentName };
      }
  }
  ```

  Every failure path returns a non-null default `AgentState { Status = Free }`
  (Free is the enum default). `GetAgentState` further wraps this:
  `AgentRegistry.cs:860-884` — if `state.md` is missing it also returns a
  default Free state.

  So on **corrupt state.md**, `GetAgentState` returns non-null with
  `Status = Free`. `GetCurrentAgent` therefore returns non-null. Step 2's gate
  in Charlie's pseudocode (`agent.Status != Working → return`) **does** no-op,
  but via the Status check, not via `GetCurrentAgent` returning null.

  This is **exactly** the mistake Slice A verification Finding 3 flagged and
  the judge ruled FALSE POSITIVE after the correct mechanism was identified
  (see `identity-hijack-slice-a-verification.md`, Finding 3 evidence block).
  Charlie's D1 reintroduces the wrong mechanism after the very judge ruling
  that disambiguated it.

- **Independent verification:** Read `ParseStateFile` and `GetAgentState` end
  to end. The only path by which `GetAgentState` returns null is
  `!IsValidAgentName(agentName)` (line 862) — irrelevant here because Charlie's
  step 2 already passed `IsValidAgentName` via `GetCurrentAgent`'s prior
  validation.
- **Recommended plan fix:** D1 should read:
  > **D1 — corrupt `state.md`.** `ParseStateFile` falls back to a default
  > `AgentState { Status = Free }` (`AgentRegistry.cs:1893-1919`) on any parse
  > failure. Step 2's `agent.Status != Working` gate trips → no-op **before any
  > write**. If `state.md` corrupts between step 2 and step 8, step 8's
  > `priorState.Status != Working` check (the same fallback) returns. Never
  > resets a corrupt state.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/AgentRegistry.cs` (lines 860-884 `GetAgentState`, 967-1014 `GetCurrentAgent`, 1893-1919 `ParseStateFile`).
- **Independent verification:** Read `ParseStateFile` end-to-end — every failure branch (`content == null`, `rawFields == null`, the `catch`) returns `new AgentState { Name = agentName }`, which is a non-null instance with `Status = AgentStatus.Free` (the enum default, confirmed via `ParseStatus`'s default `_ => AgentStatus.Free` at line 1890). `GetAgentState` only returns null when `!IsValidAgentName(agentName)` (line 862); otherwise it either constructs a default `Free` state on missing `state.md` (line 868-873) or returns `ParseStateFile`'s result. `GetCurrentAgent`'s success paths (env-var, hint, scan) all return `GetAgentState(name)` directly — they cannot return null from a corrupt state.md unless the agent name itself is invalid (irrelevant here since the agent was already valid when claimed). So on corrupt `state.md`, `GetCurrentAgent` returns a non-null state with `Status = Free`, and step 2's no-op fires via the `Status != Working` check, not via a null return. Charlie's D1 misnames the mechanism (`ParseStateFile returns null`) — the *outcome* of D1 is correct, but the *named cause* is not.
- **Alternative explanations considered:** Could there be a non-obvious path by which `ParseStateFile` returns null? No — every `return` statement in the body returns a non-null `AgentState`. Could the inquisitor be reading an older revision? Checked: the cited lines match the current worktree. This is also the exact same mechanism the Slice A verification's Finding 3 ruling disambiguated, so reintroducing the wrong mechanism in D1 is a regression in plan-text precision.

---

#### 3. B3 PID-reuse: argument is asymmetric to the watchdog's pre-existing one

- **Category:** plan-text / proof completeness
- **Severity:** low–medium
- **Type:** obvious (re-derive consequence under both code paths)
- **Evidence:**
  Charlie's B3 reads:
  > Step 4's `IsProcessRunning(ClaimedPid)` returns true → refresh skipped that
  > call. This is the **same assumption the watchdog already makes**
  > (`TryReadResumeContext` also treats `IsProcessRunning(ClaimedPid)` as
  > "alive"). Self-heals when the recycling process exits. … Shared pre-existing
  > assumption, not a new hole.

  The assumption is shared, but the **consequence** is not. Walk both paths:

  - **Watchdog under PID reuse:** `TryReadResumeContext` returns null (PID
    "alive") → no relaunch. Status quo for an agent that *can't* be resumed at
    this moment; it's already crashed. **Cost: zero (already crashed; nothing
    new lost).**

  - **Guard refresh under PID reuse:** Step 4 returns → `.session.ClaimedPid`
    stays at the stale dead PID that has just been recycled. The resumed claude
    *is alive* and is actively trying to register `dydo wait`. `IsOwnedByCaller`
    (`AgentRegistry.cs:927-933`) reads ClaimedPid (stale = recycled non-claude
    PID) → `Environment.ProcessId != claimedPid` AND
    `FindClaudeAncestor() != claimedPid` (the recycled process is by definition
    NOT this caller's claude ancestor) → false → F11 refuses the resumed
    agent's `dydo wait`. **Cost: the same silent F11-refused regression the
    Slice A verification flagged and the plan is meant to fix — reproduced for
    the duration the recycling process holds the PID.**

  PID reuse on the seconds-to-minutes scale of the resume window is rare on a
  healthy dev box (Linux PIDs are sequential and the default `pid_max` is
  32768, so wrap-around requires ~32k spawns), and in practice the agent
  recovers as soon as the recycling process exits and the next guarded call
  refreshes. But this is a *probability* argument. Per the user's "beyond
  reproach" bar (and the brief's explicit "if a proof is actually a probability
  argument, flag it as a proof failure even if it's practically fine"), this
  needs either a tighter argument or an explicit test.

- **Independent verification:** Confirmed `IsOwnedByCaller` evaluates exactly as
  described. Confirmed `IsProcessRunning` is name-blind (it only checks the
  HasExited bit; the recycled process need not be claude). Confirmed the
  refresh's step 4 short-circuit blocks the otherwise-corrective walk.
- **Recommended plan fix:** Add to B3:
  > Note the consequence-asymmetry vs. the watchdog: under PID reuse the
  > watchdog's "skip" merely delays a relaunch (cost 0); the guard refresh's
  > "skip" leaves the resumed agent's `dydo wait` silently F11-refused until
  > the recycling process exits (the Slice A regression shape for that window).
  > Recovery time = lifetime of the recycling process, typically sub-second; on
  > a Linux box with sustained fork churn the window can persist seconds. We
  > accept this as inherited from the watchdog's existing assumption; the next
  > guarded call after the recycling process exits self-corrects.
- **Recommended test:** add `pid-reuse-skips-refresh` to the test list — set
  the stale `ClaimedPid` and make `IsProcessRunningOverride` return true for
  it; assert refresh skips. Then flip the override to false; assert next
  refresh proceeds.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/AgentRegistry.cs` (lines 927-933 `IsOwnedByCaller`), `Services/WatchdogService.cs` (lines 568-603 `TryReadResumeContext` — the `IsProcessRunning(pid)` check on line 590), Charlie's plan §B3 + step 4 short-circuit.
- **Independent verification:** Walked both consequence paths. Watchdog under PID reuse: `TryReadResumeContext` returns null at line 590's `IsProcessRunning(pid) ? null` short-circuit → `PollAndResumeForAgent` early-returns at line 499; no relaunch, the agent stays as-is. Net cost: zero, because the agent was already crashed and could not be auto-resumed for the duration of the recycling process anyway. Guard refresh under PID reuse: step 4 of Charlie's pseudocode (`IsProcessRunning(ClaimedPid) == true → return`) short-circuits → `.session.ClaimedPid` remains the stale dead-recycled-non-claude PID → the resumed claude's `dydo wait` invokes `WaitCommand` → `VerifyCallerOwnsAgent` → `IsOwnedByCaller(session)`: `Environment.ProcessId` (the `dydo wait` process) ≠ `claimedPid` (recycled PID); `FindClaudeAncestor()` returns the resumed claude's PID, which is also ≠ recycled PID (by definition — they are different processes; the recycled process is not a claude ancestor of `dydo wait`). False return → F11 refuses. Net cost: the resumed agent's wait is silently refused for the duration of the recycling process's lifetime — the exact Slice A regression shape that this plan is meant to fix, reproduced for that window. The consequence asymmetry is real.
- **Alternative explanations considered:** Could the inquisitor be overstating practical impact? Yes, partly — Linux PID reuse on a healthy box requires ~32k spawns to wrap, so the realistic window is sub-second under normal load. But the brief explicitly told me to flag probability arguments as proof failures, and B3 in the plan IS a probability argument ("typically sub-second"). The inquisitor's recommended remedy (note the consequence asymmetry; add a `pid-reuse-skips-refresh` test) is proportional — it doesn't demand redesign, just honest framing and a pinned regression test for the case when the next call lands on the still-recycled-PID condition.

---

#### 4. F2 corner: the recovery is *permanently* unauditable; the amendment direction must say so

- **Category:** plan-text / Decision 022 amendment direction
- **Severity:** low
- **Type:** obvious
- **Evidence:**
  Under F2 (launcher returned `LaunchedPid=null`, slow resume, watchdog logs
  `failed` via `IsBadSessionFailFast` → `SaturateResumeAttempts`),
  `LastResumeLaunchedAt` is cleared **before** the resumed claude makes its
  first guarded call.

  Guard refresh runs:
  - Step 8 reads `priorState` under the lock. `priorState.LastResumeLaunchedAt`
    is now null.
  - Step 11 calls `RecoveryClassifier.EmitAutoRecovery` with this priorState.
  - `EmitAutoRecovery` (`RecoveryClassifier.cs:47-73`) — first line is
    `if (priorLaunchedAt == null) return;` → **emits nothing**.

  Net: no `recovery_kind=auto` Claim event, no `resume_outcome=succeeded`
  watchdog-log line. The only outcome line for this episode is the *earlier*
  `resume_outcome=failed` written by `IsBadSessionFailFast`.

  Charlie's F2 acknowledges "only the log reads `failed` for a late-succeeding
  resume" — accurate, but the next sentence ("Decision 022 amendment notes
  it") is too thin. The amendment must spell out that **this episode is
  permanently miscategorised** in the 4-bucket join: a *manual* re-claim later
  by the user would also hit the same self-gate (`HandleExistingSession`'s
  same-session branch passes the same priorState with `LastResumeLaunchedAt`
  null after the reset) and also emit nothing. There is no retroactive fix
  short of explicit user override. Inquisition tooling that filters by
  `recovery_kind=auto` will miss this episode forever.

- **Independent verification:** Read both call sites of `EmitAutoRecovery`
  (`AgentRegistry.cs:357-358` from `HandleExistingSession`, and Charlie's
  step 11 from the guard refresh). Confirmed both pass `priorState` snapshots
  whose `LastResumeLaunchedAt` is null after `SaturateResumeAttempts` /
  `ResetResumeBookkeeping`. Confirmed `EmitAutoRecovery`'s self-gate. There is
  no third emission site.
- **Recommended plan fix:** add a bullet to the Decision 022 amendment
  direction §:
  > **The F2 corner produces a permanently un-emit-able `recovery_kind=auto`.**
  > Once `SaturateResumeAttempts` clears `LastResumeLaunchedAt`, both the guard
  > refresh and any later manual `HandleExistingSession` same-session branch
  > self-gate to no-op. The episode shows `resume_outcome=failed` in the
  > watchdog log and no Claim event — inquisition queries that count
  > auto-recoveries by `recovery_kind=auto` undercount this case. Root cause
  > is launcher `LaunchedPid` reporting reliability (#0173-class), out of
  > scope here; the amendment must be explicit so inquisition consumers know
  > the 4-bucket join misses this corner.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/RecoveryClassifier.cs` (full file — lines 47-73 `EmitAutoRecovery`, line 52's self-gate `if (priorLaunchedAt == null) return;`), `Services/AgentRegistry.cs` (lines 340-360 `HandleExistingSession` same-session branch — call site #1, line 357), Charlie's plan §F2 and step 11 — call site #2.
- **Independent verification:** Read `EmitAutoRecovery` end to end. Line 51 reads `priorState?.LastResumeLaunchedAt`; line 52 returns immediately if null. Both call sites (`HandleExistingSession` and the proposed guard refresh's step 11) pass a `priorState` snapshot. After `SaturateResumeAttempts` runs (`LastResumeLaunchedAt = null` at line 1739), every subsequent `GetAgentState` read produces a snapshot with that null. Therefore: the guard refresh's step 8 reads `priorState.LastResumeLaunchedAt == null`, step 11 calls `EmitAutoRecovery` → self-gate fires → no emission. A later manual `dydo agent claim <agent>` (same session) hits `HandleExistingSession`'s same-session branch (line 340) — it also reads the same null-`LastResumeLaunchedAt` priorState (the on-disk reset is permanent) → its own `EmitAutoRecovery` call (line 357-358) also self-gates → still no emission. There is no third call site in the codebase (grepped). So the episode has exactly one `resume_outcome=failed` watchdog-log line (written earlier by `IsBadSessionFailFast`) and ZERO `recovery_kind=auto` Claim events, forever — even under user-driven manual re-claim. Inquisition tooling that filters by `recovery_kind=auto` undercounts this case permanently. Charlie's plan acknowledges F2 produces a "failed" log line, but does not spell out that the *auto Claim event* is also permanently missing, nor that a later manual re-claim cannot resurrect it. The amendment must say this explicitly so consumers of the 4-bucket join aren't silently confused.
- **Alternative explanations considered:** Could a future code path re-emit retroactively? Only via an explicit user override (e.g., a forced `dydo audit emit-recovery <session>`) which doesn't exist; there is no auto path. Could the inquisitor be missing a third emit site? Grepped `EmitAutoRecovery|recovery_kind` — only the two call sites in `AgentRegistry.HandleExistingSession` and the proposed `RefreshResumedAgentSession` (yet to be written). No hidden third site.

---

#### 5. C3 watchdog-vs-guard: a narrow lock-release race that Proof B's framing skips

- **Category:** plan-text / concurrency proof completeness
- **Severity:** low
- **Type:** obvious (re-derive `PollAndResumeForAgent` lock boundaries)
- **Evidence:**
  Charlie's C3:
  > Both take the **same** `.claim.lock` … Once the guard refreshes
  > `ClaimedPid` live, `TryReadResumeContext` returns null at its
  > `IsProcessRunning` check — the watchdog stops resuming.

  True on the *next* watchdog tick. False *within* the current tick. Walk
  `PollAndResumeForAgent` (`WatchdogService.cs:480-560`):

  1. `TryReadResumeContext(agentDir)` — acquires `.claim.lock`, reads
     state+session, **releases lock**, returns `ctx` (snapshot).
  2. `IsBadSessionFailFast(ctx)` — operates on the snapshot. (Lock not held.)
  3. `IsLaunchedClaudeStillAlive(ctx)` — operates on the snapshot. (Lock not held.)
  4. `registry.IncrementResumeAttempts(...)` — re-acquires lock,
     bumps `ResumeAttempts`, sets `LastResumeLaunchedAt = UtcNow`, sets
     `PreResumePid = ctx.ClaimedPid` (the OLD dead PID from the snapshot),
     writes, releases lock. **Does not re-check whether `ClaimedPid` is still
     dead.**
  5. `LaunchResumeTerminal(...)` — fires.
  6. `RecordResumeLaunch(...)` — re-acquires lock, writes `LaunchedPid`,
     releases.

  Between step 1 (lock released) and step 4 (lock re-acquired), the guard
  refresh — running concurrently from the alive resumed claude's first tool
  call — can win the lock, refresh `ClaimedPid` to live, reset bookkeeping.
  When the watchdog re-enters at step 4, it reads the (now-reset) state, sets
  `ResumeAttempts = 0+1 = 1`, sets `LastResumeLaunchedAt` to now, **launches a
  duplicate resume terminal**.

  In practice this race is *very* narrow: the watchdog tick is 10s; steps 1–4
  take ~milliseconds; the guard refresh fires on the *first* tool call the
  resumed claude makes, which is typically seconds after the resume terminal
  is launched (already past the in-flight watchdog tick that launched it).
  The window where a *second* watchdog tick is in steps 1–4 AND the resumed
  claude's first call lands in the same ~ms gap is essentially nil. But it's
  a real lock-release boundary that Proof B's serialization claim doesn't
  cover; the brief explicitly asked us to "verify the lock scope actually
  covers both reads+writes in both paths."

  Mitigation if it ever fires: the duplicate launch creates a second resumed
  claude, both sharing the same `session_id`. C5 ("two live resumed claudes
  for one session") covers the resulting state — the first-refresher wins,
  duplicate is closed by F11. So this is a self-correcting double-launch, not
  a corruption; user sees an extra terminal that closes within a few seconds.

- **Independent verification:** Read `PollAndResumeForAgent`, `TryReadResumeContext`,
  `IncrementResumeAttempts`, `RecordResumeLaunch`, `SaturateResumeAttempts`
  end to end. Confirmed lock-release boundary between steps 1 and 4 of
  `PollAndResumeForAgent`. Confirmed step 4 does not re-validate ClaimedPid
  liveness. Confirmed the race is pre-existing — the same window exists
  between any same-session reclaim (today's `HandleExistingSession`) and the
  watchdog, just exercised less often because manual re-claim is rare.
- **Recommended plan fix:** Add to C3:
  > Note the lock-release boundary: `PollAndResumeForAgent` drops the lock
  > between `TryReadResumeContext` (read) and `IncrementResumeAttempts`
  > (write), and `IncrementResumeAttempts` does not re-check ClaimedPid
  > liveness under the second lock. A guard refresh that lands in that
  > ~millisecond gap causes the watchdog to launch a duplicate resume
  > terminal. The duplicate is harmless under C5 — first-refresher wins, F11
  > closes the loser — but it is a window the plan inherits from the
  > pre-existing watchdog-vs-manual-reclaim race, not one Proof B's
  > serialization claim closes.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/WatchdogService.cs` (lines 480-560 `PollAndResumeForAgent`, 568-603 `TryReadResumeContext` including the finally-release at line 601), and grepped for `IncrementResumeAttempts` / `RecordResumeLaunch` to verify they acquire the lock independently.
- **Independent verification:** Read `PollAndResumeForAgent` line by line. `TryReadResumeContext` acquires `.claim.lock` at line 575, returns the snapshot record, and releases the lock in the finally on line 601 — explicitly stated in the method's own doc comment: "Lock is released before this method returns." `PollAndResumeForAgent` then proceeds at line 506 onward operating on the snapshot OUTSIDE the lock. `IncrementResumeAttempts` is invoked at line 532 — separate method, separate lock acquisition. Between line 503 (lock released) and line 532 (lock re-acquired), the `.claim.lock` is unheld. A concurrent guard refresh from the alive resumed claude's first tool call CAN win the lock in that gap. When the watchdog re-enters at line 532, it operates on the stale snapshot — it has no re-check of `IsProcessRunning(ClaimedPid)` after the lock re-acquisition; `IncrementResumeAttempts` accepts the snapshot's `ctx.ClaimedPid` (the old dead PID) as the `PreResumePid` parameter. So the watchdog could launch a duplicate resume terminal whose `PreResumePid` is already-stale. Proof B's "serialized by the lock" framing is true for the per-method scope but does not span the read→write boundary of `PollAndResumeForAgent` as a whole. The inquisitor's mitigation argument (C5 first-refresher-wins; duplicate harmless) is sound — the duplicate's guard refresh hits step 4 short-circuit (`IsProcessRunning(ClaimedPid)` true → return) and never steals ownership, F11 closes the loser's wait. But "harmless" ≠ "covered by Proof B." Proof B's wording overclaims.
- **Alternative explanations considered:** Could `IncrementResumeAttempts` re-validate ClaimedPid liveness under the second lock? Read it — no such check exists; it simply increments and writes. Could a watchdog-internal guard catch the race? The 5-minute `ResumeWarmupGate` and `IsLaunchedClaudeStillAlive` filters operate on the *snapshot's* `lastResumeAt` and `launchedPid`, which are pre-snapshot values — they do not address the post-snapshot/pre-write window. The race is genuinely uncovered by Proof B. Practical impact is low (~ms window vs. 10s tick) but the brief explicitly asked for verification that the lock scope covers both reads+writes in both paths; it does not.

---

#### 6. `HandleExistingSession` same-session branch is still REACHABLE on auto-resume; emit invariant is from TWO paths, not "moved"

- **Category:** plan-text / Decision 022 amendment direction
- **Severity:** low
- **Type:** obvious
- **Evidence:**
  Charlie writes:
  > `recovery_kind=auto` is now emitted by `GuardCommand` →
  > `RefreshResumedAgentSession`, not `HandleExistingSession` (whose
  > same-session branch still exists for an explicit manual re-claim but is no
  > longer on the auto-resume path).

  "no longer on the auto-resume path" is too strong. `HandleExistingSession`'s
  same-session branch (`AgentRegistry.cs:340-360`) fires whenever an existing
  `.session.SessionId == sessionId`, regardless of whether the trigger was
  manual or auto. A resumed agent whose first action is
  `dydo agent claim <self>` (out-of-flow but reachable — the workflow.md
  template still tells claimed agents to claim, and an LLM that's slightly
  confused on first wakeup may do so) routes through this branch.

  Proof B already correctly establishes the at-most-once invariant via the
  lock serialization + `EmitAutoRecovery`'s `priorLaunchedAt == null` self-gate.
  The plan is *correct*; only the amendment direction is loose. Worth fixing
  so a future docs-writer doesn't accidentally delete the same-session branch
  thinking it's dead code.

- **Independent verification:** Read `HandleExistingSession` and confirmed the
  same-session branch is reachable from `ClaimAgent` regardless of caller
  context. Read `EmitAutoRecovery`'s self-gate. Read Proof B and confirmed the
  lock+self-gate serialization is correct in either firing order.
- **Recommended plan fix:** Amendment direction bullet should read:
  > `recovery_kind=auto` can be emitted by **either** path — `GuardCommand →
  > RefreshResumedAgentSession` on the resumed session's first guarded call,
  > OR `HandleExistingSession`'s same-session branch on an explicit manual
  > re-claim. Both call `RecoveryClassifier.EmitAutoRecovery` under the
  > per-agent `.claim.lock`; whichever wins the lock first emits, and the
  > other path self-gates to no-op via
  > `priorState.LastResumeLaunchedAt == null` (set by the winner's
  > `ResetResumeBookkeeping`). The auto-resume invariant is **at-most-one
  > emission per resume episode, from one of two equally-correct paths** —
  > not "moved from one path to another."
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/AgentRegistry.cs` (lines 332-360 `HandleExistingSession`, with the same-session branch on line 340-360 being reachable from any `ClaimAgent` caller regardless of trigger context), `Services/RecoveryClassifier.cs` (lines 47-73 `EmitAutoRecovery` self-gate), Charlie's plan amendment direction bullet.
- **Independent verification:** Walked `HandleExistingSession` — its same-session branch (`existingSession.SessionId == sessionId`) is independent of who triggered the claim. `ClaimAgent` is exposed via `dydo agent claim` (CLI surface, line ~245). Any caller — whether a manual `dydo agent claim` from a human terminal OR the resumed claude calling `dydo agent claim` as its own first action (out-of-flow but reachable; LLMs can be confused on wakeup and the workflow.md template still tells them to claim) — that hits a same-session match routes through this branch. There is no caller-context filter. Proof B's lock+self-gate ensures at-most-one emission regardless of order, but BOTH paths must continue to exist; the same-session branch is not dead code. Charlie's wording "no longer on the auto-resume path" is misleading and a future docs-writer could plausibly read it as "delete the same-session branch" — which would break the case where the LLM does redundantly re-claim. The plan IS correct (Proof B holds); only the amendment-direction wording is loose.
- **Alternative explanations considered:** Could the workflow.md updates close this scenario? Read Emma's workflow.md (loaded earlier as part of onboarding) — section 1 still tells the agent "if you ran `dydo agent claim auto` for any reason, mention it." The framework does not prevent a confused LLM from re-claiming. So the same-session branch must remain reachable.

---

#### 7. Test list gaps: PID reuse, worktree refresh, F2 audit gap

- **Category:** missing-test
- **Severity:** low
- **Type:** obvious
- **Evidence:**
  Cross-checking Charlie's "Tests to Add" against the edge cases:
  - **B3 PID reuse:** no test exercises the scenario (recycled PID alive, step 4
    skip → next call, recycled PID dead → step 4 falls through → refresh
    proceeds). Charlie's `fresh-session-noop` is the closest, but it tests the
    *honest* live PID, not a recycled-stale shape.
  - **G1 worktree resume:** the verification recipe step 4 covers it live, but
    no unit test. The existing `AutoResumeRearmWaitGateTests.SetUpResumedAdele`
    uses a flat `_testDir` layout — a worktree variant ensures the junction
    paths Charlie's plan banks on actually resolve in tests, not just on the
    spike.
  - **F2 corner audit gap (Finding 4):** no test asserts that after
    `SaturateResumeAttempts` has cleared `LastResumeLaunchedAt`, a subsequent
    guard refresh emits no `recovery_kind=auto`. This is exactly the kind of
    instrumentation invariant that drifts silently — pinning it with a test
    keeps future changes honest.

- **Independent verification:** Walked Charlie's test list against each edge
  case category; confirmed the three above are the only gaps. The
  `refresh-idempotent-no-double-emit` test pins the double-call case but uses
  the *common* refresh path (LastResumeLaunchedAt non-null on first call).
  None of the other listed tests exercise the F2 corner or PID-reuse shapes.
- **Recommended plan fix:** add three tests to the existing list:
  - `pid-reuse-skips-refresh` — stale `ClaimedPid` mapped to a live (recycled)
    process via `IsProcessRunningOverride`; assert step 4 short-circuits and
    `.session` is byte-unchanged. Then flip the override; assert next call
    refreshes.
  - `guard-refresh-on-resume-worktree` — same as `guard-refresh-on-resume` but
    seed the workspace under a worktree-shaped layout (or stub
    `IsWorktreeContextOverride`); assert the refresh write and audit-emit
    land in the expected (worktree workspace; main `watchdog.log`).
  - `f2-corner-no-audit-after-saturate` — pre-clear `LastResumeLaunchedAt`
    (simulating prior `SaturateResumeAttempts`); fire the refresh; assert
    `recovery_kind=auto` audit event count is 0 and the `.session.ClaimedPid`
    IS updated (functional recovery without audit).
- **Judge ruling:** CONFIRMED
- **Files examined:** Charlie's plan's "Tests to Add" §, cross-referenced against the edge cases identified in this report (Findings 1, 3, 4) and the existing test file `DynaDocs.Tests/Services/AutoResumeRearmWaitGateTests.cs`.
- **Independent verification:** Walked Charlie's enumerated tests one by one against each edge case: (a) B3 PID-reuse — the closest existing test is `fresh-session-noop` (honest live PID), but no test exercises the recycled-stale-but-IsProcessRunning-true shape; this matters because it's exactly the silent-F11-refuse window from Finding 3. (b) G1 worktree — the verification recipe covers it in a live spike, but unit-test-level pinning is missing; the existing `AutoResumeRearmWaitGateTests.SetUpResumedAdele` uses a flat `_testDir` and would not catch a junction-resolution regression. (c) F2 corner — the only existing test in the auto-recovery space that asserts emission semantics is `recovery-audit-event` (positive path) and `non-watchdog-resume-refreshes-without-emit` (no-emit because LastResumeLaunchedAt is null *from the start*, e.g., manual `claude --resume`). Neither test pins the F2 shape: `SaturateResumeAttempts` has *cleared* `LastResumeLaunchedAt` from a non-null pre-state. The `non-watchdog` test does not exercise that history transition. These three gaps line up exactly with the three biggest plan-text concerns (Findings 1, 3, 4) — pinning them with tests is the right "beyond reproach" hardening. The three recommended tests are well-scoped, isolated, and use the existing `ProcessUtils` override infrastructure.
- **Alternative explanations considered:** Are any of the three gaps actually covered by an existing test under a different name? Searched: no. Are the recommended tests achievable with current test harness? Yes — `IsProcessRunningOverride` and `FindAncestorProcessOverride` already exist, the worktree variant only needs the existing junction setup, and the F2 shape needs only a pre-seeded `LastResumeLaunchedAt = null` state.

---

#### 8. D3 "not introduced or widened" wording is a slight overreach (cosmetic)

- **Category:** plan-text / wording
- **Severity:** very low
- **Type:** obvious
- **Evidence:**
  D3 says non-atomic `.session` / `state.md` writes are "Pre-existing pattern
  … **not introduced or widened**."

  The pattern is pre-existing, yes. But Charlie's plan does introduce a **new
  state.md writer in the guard hot path** — `ResetResumeBookkeeping` from the
  guard refresh fires on every resumed agent's first call. Previous writers
  (SetRole, SetDispatchMetadata, IncrementResumeAttempts, etc.) operate from
  much narrower call sites, several with lock protection. In practice the new
  writer's exposure is bounded:
  - Other writers either take `.claim.lock` (IncrementResumeAttempts,
    SaturateResumeAttempts, RecordResumeLaunch) — serialized with the guard
    refresh — or target a different agent state (SetDispatchMetadata writes
    to a Dispatched/Queued target, never Working, and the refresh's status
    gate skips non-Working).
  - The agent's own concurrent SetRole / TrackReadCompletion / etc. run from
    the SAME claude session and are serialized through the same hook by Claude
    Code (PreToolUse runs to completion before the tool).

  So the race surface barely changes in practice. The wording "not introduced
  or widened" is still slightly too strong — a new writer IS introduced. The
  honest claim is "introduced, but the new writer is lock-serialized with
  every other writer it can race with."

- **Independent verification:** Enumerated every `UpdateAgentState` caller
  (`AgentRegistry.cs:144, 213, 408, 567, 737, 1670, 2372, 2380, 2391, 2399`).
  Walked the lock semantics of each. Confirmed the picture above.
- **Recommended plan fix:** rewrite D3's last sentence: "Pre-existing pattern;
  the guard refresh introduces a new state.md writer on the hot path, but it
  is lock-serialized (via `.claim.lock`) with every other writer it can race
  with for a Working agent. Atomic temp-write+rename remains a separate
  codebase-wide hardening — out of scope, noted."
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/AgentRegistry.cs` (lines 211-220 `ResetResumeBookkeeping`, 1749-1754 `UpdateAgentState`, 1756+ `WriteStateFile`, and the existing `UpdateAgentState` callers).
- **Independent verification:** Confirmed `ResetResumeBookkeeping` writes state.md via `UpdateAgentState` (line 213) which delegates to `WriteStateFile` (line 1756) using `File.WriteAllText` — non-atomic. The guard refresh wires this onto every resumed agent's first guarded call (Charlie's step 10), which IS a new write site on the hot path. Existing writers all either take `.claim.lock` (IncrementResumeAttempts, SaturateResumeAttempts, RecordResumeLaunch — all do so in their own methods) or target Dispatched/Queued (SetDispatchMetadata), and the refresh's status gate skips non-Working agents, so the race surface relevant for Working agents is fully lock-serialized with the new writer. The inquisitor's reframing ("introduced, but lock-serialized with every other writer it can race with") is more precise than "not introduced or widened" and avoids the future-reader confusion the looser wording invites. Severity is appropriately low — wording precision, not a defect.
- **Alternative explanations considered:** Could "not widened" be technically defensible since the *race surface* doesn't widen even though a writer is added? Arguably yes — but the phrase "not introduced or widened" is exactly the language a careful reviewer would flag as overclaim. The user's "beyond reproach" bar makes this worth fixing.

---

### Hypotheses Not Reproduced (verified sound — no finding)

- **Trigger correctness (clauses 1-4 exact).** The 4-clause AND
  (agent owns this `session_id` ∧ Status=Working ∧ ClaimedPid set ∧ dead ∧
  live claude ancestor) is correct. Fresh-claim ClaimedPid is set to the live
  ancestor (`ResolveClaimedPid`); steady-state callers always see it alive →
  step 4 short-circuits. ClaimedPid is dead-while-a-guarded-call-runs only
  when claude crashed and a new claude is alive for the same `session_id` —
  the resume case. No other reachable trigger. ✓
- **TOCTOU fix (capture `live` at step 5; write at step 9, not
  `RefreshClaimedPid`).** Re-derived: between step 5 and step 9, if the live
  claude exits, we write the just-died PID; next call retries (status quo,
  same as before the resume started). The risk of `RefreshClaimedPid`'s
  re-resolve falling back to a parent-shell PID (`ResolveClaimedPid` →
  `GetParentPid`) is genuinely avoided by writing the validated `live`
  directly. Strictly safer than `RefreshClaimedPid`. ✓
- **Proof C (F11 wait-DoS stays closed).** Verified `WaitCommand` /
  `VerifyCallerOwnsAgent` / `IsOwnedByCaller` unmodified. Attacker shell:
  no claude ancestor + Environment.ProcessId != ClaimedPid + FindClaudeAncestor
  null → false → refused. Guard refresh runs ONLY as PreToolUse hook of the
  resumed claude, never as the attacker's plain shell — no path to weaken the
  gate. ✓
- **F1 hijack closure not weakened.** The refresh resolves identity via
  `GetCurrentAgent(sessionId)` — no env-var trust beyond what
  `TryResolveCurrentAgentFromEnvVar`'s `IsOwnedByCaller` gate already
  permits. Hint/scan paths require a truthful `session_id` (gated by F1's
  design). A spoofed `DYDO_AGENT` cannot mislead the refresh. ✓
- **F13 (Windows `-NoProfile` + ProfileReSource) unaffected.** Refresh
  touches none of the launcher mechanism. ✓
- **Proof B (idempotent no-double-emit).** Both paths (guard refresh, same-
  session reclaim) take the same per-agent `.claim.lock`; whichever wins runs
  `ResetResumeBookkeeping` (setting LastResumeLaunchedAt=null) BEFORE
  releasing the lock; the other path's `EmitAutoRecovery` self-gates on
  `priorLaunchedAt == null`. At-most-one emission per resume episode is a
  proof, not a probability. (Finding 4's F2 corner is a *zero-emission*
  case, not double-emit.) ✓
- **C5 duplicate-claude.** Trigger keyed on deadness (Charlie's deliberate
  choice over Brian's "ClaimedPid != ancestor") makes the duplicate
  scenario deterministic: first refresher wins, second hits step 4 short-
  circuit (alive). Duplicate cannot steal ownership. F11 closes the
  duplicate's wait. ✓
- **D2 missing state.md handling.** Verified `GetAgentState` returns a
  default `Free` state when state.md is absent (`AgentRegistry.cs:866-873`).
  Status gate trips at step 2/8 → no-op → role NOT wiped. Charlie's D2 is
  correctly stated (contrast with D1, where the *mechanism* is misstated). ✓
- **D4 .session field preservation.** Charlie's `WriteClaimedPid` extraction
  preserves Agent/SessionId/Claimed by construction. The named test
  `refresh-preserves-session-identity-fields` pins it. ✓
- **E1-E4 lifecycle.** Refresh touches only `ClaimedPid` and the 4 resume-
  bookkeeping fields. Role/task/status/writable/readonly paths unchanged.
  Release-after-refresh is well-ordered (refresh's emit then release's
  Release event). ✓
- **F1, F3 audit semantics.** Two Claim events per session is byte-identical
  to what `HandleExistingSession` already emits on a same-session reclaim;
  the `recovery_kind` design anticipated this. `human=null` is fine — same
  as the existing path. ✓
- **G1, G2, G3 worktree/platform.** Junction-shared `.session` /
  `.claim.lock` resolve identically from main and worktree (modulo the
  missing unit test — Finding 7). `EmitAutoRecovery` routes
  `resume_outcome` through `PathUtils.FindMainDydoRoot` → lands in main
  `watchdog.log`. Three platforms covered by the single
  `FindClaudeAncestor` / `IsProcessRunning` abstraction. ✓
- **H1, H2, H3 guard pipeline placement.** Before Security Layer 1 covers
  all tool types AND runs even when the triggering call is blocked. The
  resumed agent's first `dydo wait` passes F11 honestly because the
  PreToolUse hook completes (refreshing ClaimedPid) before the `dydo wait`
  process spawns. CLI-args mode self-gates via FindClaudeAncestor null.
  All three correct. ✓
- **Companion change predicate (outside the F2 corner).** Within the
  warmup window with `LastResumeLaunchedAt` non-null, `ResumeInFlight`
  correctly returns true and refuses concurrent claims. Once warmup
  elapses (and the gate clears via the > comparison), or release/claim
  clears `LastResumeLaunchedAt`, the predicate flips back to false. ✓
  (The F2-corner failure mode is Finding 1.)
- **Operation order 9 → 10 → 11.** Each step is a single durable file
  write; a partial sequence is partial-but-consistent (loss-of-step-10
  costs one episode of ResumeAttempts accumulation; loss-of-step-11 costs
  one audit line). Self-corrects on next episode. ✓
- **Search for a "14th edge case."** Exercised every entry point I could
  derive: tool-type variations (Bash/Read/Write/Glob/Grep/Agent/EnterPlanMode),
  hook-input edge cases (null session_id, malformed JSON, missing
  toolName), reviewer-status agent that gets resumed (watchdog skips,
  refresh skips — consistent), agent that crashes mid-claim (lock
  reclaimed on next call), hint-file ping-pong across multi-agent
  scenarios, worktree directory deleted under a resumed claude, clock
  skew during warmup-gate comparison, AuditService throws inside the
  try/catch, Reviewing→Working transitions, two different agents
  simultaneously resumed. **None reveal a new defect.** Charlie's 8-category
  enumeration is comprehensive.

### Confidence: high

Read all cited code end to end (AgentRegistry ~2600 lines spot-checked at every
section touched, WatchdogService resume path, GuardCommand `Execute`,
RecoveryClassifier, ProcessUtils ancestry, models). Re-derived each of
Charlie's 36 enumerated edge cases (across 8 categories — the brief's "13" was
already stale; Charlie expanded thoughtfully) against the actual code. Ran
through every lock-release boundary in the watchdog and the proposed refresh
path. The findings are precise and reproducible (cited code + lines); none
require a test scout to confirm. **Areas examined less deeply:** I did not
re-run any existing test, did not exercise a live spike, and did not
walk Slice B / #0190 / F14–F19 (out of scope per brief). I trust Charlie's
verification recipe to catch the live behavior; the design-level findings
above are what the inquisition is for.

---

## Judge handoff

Dispatching a judge per the inquisitor pattern. All 8 findings are
plan-text / amendment-direction / test-list precision; none break the design.
The judge should re-verify by reading the cited code (the line numbers above
are in this worktree's checkout of `fix/identity-hijack-slice-a`) and rule on
each finding. Recommended issue filings on CONFIRMED findings: none —
these are plan amendments, not code defects yet (no code has landed).

---

## 2026-05-23 — Emma (judge ruling)

### Summary

- **Findings reviewed:** 8 (all design-phase, no code landed yet)
- **Rulings:** 8 CONFIRMED, 0 FALSE POSITIVE, 0 INCONCLUSIVE
- **Issues filed:** none — per the brief, these are plan amendments, not code defects.
- **Verdict on Adele's plan-safety assessment:** UPHELD. The plan is safe to code from AFTER the plan-text fixes (Findings 1, 2, 3, 4, 6, 8) and the three added tests (Finding 7). The core mechanism is correct; no 14th edge case was found that breaks the design.

### Reasoning (cross-cutting)

I re-derived each finding from the cited code independently (full reads of `AgentRegistry.cs:158-422, 855-1014, 1720-1919`; `WatchdogService.cs:470-640`; full `RecoveryClassifier.cs`). Every finding is grounded in code that behaves exactly as the inquisitor describes:

1. **Finding 1 (Proof A overclaim)** — `SaturateResumeAttempts` (`AgentRegistry.cs:1739`) clears `LastResumeLaunchedAt` mid-F2-corner, defeating the companion clause `ResumeInFlight` and re-opening the archival-during-recovery window. The "provably zero" framing is a probability argument in disguise; the inquisitor's reframe ("provably zero outside the F2 corner") is honest.
2. **Finding 2 (D1 wrong mechanism)** — `ParseStateFile` returns a default `Free` AgentState, never null (`AgentRegistry.cs:1893-1919`). The status-gate trip is correct in outcome but the named cause is wrong; this is the same mistake Slice A's Finding 3 ruling already disambiguated, so the regression in plan-text precision matters.
3. **Finding 3 (B3 consequence asymmetry)** — `IsOwnedByCaller` (`AgentRegistry.cs:927-933`) under PID-reuse reproduces the silent-F11-refuse Slice A regression for the recycling-process lifetime, where the watchdog's symmetric "skip" merely delays a relaunch at zero cost. The plan's "shared assumption" framing hides this.
4. **Finding 4 (F2 permanent audit gap)** — `EmitAutoRecovery`'s self-gate on `priorLaunchedAt == null` (`RecoveryClassifier.cs:52`) cannot be bypassed from either call site after `SaturateResumeAttempts` zeros the field on disk. No retroactive fix exists; inquisition tooling that filters by `recovery_kind=auto` undercounts forever. The Decision-022 amendment must say this explicitly.
5. **Finding 5 (C3 lock-release boundary)** — `PollAndResumeForAgent` drops `.claim.lock` between `TryReadResumeContext` (line 575+601) and `IncrementResumeAttempts` (line 532, separate lock). `IncrementResumeAttempts` does not re-check `IsProcessRunning(ClaimedPid)` under the second lock. Proof B's "serialized by the lock" covers per-method scope, not the read→write boundary of `PollAndResumeForAgent` as a whole. The race is self-correcting via C5 (duplicate harmless) but is genuinely not closed by Proof B.
6. **Finding 6 (HandleExistingSession reachable)** — same-session branch (`AgentRegistry.cs:340-360`) fires for any `ClaimAgent` caller regardless of trigger context; the workflow.md template still tells claimed agents to claim. The amendment-direction wording "no longer on the auto-resume path" invites future deletion of live code; the reframe ("at-most-one emission from one of two equally-correct paths") is the precise statement.
7. **Finding 7 (test gaps)** — three gaps (`pid-reuse-skips-refresh`, `guard-refresh-on-resume-worktree`, `f2-corner-no-audit-after-saturate`) line up with Findings 1, 3, and 4 exactly; none of Charlie's existing tests cover these shapes. The recommended tests are well-scoped and use existing override infrastructure.
8. **Finding 8 (D3 "not widened" wording)** — `ResetResumeBookkeeping` via `UpdateAgentState` (`AgentRegistry.cs:213, 1749-1756`) DOES introduce a new state.md writer on the hot path; the race surface argument still holds (lock-serialized with every relevant writer for Working agents), so the reframe ("introduced, but lock-serialized") is more precise without changing the design.

### What the code-writer should do

1. **Charlie revises the plan in place** per the recommended fixes in Findings 1, 2, 3, 4, 6, 8.
2. **Add three tests** to the test list per Finding 7.
3. **No code lands** until the user signs off on the revised plan (Charlie's gate, intact).
4. **Re-dispatch the code-writer** on `f11-guard-side-replan` only after sign-off.

### What this audit did not cover

Hypotheses-not-reproduced (§ before this section) are the inquisitor's adversarial enumeration of items that did NOT break under re-derivation. I sampled three (Proof C / F11 closure, Proof B / idempotent at-most-one, and C5 / duplicate-claude) and confirmed the inquisitor's verifications hold — the core mechanism is sound. I did not run any test, did not exercise a live spike, and did not walk Slice B / #0190 / F14–F19 (out of scope). My confidence in the rulings above is high; my confidence in the plan's design correctness is high; the residual risk is in implementation precision, which the code-writer's review + verification recipe will catch.

### Worktree note

`ls dydo/agents/Emma/.worktree` checked — this judge session is running inside the f11-guard-side-plan-audit worktree itself. No code changes were made; only the inquisition report was updated with rulings (which is shared across worktrees via the `dydo/project/inquisitions/**` junction). Cleanup recommendation: **discard** the worktree once the user dismisses me — `dydo worktree cleanup f11-guard-side-plan-audit --agent Emma`.
