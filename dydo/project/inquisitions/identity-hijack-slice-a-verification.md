---
area: identity-hijack-slice-a-verification
type: inquisition
---

# Inquisition — identity-hijack Slice A (post-implementation verification)

## 2026-05-21 — Dexter

### Scope

- **Entry point:** Feature investigation — post-implementation QA of identity-hijack
  Slice A (commits `b9a94f6`, `953f7f2`, `297e5e2`, `3842000`; `01435df` on the branch
  is audit-data only, no `.cs` change). Mandate: confirm the DYDO_AGENT hijack class
  was closed *flawlessly* before merge to `master`.
- **Files investigated:** `Services/AgentRegistry.cs`, `Services/AgentSessionManager.cs`,
  `Commands/GuardCommand.cs`, `Commands/WaitCommand.cs`, `Commands/AgentLifecycleHandlers.cs`,
  `Services/WatchdogService.cs`, `Services/WindowsTerminalLauncher.cs`,
  `Services/LinuxTerminalLauncher.cs`, `Services/MacTerminalLauncher.cs`,
  `Services/ProcessUtils.Ancestry.cs`; all touched test files; `IntegrationTestBase.cs`.
- **Reference artifacts read:** `plan-identity-hijack-fix.md`, `plan-f13-windows.md`,
  review r2 + r3, `f13-spike-result.md`.
- **Scouts dispatched:** 1 test-writer (Henry — hypothesis #1 reproduction).
- **Gates:** identity test subset 854/854 pass; full suite 4264/4264 (Henry's run with
  the verification test added). Build clean.

### Verdict

**The slice is NOT safe to merge to master as-is.** It closes the hijack class
correctly (F1/F8/F10/F12/F13 are sound), but Slice A's own F11 change introduces a
**new high-severity regression** (Finding 1) — exactly the "new bug" the engagement
mandate was meant to prevent. Two low-severity issues (Findings 2, 3) are also present.
Finding 1 should be fixed before merge; 2 and 3 can be follow-ups.

---

### Findings

#### 1. F11 ownership check silently breaks the auto-resume general-wait re-arm

- **Category:** bug (regression)
- **Severity:** high
- **Type:** tested (reproduced by test-writer Henry)
- **Evidence:**

  All three terminal launchers' **resume** bodies launch a background `dydo wait` to
  "re-arm the general wait … reachability without re-executing its workflow"
  (#022/#021):
  - `Services/WindowsTerminalLauncher.cs:98` — `Start-Process -WindowStyle Hidden -FilePath dydo -ArgumentList 'wait' | Out-Null`
  - `Services/LinuxTerminalLauncher.cs:61` — `(dydo wait >/dev/null 2>&1 &)`
  - `Services/MacTerminalLauncher.cs:63` — `(dydo wait >/dev/null 2>&1 &)`

  That re-arm `dydo wait` is a child of the **resume shell**, not of `claude`
  (`claude --resume` is launched as a *later sibling* in the same `-Command`/bash
  body). So `ProcessUtils.FindClaudeAncestor()` returns null for it. Meanwhile the
  agent's `.session.ClaimedPid` still holds the **dead pre-resume claude PID**
  (`RefreshClaimedPid` only runs on the next `ClaimAgent`, which has not happened yet).

  `WaitCommand.Execute` (`Commands/WaitCommand.cs:52-56`, new in this slice) now calls
  `registry.VerifyCallerOwnsAgent(agent.Name)` → `IsOwnedByCaller`
  (`AgentRegistry.cs:927-933`): `Environment.ProcessId != claimedPid` **and**
  `FindClaudeAncestor()` is null → returns **false** → `WaitCommand` returns
  `ExitCodes.ToolError` *before* `WaitGeneral`, registering **no** wait marker.

  Because the re-arm is launched detached with output discarded
  (`| Out-Null` / `>/dev/null 2>&1`), the `"Caller does not own agent … Refusing to
  register wait marker."` stderr is **silently swallowed**. The resumed agent comes up
  with no general wait; the guard then blocks its next tool call for a missing general
  wait. The #022/#021 re-arm mechanism is **dead on the resume path, on every platform.**

  This was invisible to the slice's tests: `IntegrationTestBase.cs:54` pins
  `ProcessUtils.FindAncestorProcessOverride = (_, _) => Environment.ProcessId`, so every
  caller in integration tests trivially "owns" its agent — the production resume-rearm
  shape (no claude ancestor) cannot occur in the test harness.

  **Reproduction (Henry, `DynaDocs.Tests/Services/AutoResumeRearmWaitGateTests.cs`,
  2/2 pass; full suite 4264/4264 with the file added):**
  Model the resumed agent's on-disk state — Adele claimed with
  `.session.ClaimedPid = 999001` (dead pre-resume claude); `.session-context` =
  verified two-line `"resume-sid-001\nAdele"`; `DYDO_AGENT=Adele`;
  `FindAncestorProcessOverride => null` (no claude ancestor). Invoke the `wait`
  command with no flags. Assertions, all hold:
  - `exitCode == ExitCodes.ToolError`
  - stderr contains `"does not own"`
  - `dydo/agents/Adele/.waiting` is never created → no `_general-wait.json` marker
  Contrast test: with `FindAncestorProcessOverride` returning the `ClaimedPid`,
  `VerifyCallerOwnsAgent` is true — isolating the cause to the missing claude ancestor.
  Sanity check (rules out a false green): if F11 were removed the poll loop is
  entered, exit code is still 2 (parent-death) but stderr lacks `"does not own"` and
  `.waiting` survives — so the discriminating assertions genuinely fail without F11.

  Key test essence (worktree is temporary; full file preserved in
  `dydo/agents/Dexter/archive/.../inbox` Henry message and reproduced below):
  ```csharp
  // .session.ClaimedPid = 999001 (dead pre-resume claude); state.md status: working;
  // .session-context = "resume-sid-001\nAdele"; DYDO_AGENT=Adele
  ProcessUtils.FindAncestorProcessOverride = (_, _) => null;   // no claude ancestor
  ProcessUtils.IsProcessRunningOverride   = _ => false;        // poll-loop safety net
  var (exitCode, _, stderr) =
      ConsoleCapture.All(() => WaitCommand.Create().Parse(Array.Empty<string>()).Invoke());
  Assert.Equal(ExitCodes.ToolError, exitCode);
  Assert.Contains("does not own", stderr);
  Assert.False(Directory.Exists(".../dydo/agents/Adele/.waiting"));
  // contrast: FindAncestorProcessOverride => ClaimedPid  =>  VerifyCallerOwnsAgent true
  ```

  **Mitigation note (honest):** the agent is not permanently stranded — once it
  re-claims (`RefreshClaimedPid` updates `ClaimedPid`) and re-runs `dydo wait` under
  its own claude, ownership passes. But the re-arm feature is non-functional, the
  failure is silent, and there is a window where a resumed agent is unreachable by
  other agents' messages. The F11 plan analysis assumed the only non-owning `dydo wait`
  caller is an attacker; it missed that the resume launcher spawns a structurally
  non-owning `dydo wait` itself.

- **Judge ruling:** CONFIRMED — severity HIGH upheld
- **Files examined:** `Commands/WaitCommand.cs` (1-66, full `Execute` + `WaitGeneral`);
  `Services/AgentRegistry.cs` (927-961 `IsOwnedByCaller` / `VerifyCallerOwnsAgent` /
  `TryResolveCurrentAgentFromEnvVar`, 1076-1086 `GetSessionContext`, 192-206
  `RefreshClaimedPid`, 332-360 `HandleExistingSession`); `Services/WindowsTerminalLauncher.cs`
  (89-120); `Services/LinuxTerminalLauncher.cs` (55-103); `Services/MacTerminalLauncher.cs`
  (43-66); `Services/ProcessUtils.Ancestry.cs` (67-93 `FindClaudeAncestor`);
  `Services/TerminalLauncher.cs` (39-45 `ResumeContinuationPrompt`); Decision 022;
  `git diff master`.
- **Independent verification:** Traced the failure end-to-end myself. (1) `git diff master`
  confirms `VerifyCallerOwnsAgent` and the `WaitCommand.cs:47-56` gate are *new in this slice*
  — so this is a genuine Slice A regression, not pre-existing. (2) Read all three launchers'
  resume bodies in source: each launches the re-arm `dydo wait` (Start-Process / `( … &)`)
  as a child of the resume shell, with `claude --resume` a later *sibling* — so
  `FindClaudeAncestor()` from the wait process has no claude in its ancestry and returns null.
  (3) Walked `IsOwnedByCaller`: `ClaimedPid` is the dead pre-resume PID, `Environment.ProcessId`
  differs, `FindClaudeAncestor()` null → false → `VerifyCallerOwnsAgent` false → `Execute`
  returns `ToolError` before `WaitGeneral`, no marker. (4) Henry's test models this faithfully
  — `FindAncestorProcessOverride => null` is exactly the no-claude-ancestor shape, `ClaimedPid
  = 999001` the dead PID; the assertions discriminate (the contrast test passes the gate when
  the ancestor matches `ClaimedPid`, and the documented no-F11 sanity check shows `.waiting`
  survives without the gate). (5) **Additional finding beyond the report:** Decision 022 §
  lines 12/49/53 explicitly design the resumed agent to *continue without re-arming or
  re-claiming* — the backgrounded `dydo wait` IS the intended general wait. So the report's
  "self-healing once it re-claims" understates the impact: re-claim is *not* part of the
  Decision 022 resume flow, and the resumed agent's own subsequent `dydo wait` is refused for
  the same reason until a `ClaimAgent` runs `RefreshClaimedPid`. The auto-resume feature is
  effectively non-functional on every platform, not merely missing a re-arm.
- **Alternative explanations considered:** Could `FindClaudeAncestor` find the *old* claude?
  No — it is dead and was a sibling tab, never an ancestor. Could the re-arm be redundant
  belt-and-suspenders the resumed claude duplicates itself? No — Decision 022 states the
  resumed claude does not re-execute its workflow; the launcher `dydo wait` is the sole
  mechanism. This is a real, merge-blocking regression. Severity HIGH is upheld (not lowered
  to MEDIUM): it silently breaks a shipped feature on all three platforms and strands a
  resumed agent behind the guard's missing-general-wait rule. Not CRITICAL — conversation
  context is preserved, no data loss, and an out-of-flow re-claim recovers it.
- **Issue:** #0207

---

#### 2. `GetSessionContext` env path skips `IsValidAgentName` validation

- **Category:** coding-standards / defense-in-depth
- **Severity:** low
- **Type:** obvious
- **Evidence:** `AgentRegistry.GetSessionContext` (`Services/AgentRegistry.cs:1078-1083`)
  reads `DYDO_AGENT` and calls `GetSession(agentName)` directly, with no
  `IsValidAgentName` guard. Its sibling `TryResolveCurrentAgentFromEnvVar`
  (`AgentRegistry.cs:946-948`) — extracted from the same fix — *does* validate:
  `if (string.IsNullOrEmpty(envAgent) || !IsValidAgentName(envAgent)) return null;`.
  `DYDO_AGENT` is attacker-controlled in this slice's own threat model;
  `GetSession` builds `Path.Combine(WorkspacePath, agentName)`, so an unvalidated
  name is at worst a `.session` file-existence probe outside the agents dir (the
  result still must pass `IsOwnedByCaller`, so not directly exploitable). The defect
  is the inconsistency: two env-path readers hardened in the same slice, only one
  validates the name. Add the `IsValidAgentName` guard to `GetSessionContext` for
  symmetry.
- **Judge ruling:** CONFIRMED — severity LOW
- **Files examined:** `Services/AgentRegistry.cs` (1076-1086 `GetSessionContext`, 950-961
  `TryResolveCurrentAgentFromEnvVar`, 1536-1541 `GetSession`, 1645-1646 `IsValidAgentName`,
  76-77 `GetAgentWorkspace`); `git diff master`.
- **Independent verification:** `git diff master` confirms `GetSessionContext` *was* hardened
  in this slice (the env path gained `&& IsOwnedByCaller(session)`) yet still omits the name
  guard its extracted sibling `TryResolveCurrentAgentFromEnvVar` has. Read `IsValidAgentName`
  — it is `AgentNames.Contains(name)`, so it rejects path-traversal names; `GetSession` does
  `Path.Combine(WorkspacePath, agentName, ".session")` on the raw name with no guard, so the
  probe-outside-the-agents-dir is genuinely reachable. Agreed it is not directly exploitable
  (the `IsOwnedByCaller` gate downstream still applies).
- **Alternative explanations considered:** Could `GetSessionContext` deliberately skip
  validation because it runs pre-claim? No — during a legitimate claim `DYDO_AGENT` names a
  real pool agent, so adding the guard cannot break the claim flow. A genuine
  defense-in-depth inconsistency; coding-standards §6 (validate at boundaries) supports the
  fix. LOW severity and a safe post-merge follow-up.
- **Issue:** #0208

---

#### 3. CC=30 `TryResolveCurrentAgentFromEnvVar` extraction is not strictly behavior-preserving

- **Category:** bug (latent / behavior change)
- **Severity:** low
- **Type:** obvious
- **Evidence:** The extraction (`AgentRegistry.cs:940-961`) moved the env fast-path out
  of `GetCurrentAgent` to drop CRAP under 30. The old inline code did
  `if (envSession?.SessionId == sessionId) return GetAgentState(envAgent);` — if
  `GetAgentState` returned null, `GetCurrentAgent` **returned null** (stopped). The new
  code returns `GetAgentState(envAgent)` from the helper; `GetCurrentAgent` then does
  `if (envHit != null) return envHit;` — so a null result **falls through** to the
  hint/scan paths instead of stopping. The reachable trigger (`.session` present but
  `state.md` absent for the env-named agent) is extremely unlikely, and the new
  fall-through behavior is arguably *better*. But the brief explicitly asked to verify
  this refactor is behavior-preserving — it is not, strictly. Worth a one-line note in
  the code or a deliberate confirmation that the fall-through is intended.
- **Judge ruling:** FALSE POSITIVE
- **Files examined:** `git diff master` (the `GetCurrentAgent` extraction hunk);
  `Services/AgentRegistry.cs` (860-884 `GetAgentState`, 967-1014 `GetCurrentAgent`, 950-961
  `TryResolveCurrentAgentFromEnvVar`).
- **Independent verification:** The control-flow difference is real (old: `return
  GetAgentState(envAgent)` *inside* the matched `if`, stops on null; new: `if (envHit !=
  null) return envHit;`, falls through on null) — but it is observably inert, and the
  finding's reachable trigger is misdescribed. (1) The cited trigger — `.session` present,
  `state.md` *absent* — is wrong: `GetAgentState` returns a **non-null** `new AgentState{Free}`
  when `state.md` is absent (lines 867-873), so `envHit` is non-null and `GetCurrentAgent`
  returns it with no fall-through. The only valid-name null path is `ParseStateFile`
  returning null on a *corrupt* `state.md`. (2) Even in that corrupt-`state.md` case, both
  fall-through paths (hint, then slow scan) reconverge on `GetAgentState(<the agent whose
  .session matches sessionId>)` — the identical null — so `GetCurrentAgent` still returns
  null. The observable result is unchanged unless two agents share one session id, which is
  itself state corruption.
- **Alternative explanations considered:** The extraction was never intended as a pure
  refactor — it deliberately adds the `IsOwnedByCaller` F1 gate. The incidental
  fall-through-on-null produces no observable behavior change, so there is no latent bug to
  confirm. The inquisitor's instinct to flag-and-verify was reasonable; verification simply
  comes back clean. An optional one-line code comment confirming the fall-through is
  intentional would be harmless polish, but it is not a defect and no issue is filed.

---

### Hypotheses Not Reproduced / Verified Sound

- **F1 hijack closure** — `GetSessionContext` and `GetCurrentAgent` are the *only*
  identity-resolving reads of `DYDO_AGENT` (grep-confirmed across all `.cs`). Every one
  of the ~30 identity callsites resolves via `sessionId = GetSessionContext()` →
  `GetCurrentAgent(sessionId)`, so gating both env paths closes the class systemically.
  Hint/scan paths are intentionally left ungated (they require a truthful `sessionId`).
- **F1 reproducer genuine** — `IdentityHijackRoleSetTests.ExecuteRoleFlow_…` exercises
  the hijack non-trivially: `FindAncestorProcessOverride` wires through
  `FindClaudeAncestor` (`ProcessUtils.Ancestry.cs:69-75`), Charlie is rejected by a
  real PID mismatch, not a degenerate always-false. The two #0189 encoded-bug tests
  are genuinely rewritten to the new contract; `IsOwnedByCaller` is unit-tested on all
  branches incl. null `ClaimedPid` and no-claude-ancestor.
- **F8 claim rollback** — `ExecuteClaim`/`ExecuteClaimAuto` refuse a mismatched stale
  `DYDO_AGENT` *before* any `ClaimAgent`/`ClaimAuto` call → structurally no half-claim
  leak. The auto-target check→claim TOCTOU is benign (yields a fully-formed claim of a
  different agent, never a leak).
- **F12 legacy read drop** — `AgentSessionManager.GetSessionContext` returns null for
  single-line content; the phase-1 `.session-context` write is removed from
  `HandleDydoBashCommand`. Claim resolves via `.pending-session` (guard always stages
  it), so dropping the single-line fallback breaks nothing. `IntegrationTestBase` /
  `SeedVerifiedSessionContext` fix-ups correctly publish the verified two-line shape.
- **F13 Windows mechanism** — `-NoProfile` + `ProfileReSource` pins `DYDO_AGENT` as the
  first `-Command` statement; both `GetArguments` and `GetResumeArguments` (incl.
  worktree paths) carry `-NoProfile`; only `;` needs `wt` escaping; profile loads are
  `try`-guarded. Watchdog scrub (`WatchdogService.cs:168`,
  `psi.Environment.Remove("DYDO_AGENT")` under `UseShellExecute=false`) is a valid
  combination. The live wt spike (`f13-spike-result.md`) proved all three launch paths
  observe the dispatched name, not the stale parent value.

### Confidence: high

F1/F8/F10/F12/F13 were code-read end-to-end and cross-checked against the plan, the
two prior reviews, and the F13 spike; the identity test subset was run green. The one
regression (Finding 1) is reproduced by a sanity-checked test. Not examined in depth:
Slice B surfaces (out of scope), and the non-Windows launchers' resume bodies beyond
the `dydo wait` re-arm line. The F13 Windows runtime behavior is trusted via the
recorded live spike rather than independently re-run.

### Reproduction test (Finding 1) — full source

Preserved here because the worktree is temporary. File:
`DynaDocs.Tests/Services/AutoResumeRearmWaitGateTests.cs` (Henry; 2/2 pass).

```csharp
namespace DynaDocs.Tests.Services;

using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;
using DynaDocs.Utils;

public class AutoResumeRearmWaitGateTests : IDisposable
{
    private const string ResumeSessionId = "resume-sid-001";
    private const int DeadPreResumeClaudePid = 999001;
    private readonly string _testDir;
    private readonly string _originalDir;

    public AutoResumeRearmWaitGateTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-rearm-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_testDir);
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            { "version": 1, "agents": { "pool": ["Adele", "Zelda"],
              "assignments": { "testuser": ["Adele", "Zelda"] } } }
            """);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
        Environment.SetEnvironmentVariable("DYDO_AGENT", null);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
        ProcessUtils.FindAncestorProcessOverride = null;
        ProcessUtils.IsProcessRunningOverride = null;
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private void SetUpResumedAdele()
    {
        var agentsDir = Path.Combine(_testDir, "dydo", "agents");
        var workspace = Path.Combine(agentsDir, "Adele");
        Directory.CreateDirectory(workspace);
        var session = new AgentSession
        {
            Agent = "Adele", SessionId = ResumeSessionId,
            Claimed = DateTime.UtcNow, ClaimedPid = DeadPreResumeClaudePid
        };
        File.WriteAllText(Path.Combine(workspace, ".session"),
            JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AgentSession));
        File.WriteAllText(Path.Combine(workspace, "state.md"), """
            ---
            agent: Adele
            role: code-writer
            task: t
            status: working
            ---
            """);
        File.WriteAllText(Path.Combine(agentsDir, ".session-context"),
            $"{ResumeSessionId}\nAdele");
    }

    [Fact]
    public void Rearm_GeneralWait_RefusedByF11Gate_AndRegistersNoMarker()
    {
        SetUpResumedAdele();
        ProcessUtils.FindAncestorProcessOverride = (_, _) => null;   // no claude ancestor
        Environment.SetEnvironmentVariable("DYDO_AGENT", "Adele");
        ProcessUtils.IsProcessRunningOverride = _ => false;          // poll-loop safety net

        var command = WaitCommand.Create();
        var (exitCode, _, stderr) = ConsoleCapture.All(() => command.Parse(Array.Empty<string>()).Invoke());

        Assert.Equal(ExitCodes.ToolError, exitCode);
        Assert.Contains("does not own", stderr);
        var waitingDir = Path.Combine(_testDir, "dydo", "agents", "Adele", ".waiting");
        Assert.False(Directory.Exists(waitingDir),
            "F11 refused before the poll loop — no _general-wait marker should have been registered.");
    }

    [Fact]
    public void HonestCallerUnderOwnClaude_PassesSameGate()
    {
        SetUpResumedAdele();
        ProcessUtils.FindAncestorProcessOverride = (_, _) => DeadPreResumeClaudePid;
        var registry = new AgentRegistry(_testDir);
        Assert.True(registry.VerifyCallerOwnsAgent("Adele"));
    }
}
```

---

## Judge Ruling — Brian — 2026-05-21

Reviewed all 3 findings against the code independently (see the inline ruling block on
each finding for files examined and verification).

| # | Finding | Ruling | Severity | Issue |
|---|---------|--------|----------|-------|
| 1 | F11 gate silently breaks the auto-resume general-wait re-arm | CONFIRMED | HIGH (upheld) | #0207 |
| 2 | `GetSessionContext` env path skips `IsValidAgentName` | CONFIRMED | LOW | #0208 |
| 3 | CC=30 extraction not strictly behavior-preserving | FALSE POSITIVE | — | — |

**Merge decision: Slice A is NOT safe to merge to `master` as-is.** Finding 1 is a real
HIGH regression introduced by this slice — it silently breaks the Decision 022 auto-resume
feature on all three platforms — and must be fixed before merge. The F1/F8/F10/F12/F13
hijack-closure work spot-checked sound (env-path gating in `GetSessionContext` /
`GetCurrentAgent`, the F12 single-line `.session-context` drop, and the F13 `-NoProfile` +
profile re-source were read against the diff and confirmed). Finding 2 (LOW) is a genuine
defense-in-depth inconsistency safe to land as a post-merge follow-up. Finding 3 needs no
action — the control-flow change is observably inert and the finding's stated trigger is
incorrect.

After Finding 1 is fixed, re-run the identity subset plus Henry's
`AutoResumeRearmWaitGateTests` (which should then assert the marker IS registered) before
merging.
