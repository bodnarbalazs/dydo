---
area: backend
type: inquisition
---

# Identity Hijack Bug Class

A survey of every surface where dydo's "current agent" resolution can diverge from
the calling process's actual ownership. Triggered by issues #0108 (resolved 2026-05-18)
and #0183 (open, filed 2026-05-18); commissioned by Adele as a full bug-class
inventory before any further code changes.

## 2026-05-19 — Brian

### Scope

- **Entry point:** Area investigation, framed as a bug-class survey.
- **Files investigated:**
  - `Services/AgentRegistry.cs` (full read of the identity-resolution and reclaim paths)
  - `Services/AgentSessionManager.cs` (full)
  - `Services/AgentSelector.cs` (post #0108 fix)
  - `Services/DispatchService.cs` (top 100 lines, plus identity-resolving callsites)
  - `Services/MessageService.cs` (full)
  - `Services/InboxService.cs` (full)
  - `Services/LinuxTerminalLauncher.cs`, `WindowsTerminalLauncher.cs`, `MacTerminalLauncher.cs` (full)
  - `Commands/AgentLifecycleHandlers.cs` (full)
  - `Commands/GuardCommand.cs` (NotifyUnreadMessages, HandleDydoBashCommand, top-level dispatcher, and bash routing)
  - `Commands/WaitCommand.cs` (full)
  - `DynaDocs.Tests/Services/AgentRegistryTests.cs:2861-2944`
  - `DynaDocs.Tests/Services/WhoamiConcurrencyTests.cs:310-355`
- **Docs cross-checked:**
  - `dydo/agents/Adele/brief-identity-hijack-round-2.md` (working hypothesis)
  - `dydo/project/issues/0183-identity-hijack-round-2-…` (open, severity high)
  - `dydo/project/issues/resolved/0108-…` (round-1 fix)
  - `dydo/project/changelog/2026/2026-04-09/investigate-wait-flag-bug.md` (April-09 verified-fallback fix)
  - `dydo/project/changelog/2026/2026-05-18/fix-issue-0108-self-dispatch-hijack.md`
  - `dydo/understand/architecture.md` (worktree dispatch, audit, watchdog sections)
- **Scouts dispatched:** 1 reviewer (Emma, `identity-hijack-surface-scan` — completed), 1 test-writer (Dexter, `identity-hijack-repro-role-set` — completed, in-process repro test landed in `DynaDocs.Tests/Services/IdentityHijackRoleSetTests.cs` with the failing case marked `[Fact(Skip = ...)]`). See **Scout reports** below.

### Bug class definition

An **identity hijack** is a bug where a process `P_caller` whose actual session is claimed by agent `A` invokes a `dydo` command that mutates agent `B`'s filesystem state — state.md, `.session`, inbox, `.waiting`, audit trail, dispatch markers — because dydo's "current agent" resolution returns `B` instead of `A`.

The shape is always: `P_caller` reads or writes `B`'s record, the operator's mental model says they are operating on `A`, the system silently does the wrong thing. Hijacks are distinct from refused operations (which fail loudly) and idempotent reclaims (which return the operator to a known good state).

Specifically the bug class **excludes**:
- Operations the operator made on the wrong agent name explicitly (`dydo agent role` after typing the wrong name).
- Cases where two agents legitimately share state via the worktree junction (`dydo/agents/<A>/state.md` is the same file in main and worktree — this is by design; #0108's worktree symptom is hijack-adjacent but the underlying conflict is "two valid claims on the same agent name," not "wrong agent resolved").
- Failures where identity is unknown (`No agent identity assigned to this process`) — these block; they do not hijack.

The bug class includes: any silent mis-routing of a state-mutating effect from the agent the process actually owns to a different agent.

### Map of surfaces

#### S0. The two identity-resolution primitives (the trust spine — asymmetric)

Every identity-aware command in dydo flows through the same two functions in `Services/AgentRegistry.cs`. Each has a `DYDO_AGENT` fast path. **The two are asymmetric** — only one is actually the pivot of the hijack, but the asymmetry is non-obvious from reading them in isolation and was clarified by the test-writer scout (Dexter, repro report below):

**`GetSessionContext()` — `Services/AgentRegistry.cs:1039-1049`:**

```csharp
public string? GetSessionContext()
{
    var agentName = Environment.GetEnvironmentVariable("DYDO_AGENT");
    if (!string.IsNullOrEmpty(agentName))
    {
        var session = GetSession(agentName);
        if (session != null) return session.SessionId;   // ← trusts env var blindly
    }
    return _sessionManager.GetSessionContext();          // verified path (April-09 fix)
}
```

**`GetCurrentAgent(sessionId)` — `Services/AgentRegistry.cs:924-977`:**

```csharp
var envAgent = Environment.GetEnvironmentVariable("DYDO_AGENT");
if (!string.IsNullOrEmpty(envAgent) && IsValidAgentName(envAgent))
{
    var envSession = GetSession(envAgent);
    if (envSession?.SessionId == sessionId)
        return GetAgentState(envAgent);                  // ← trusts env var blindly
}
// then hint file, then per-agent scan
```

**The pivot is `GetSessionContext` alone.** It returns DYDO_AGENT's stored session id unconditionally — no guard, no verification. `GetCurrentAgent`'s env fast path *does* gate on `envSession?.SessionId == sessionId` (line 934), so passing a *truthful* sessionId in causes the env path to be bypassed (envSession's sid won't match) and the scan finds the actually-owning agent. The hijack only manifests because `GetCurrentAgent` is, in practice, almost always called with a sessionId that came from `GetSessionContext` — and that sessionId is the poisoned one. The "closed loop" is upstream-only: poison the sid at GetSessionContext, the downstream GetCurrentAgent's gate becomes vacuously true.

This asymmetry was confirmed by a direct test (Scout reports → Dexter): calling `registry.SetRole(sid_zelda, "co-thinker", "test-task", ...)` directly — bypassing `GetSessionContext` — does NOT hijack, because `GetCurrentAgent(sid_zelda)` correctly resolves to Zelda. Only the full `ExecuteRole` flow (which sources sessionId from `GetSessionContext`) hijacks.

**The fix surface is therefore one function, not two.** Hardening `GetSessionContext` to verify that the DYDO_AGENT-named agent's `.session.ClaimedPid` belongs to the calling process (or its claude ancestor) closes the entire hijack class. `GetCurrentAgent`'s env fast path can be kept as-is or hardened symmetrically as defense-in-depth, but it is not itself the pivot.

The calling process's PID, parent shell, claude-tab ancestry — none of it enters the equation in either primitive today. **The two-line file fallback added by the April-09 fix (`.session-context` with verified agent name) is bypassed entirely** when DYDO_AGENT is set, because `GetSessionContext`'s env fast path returns before `_sessionManager.GetSessionContext()` is reached.

The April-09 changelog (`investigate-wait-flag-bug.md`, line 213) explicitly leaves the DYDO_AGENT fast path "unchanged" with the assumption that "dispatched terminals have DYDO_AGENT set and never reach this path." That assumption holds only as long as DYDO_AGENT never goes stale relative to the calling process — but it does, in three observable cases (S1, S5, S6 below).

#### S1. `dydo agent role` write target (#0183 reproduction #1)

- **File:** `Commands/AgentLifecycleHandlers.cs:172-213` (`ExecuteRole`) → `Services/AgentRegistry.cs:703-768` (`SetRole`).
- **What it does:** Resolves `current` via `GetCurrentAgent(GetSessionContext())`, then `UpdateAgentState(current.Name, ...)` to set role, task, writable/readonly paths, must-reads, dispatched-by markers, task-role history.
- **Failure mode:** Process P with `DYDO_AGENT=Charlie` (inherited or stale) running `dydo agent role <r> --task <t>` mutates **Charlie's state.md**, regardless of which agent P actually claimed. The role/task/permission set on Charlie persist until the next role-set or release. The operator sees `whoami` return Charlie (same primitive), so the hijack is invisible to them.
- **Repro:** See `dydo/project/issues/0183-…` timeline — reproduced 3× in one LC session. See also the test-writer scout report below for an in-process repro.
- **Severity:** **CRITICAL.** Reachable on every role-set (the most common identity-mutating command after claim). Blast radius: another agent's role/permissions/task assignment silently overwritten. Observability: nil — the operator doesn't see the wrong agent's record being touched.

#### S2. `dydo agent release` target

- **File:** `Commands/AgentLifecycleHandlers.cs:59-85` → `Services/AgentRegistry.cs:533-594` (`ReleaseAgent`).
- **What it does:** `GetCurrentAgent(GetSessionContext())` → deletes `.session`, sets state to Free, clears wait/reply/dispatch markers, archives workspace state.
- **Failure mode:** Same hijack chain. Releases the *wrong* agent. The actually-claimed agent stays Working (its `.session` is untouched), but Charlie goes Free with no actual claude process backing it — `claim auto` may then hand Charlie out to a new claim while the original Charlie session id is dangling.
- **Severity:** **HIGH.** Reachable on every release. Less common than role-set in volume, but more destructive — frees up Charlie for re-claim, opening a dual-claim window that compounds with the watchdog auto-resume.

#### S3. `dydo dispatch` sender identity (round-1 #0108 + residual round-2 attribution)

- **File:** `Services/DispatchService.cs:18-32` (Execute entry) and lines 472, 505.
- **What it does:** Resolves `sender` via the same primitives; uses `sender.Name` to (a) attribute the inbox brief, (b) write dispatch-wait markers in the callee's workspace pointing back at `sender.Name`, (c) clear reply-pending markers on `sender.Name`, (d) audit the dispatch.
- **Failure mode:** Sender mis-attribution — the dispatched inbox brief says `from: <hijacked>` even though the actual operating agent is different. The reply-pending markers on the *real* dispatcher are not cleared (because the hijack cleared markers on Charlie instead). Round-1 #0108 fixed *target* selection (self-dispatch refused, sender filtered from pool) but did **not** verify *sender* identity — `senderName` in `AgentSelector` is whatever `GetCurrentAgent(GetSessionContext())` returned upstream.
- **Severity:** **HIGH.** The round-1 fix narrowed the symptom (no more "Brian dispatches to Brian and overwrites Brian's session") but did not close the surface — a hijacked sender can still misattribute the dispatch, write markers in the wrong agent's workspace, and skew audit.

#### S4. `dydo msg` sender + delivery

- **File:** `Services/MessageService.cs:7-47` → `DeliverInboxMessage:53-91`.
- **What it does:** `sender = GetCurrentAgent(GetSessionContext())`, target validation, writes file to target's inbox with frontmatter `from: <sender.Name>`, calls `registry.AddUnreadMessage(toName, messageId)`, clears reply-pending markers on `sender.Name`, stamps dispatch-wait markers on `sender.Name`.
- **Failure mode:** Outgoing messages carry the hijacked agent's name as sender. The phantom-inbox file in #0183 (`dydo/agents/Zelda/inbox/98e06797-msg-general.md` with `from: Charlie`) was almost certainly produced by this surface — some process with `DYDO_AGENT=Charlie` sent a message to Zelda, the file says From: Charlie because the hijack made GetCurrentAgent return Charlie. Reply-pending markers also clear on the wrong agent.
- **Severity:** **HIGH.** Reachable on every `dydo msg` call. Creates the *evidence* of the phantom-inbox deadlock (S8).

#### S5. `dydo wait` agent context + the exit-2 symptom

- **File:** `Commands/WaitCommand.cs:35-91`.
- **What it does:** `agent = GetCurrentAgent(GetSessionContext())`. The general-wait `existing` check (`WaitGeneral:83-91`) looks at the *hijacked* agent's `.waiting/` directory; if a Listening marker with a live PID is found, the wait refuses with `ExitCodes.ToolError` and a stderr message.
- **Failure mode:** When Charlie's actual terminal has its general-wait already running (which it always does after claim), a hijacked `dydo wait` from any process with `DYDO_AGENT=Charlie` sees Charlie's live PID and refuses with exit code 2. **This fully explains the "`dydo wait` background failure with exit code 2, no stderr" symptom in #0183.** The stderr exists but the resume bodies in all three launchers redirect it: Linux `(dydo wait >/dev/null 2>&1 &)` (LinuxTerminalLauncher.cs:61), Mac `(dydo wait >/dev/null 2>&1 &)` (MacTerminalLauncher.cs:63), Windows `Start-Process -WindowStyle Hidden -FilePath dydo -ArgumentList 'wait' | Out-Null` (WindowsTerminalLauncher.cs:83). The diagnostic is silently swallowed.
- **Severity:** **HIGH.** Reachable on every resume body and every manual `dydo wait`. The symptom is operationally severe (general-wait never registers, so guard's `MissingGeneralWait` check fires perpetually) and the diagnostic is hidden by stderr redirection in the launchers.

#### S6. `dydo agent claim` interaction with stale DYDO_AGENT

- **File:** `Services/AgentRegistry.cs:245-292` (`ClaimAgent`) + `ValidateClaimPreconditions:311-330`.
- **What it does:** `ResolveSessionId(agentName)` first reads `.pending-session[agentName]` (which the guard wrote with the correct session id when it intercepted the claim command). If that file is present, the calling process's true session id is used — the env-var fast path is bypassed inside `ClaimAgent` proper. If `.pending-session` is missing (e.g., direct call from a non-guarded context), `GetSessionContext()` is used, which is the hijacked path.
- **Failure mode:** `ValidateClaimPreconditions` calls `GetCurrentAgent(sessionId)` at line 322: if DYDO_AGENT=Charlie and the sessionId resolved-or-passed matches Charlie's `.session`, it returns `existingAgent.Name == "Charlie" && agentName == "Zelda"` → refuses the claim with "This session already has agent Charlie claimed. Release first." This is a *confusing* but *non-hijacking* failure mode — the claim refuses rather than corrupting state. It is **observable** (the error fires) but the error message is misleading (the operator is *trying* to claim Zelda, not Charlie; the error doesn't mention the env var).
- **Severity:** **MEDIUM.** Not silently destructive; loud but misleading. The operator workaround is to `unset DYDO_AGENT` first. Adele's suggested defense (refuse claim when DYDO_AGENT≠agentName with an actionable error) is appropriate.

#### S7. Guard's per-tool-call identity (the spine of the deadlock)

- **File:** `Commands/GuardCommand.cs:252, 275, 338, 429, 553, 634, 713, 842, 1008, 1465` (every `GetCurrentAgent(sessionId)` callsite in the guard).
- **What it does:** Every PreToolUse hook invocation resolves identity, then dispatches to: off-limits checks, NOTICE delivery (`NotifyUnreadMessages`), must-read enforcement, RBAC (`IsPathAllowed`), wait-marker checks (`CheckPendingState`).
- **Failure mode:** The guard's view of "who is calling" is whichever agent DYDO_AGENT names. Consequences:
  - RBAC is evaluated against the hijacked agent's writable/readonly paths. If Charlie has broad write paths and Zelda doesn't, a Zelda-process file write may pass guard — *cross-agent privilege elevation through env var inheritance*.
  - NOTICE is sourced from the hijacked agent's UnreadMessages. The phantom-inbox NOTICE in #0183 stems from this: Zelda's UnreadMessages has the phantom id but the guard resolves to Charlie (no phantom there), so the NOTICE doesn't fire from the hijacked side; conversely a Zelda-actually-claimed process sees the NOTICE about Zelda's inbox even though it can't write to Zelda's record to clear it.
  - Audit events are tagged with the hijacked agent.
- **Severity:** **CRITICAL.** Every tool call. Cross-agent RBAC privilege elevation is the highest-impact known consequence of this surface.

#### S8. NOTICE / unread-message deadlock (downstream of S4 + S7)

- **File:** `Commands/GuardCommand.cs:991-1042` (`NotifyUnreadMessages`).
- **What it does:** Self-heals phantom ids (where state.md says "unread" but the file is gone). Then iterates `agent.UnreadMessages`, prints NOTICE with one line per file, returns `ExitCodes.ToolError`.
- **Failure mode:** The self-heal handles "id in state.md, file absent" — the LC scenario is the inverse: file *present* in agent A's inbox, but the calling process can only resolve to agent B. Concretely:
  - Zelda's state.md has UnreadMessages = `[98e06797]`. The file exists at `dydo/agents/Zelda/inbox/98e06797-msg-general.md`. Self-heal doesn't fire (file present).
  - Operator wants to clear it: `dydo inbox clear --id 98e06797` (InboxService.cs:67) resolves identity via the same primitives. If DYDO_AGENT=Charlie, "no inbox item with ID" because the id is in Zelda's UnreadMessages, not Charlie's.
  - Operator wants to give Zelda a role to read the file (Stage 1 read-block fires: "Agent Zelda has no role set"): `dydo agent role <r>` hijacks to Charlie (S1).
  - Operator wants to delete the file with `Remove-Item`: blocked by the same NOTICE.
  - `dydo guard lift Charlie 10` doesn't help — RBAC lift is per-agent; the NOTICE is the guard's pre-RBAC layer.
- **The deadlock is the intersection of two bugs**: identity resolution is wrong (S1, S7), and the NOTICE has no operator escape hatch (`dydo inbox clear --force --file <path>` does not exist; the NOTICE handler doesn't probe whether the calling agent can actually clear the cited inbox).
- **Severity:** **HIGH.** Reachable whenever a phantom message lands in a different agent's inbox via S4, which is reachable any time DYDO_AGENT is stale.

#### S9. `.session-context` fallback (April-09 verified path, multi-human gap)

- **File:** `Services/AgentSessionManager.cs:205-233` (`ResolveSessionFallback`).
- **What it does:** Scans `AgentNames` for the single Working agent, returns its session id if exactly one is found. Used when verification of the two-line `.session-context` file fails.
- **Failure mode:** The implementation **does not filter by `AssignedHuman == currentHuman`**, despite the April-09 plan (`investigate-wait-flag-bug.md:51`) explicitly saying "Filter by `assignedHuman == currentHuman`." In a multi-human project, the fallback can return another human's session id. In a single-human project, the impact is bounded — but `ResolveSessionFallback` then short-circuits to `null` if more than one Working agent exists, which produces "No agent identity" errors instead of mis-resolved identity. Either outcome is wrong, but the multi-human resolution-to-stranger is the more concerning case.
- **Severity:** **LOW-MEDIUM** in current usage (single-human projects). **HIGH** if dydo supports multi-human projects in the future without auditing this path. Pre-existing latent bug, separate from the round-2 hijack.

#### S10. `HandleExistingSession` and watchdog auto-resume (#0143, #0153, decision 022)

- **File:** `Services/AgentRegistry.cs:332-382` (`HandleExistingSession`).
- **What it does:** When claim arrives and an existing session exists for the agent:
  - **Path A** (state Free/Dispatched/Queued or no existingSession): proceed with claim.
  - **Path B** (same-session reclaim, `existingSession.SessionId == sessionId`): `RefreshClaimedPid` (#0143) + `ResetResumeBookkeeping` (#0153) + audit event with `recovery_kind=auto` + proceed (idempotent — workspace not regenerated).
  - **Path C** (stale-working >5min + dead PID): proceed with workspace archive.
  - **Path D** (anything else): refuse with "already claimed by another session" + claimable-agents hint.
- **Hijack interaction:** The `sessionId` passed in comes from `ResolveSessionId(agentName)` which prefers `.pending-session[agentName]` over `GetSessionContext()`. The guard writes `.pending-session` when it intercepts a claim command (`Commands/GuardCommand.cs:680-706`), so the calling-process session id flows through correctly for claim-by-name. Path B's same-session check therefore compares the *real* calling-process session id against Charlie's stored session id — they differ, falls to Path D, claim refused. No silent hijack here.
- **Edge case:** If the guard's `HandleClaimSessionStorage` is bypassed (direct call to `ClaimAgent`, no guard interception), `GetSessionContext()` is used, which is hijackable. In production, every claim goes through the guard (it intercepts every Bash invocation), so this is a test-only concern.
- **Severity:** **LOW.** The reclaim paths are correctly gated against the hijack via `.pending-session` — credit to the prior #0103/#0143/#0153 work. The audit's `recovery_kind=auto` and the resume-outcome event in the watchdog log (architecture.md `Watchdog` section) accurately reflect same-session reclaims; identity isn't muddied here.

#### S11. Worktree shared `dydo/agents/` junction

- **File:** `Services/WorktreeCommand.cs` (junction creation); `dydo/understand/architecture.md#worktree-dispatch`.
- **What it does:** Four directories are junctioned across worktrees: `dydo/agents/`, `dydo/_system/roles/`, `dydo/project/issues/`, `dydo/project/inquisitions/`. Per-agent state files are physically the same file across all worktrees.
- **Failure mode:** Two worktree processes for the same agent name race on `dydo/agents/<X>/state.md`, `.session`, and `.session-agent`. This was the round-1 #0108 surface (worktree reviewer auto-dispatch onto the orchestrator). Round-1's fix (filter sender from selector pool) closes the *dispatch* surface but the shared-junction *race* is intrinsic — two worktrees running as the same agent name can still produce write-write conflicts on .session.
- **Hijack interaction with round-2:** A worktree process with `DYDO_AGENT=X` and a main-tree process with `DYDO_AGENT=X` mutate the same files via S1, S2, S3, S4. The worktree doesn't add hijack vectors but amplifies blast radius.
- **Severity:** **MEDIUM.** Known surface; documented in #0108. Not the round-2 root cause but compounds it.

#### S12. DYDO_HUMAN identity scoping

- **File:** `Services/AgentRegistry.cs:107-114`, `ClaimAuto:469-475`, and various callsites.
- **What it does:** `GetCurrentHuman()` reads `DYDO_HUMAN` env var. Used to filter `GetFreeAgentsForHuman`, `MessageService.CheckOwnership`, and the nudge logic in `ClaimAuto`. Not used to derive *agent* identity, but used to scope *which agents the current process can interact with*.
- **Failure mode:** If DYDO_HUMAN is wrong (set by a stale parent shell, never set, or spoofed), `claim auto` selects an agent assigned to the wrong human — which may then be claimed by a different operator's process. Not a hijack of the agent record, but a hijack of the human-agent assignment.
- **Severity:** **LOW** in current single-operator usage. Worth noting for the multi-human future.

#### S13. Watchdog auto-resume identity bookkeeping

- **File:** `Services/WatchdogService.cs` (not re-read in this inquisition, but referenced via #0143, #0153, decision 022, architecture.md `Watchdog` section).
- **What it does:** Polls agent state every 10s, fires resume launchers when an agent's `.session.ClaimedPid` is dead and `LastResumeLaunchedAt` is stale. Resume bodies (per OS launcher) re-export `DYDO_AGENT=<agentName>` (LinuxTerminalLauncher.cs:60, MacTerminalLauncher.cs:47, WindowsTerminalLauncher.cs:80).
- **Hijack interaction:** If the watchdog mis-identifies an agent and resumes the wrong one, the resumed terminal has `DYDO_AGENT=<wrongAgent>` baked into the resume body, and every command in that shell flows through the hijacked primitives. Issue #0181 (saturate vs claim race) and #0143 (re-resume of already-resumed) are nearby concerns; not directly hijack-class but the resume body's DYDO_AGENT export is the same pivot point.
- **Severity:** **MEDIUM.** Indirect hijack vector — depends on the watchdog correctly identifying the agent, which other open issues (#0181) note can race.

### Confirmed reproductions

#### R1. `dydo agent role` writes to Charlie when DYDO_AGENT=Charlie and the calling process owns Zelda

**Status:** Reproduced 3× in production (LC operator, 2026-05-18; see issue #0183). In-process test reproduction dispatched to test-writer Dexter (`identity-hijack-repro-role-set`) — **see Scout reports below for the test code and result**.

The reproduction chain, in code terms (assumes calling process has DYDO_AGENT=Charlie inherited, Zelda is the actually-claimed agent, sid2 is the calling claude tab's session id, sid1 is Charlie's stored session id from a prior claim):

```
ExecuteRole("co-thinker", "test-task")                              // AgentLifecycleHandlers.cs:172
 ├─ registry.GetSessionContext()                                    // AgentRegistry.cs:1039
 │   └─ DYDO_AGENT="Charlie" → GetSession("Charlie") → return sid1
 ├─ registry.GetCurrentAgent(sid1)                                  // AgentRegistry.cs:924
 │   └─ DYDO_AGENT="Charlie" → GetSession("Charlie").SessionId == sid1 → return Charlie
 └─ registry.SetRole(sid1, "co-thinker", "test-task", out _)        // AgentRegistry.cs:703
     ├─ GetCurrentAgent(sid1) → Charlie (same path)
     └─ UpdateAgentState("Charlie", s => { s.Role = "co-thinker"; ... })  // WRITES CHARLIE'S state.md
```

Zelda's state.md is untouched. `dydo whoami` from the same process also returns Charlie (same primitives), so the operator never sees Zelda's record in their `whoami` output — the deception is total.

#### R2. `dydo wait` exit-code-2 explanation

When the calling process has DYDO_AGENT=Charlie and Charlie's own claude tab already has a live general-wait registered, `WaitCommand.WaitGeneral` (`Commands/WaitCommand.cs:83-91`) reads Charlie's `.waiting/_general-wait.json`, finds a Listening marker with a running PID, and returns `ExitCodes.ToolError` (exit 2). The stderr explanation is present but redirected to `/dev/null` (Linux/Mac resume bodies) or `Out-Null` (Windows resume body), so the operator sees only the exit code with no message.

This is not a separately filed bug — it's a direct consequence of S5 and the resume-body stderr suppression.

#### R3. Phantom-inbox NOTICE loop (operator's report)

Reproduced by the LC operator (issue #0183 timeline). The mechanism is: S4 creates a phantom file in agent A's inbox, attributed `from: <hijacked>`; A's UnreadMessages picks up the id (`MessageService.cs:88`); the guard's NOTICE (`GuardCommand.cs:991`) fires on every tool call for any process resolved to A; the operator's clear path (S1, S8) hijacks to a different agent and fails.

**Code-level evidence for the unreachable-by-design clear path:** `InboxService.ExecuteClear:67-113` resolves identity via the same primitives, so a phantom file in Zelda's inbox cannot be cleared by a process that resolves to Charlie. There is no `--force --file <path>` operator escape hatch in this command.

### Severity matrix

| Surface | Reachability | Blast radius | Observability | Severity |
|---|---|---|---|---|
| S0 (primitives) | Every identity command | All identity-aware logic | Hidden (whoami also lies) | CRITICAL |
| S1 (agent role write) | Every role-set | Other agent's role/permissions/task | Invisible to operator | CRITICAL |
| S7 (guard per-tool) | Every PreToolUse hook | Cross-agent RBAC, NOTICE delivery, audit | Audit logs the wrong agent | CRITICAL |
| S2 (agent release) | Every release | Other agent freed; reclaim window opens | Visible via `agent list` post-release | HIGH |
| S3 (dispatch sender) | Every dispatch | Inbox attribution + reply markers wrong | Inbox brief shows wrong sender | HIGH |
| S4 (msg sender) | Every msg | Phantom file in target's inbox, attribution wrong | File visible but origin lies | HIGH |
| S5 (wait exit 2) | Every resume + manual wait | General-wait never registers; deadlock | Stderr suppressed in resume bodies | HIGH |
| S8 (NOTICE deadlock) | Whenever S4 lands a phantom | Operator file IO stops cold | NOTICE fires loudly but no escape | HIGH |
| S6 (claim refuse) | Manual claim with stale env | Confusing refusal, no corruption | Visible error (misleading) | MEDIUM |
| S11 (worktree junction race) | Multi-worktree same agent name | Shared state.md write-write | #0108 known surface | MEDIUM |
| S13 (watchdog resume) | Auto-resume edge cases | Resumed shell starts hijacked | Symptoms then identical to S1-S8 | MEDIUM |
| S9 (.session-context fallback) | When verification fails AND single-Working-agent | Wrong sessionId returned (cross-human if applicable) | Compound: fires more readily when one agent is Working system-wide (common) | MEDIUM (revised up after Emma's compound analysis) |
| S10 (HandleExistingSession) | Only with `.pending-session` missing | Theoretical | Production goes through guard | LOW |
| S12 (DYDO_HUMAN) | Multi-human future | Claim-auto picks wrong human's agent | Visible (different name) | LOW |
| F11 (wait DoS) | Attacker sets DYDO_AGENT, runs `dydo wait` once | Victim cannot re-arm general-wait | Exit 2 (stderr suppressed in resume bodies) | HIGH |
| F12 (phase-1 race) | Two concurrent dydo commands | Wrong sessionId returned to reader in window | Briefly observable | MEDIUM |
| F10 (audit corruption) | Whenever F1 is exploited | Audit attribution falsified | Audit-replay invisible | HIGH (forensic) |
| F13 (watchdog env leak) | PowerShell startup window post-watchdog-spawn | Profile scripts see leaked DYDO_AGENT | OS-specific | LOW-MEDIUM |

### Findings

#### F1. `GetSessionContext()` trusts DYDO_AGENT without verifying calling-process ownership; the entire hijack class flows from this single function

- **Category:** bug (security boundary)
- **Severity:** CRITICAL
- **Type:** tested (Dexter scout — `IdentityHijackRoleSetTests.ExecuteRoleFlow_DydoAgentMismatchesActualClaim_HijacksRoleToEnvAgent` fails today; the direct-SetRole sibling test `SetRole_DydoAgentMismatchesPassedSession_WritesToActualSessionOwner` passes, isolating the pivot to `GetSessionContext`)
- **Evidence:** `Services/AgentRegistry.cs:1041-1046` (`GetSessionContext` env fast path) — returns `DYDO_AGENT`'s stored session id with no guard. `GetCurrentAgent`'s env fast path at `Services/AgentRegistry.cs:929-936` does gate on `envSession?.SessionId == sessionId` and is therefore NOT the hijack pivot on its own — it becomes complicit only because the poisoned sid from `GetSessionContext` makes the gate vacuously true downstream. The April-09 verified-fallback path was added to `.session-context` parsing but explicitly left the env fast path "unchanged" (`dydo/project/changelog/2026/2026-04-09/investigate-wait-flag-bug.md:60-63`). The fix surface is one function — hardening `GetSessionContext` alone closes the bug class.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/AgentRegistry.cs` lines 1039-1049 (`GetSessionContext`), 924-977 (`GetCurrentAgent`), 703-768 (`SetRole`); `Commands/AgentLifecycleHandlers.cs` lines 172-213 (`ExecuteRole`); `DynaDocs.Tests/Services/IdentityHijackRoleSetTests.cs` (full file).
- **Independent verification:** Traced the asymmetry by inspection. In `GetSessionContext`, the env-var branch unconditionally returns `GetSession(DYDO_AGENT)?.SessionId` with no PID/ownership check — the comment on line 1037 explicitly states "DYDO_AGENT env var bypasses the shared file entirely." In `GetCurrentAgent`, the env branch at line 934 gates on `envSession?.SessionId == sessionId`, so feeding it a truthful sid would correctly fall through to the hint/scan path. Dexter's two-test pair encodes exactly this asymmetry: `SetRole_DydoAgentMismatchesPassedSession_WritesToActualSessionOwner` calls SetRole directly with `sid_zelda` and passes; `ExecuteRoleFlow_DydoAgentMismatchesActualClaim_HijacksRoleToEnvAgent` replays ExecuteRole's `GetSessionContext → SetRole` sequence and is `[Fact(Skip = …)]` because it fails. The narrowing of the pivot to one function — not two — is correct.
- **Alternative explanations considered:** Could the env-path in `GetSessionContext` be a deliberate optimization for dispatched terminals where DYDO_AGENT is known-truthful? The April-09 changelog explicitly relies on that invariant. But the invariant breaks in three observable cases (S1, S5, S6) — inherited stale DYDO_AGENT after a manual claim, watchdog-resumed shells with DYDO_AGENT baked into the resume body, and any subshell of a hijacked process. The optimization isn't load-bearing (the verified `.session-context` path costs one file read) and is the trust-spine of the entire identity model.
- **Issue:** #0183 (already filed pre-inquisition — same root cause, no duplicate created)

#### F2. Round-2 hijack surface (`dydo agent role`) is unrelated to round-1 (#0108) and writes to other agents' state.md

- **Category:** bug
- **Severity:** CRITICAL
- **Type:** tested (reproduced 3× in production, code-level chain in R1, in-process test reproduction by Dexter scout — see Scout reports)
- **Evidence:** S1 above. The round-1 fix in `AgentSelector` does not cover this surface — the round-1 work was target selection; round-2 is identity *resolution*. The round-1 fix should remain.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Commands/AgentLifecycleHandlers.cs` lines 172-213 (`ExecuteRole`); `Services/AgentRegistry.cs` lines 703-768 (`SetRole`); `Services/AgentSelector.cs` (post-#0108 review per #0108 changelog); `dydo/project/changelog/2026/2026-05-18/fix-issue-0108-self-dispatch-hijack.md`.
- **Independent verification:** Confirmed the chain — `ExecuteRole` calls `registry.GetSessionContext()` (line 175) then `registry.GetCurrentAgent(sessionId)` (177) and `registry.SetRole(sessionId, …)` (184); `SetRole` at line 707 re-derives the agent via `GetCurrentAgent(sessionId)` then calls `UpdateAgentState(agent.Name, …)` at 737. The #0108 fix is in `AgentSelector` (the dispatch target-selection codepath) and does not intersect any of these lines. F2 is a separate surface from F9.
- **Alternative explanations considered:** Could the symptom be the same #0108 bug not yet rebuilt into the deployed binary at the LC site? Issue #0183's "Note on tool version" raises this. But the in-process repro (Dexter's failing test) runs against the current source on this branch, so it is decoupled from any deployment lag.
- **Issue:** #0183 (already filed pre-inquisition — covers the same user-observable surface; no duplicate created)

#### F3. Every identity-resolving callsite (~30 across Commands/ and Services/) is hijackable

- **Category:** bug (systemic)
- **Severity:** CRITICAL
- **Type:** obvious
- **Evidence:** Surfaces S1–S8 above. The pattern `var sessionId = registry.GetSessionContext(); var agent = registry.GetCurrentAgent(sessionId);` is duplicated as an idiom across `Commands/AgentLifecycleHandlers.cs`, `Commands/AgentLifecycleHandlers.cs`, `Commands/GuardLiftCommand.cs`, `Commands/InquisitionCommand.cs`, `Commands/GuardCommand.cs` (10× callsites), `Commands/ReviewCommand.cs`, `Commands/TaskCreateHandler.cs`, `Commands/WaitCommand.cs`, `Commands/WhoamiCommand.cs`, `Commands/WorktreeCommand.cs` (2× callsites), `Commands/WorkspaceCommand.cs`, `Services/MessageService.cs`, `Services/InboxService.cs` (2× callsites), `Services/DispatchService.cs` (3× callsites), and `Services/AgentRegistry.cs` internal callers. Fixing the bug at the primitive level (F1) closes the whole class; fixing it callsite-by-callsite would require ~30 edits and is brittle.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/AgentRegistry.cs` lines 1039-1049, 924-977, 703-768, 533-594; `Commands/AgentLifecycleHandlers.cs` 59-85 (`ExecuteRelease`), 172-213 (`ExecuteRole`); `Commands/WaitCommand.cs` 35-91; `Services/MessageService.cs` 7-91; `Services/DispatchService.cs` 10-75, 470-490; `Services/InboxService.cs` 35-113; `Commands/GuardCommand.cs` 132-262 (`Execute`/`ParseInput`/`HandleDydoBashCommand`).
- **Independent verification:** Spot-checked five callsites — every one I read uses the `GetSessionContext` → `GetCurrentAgent(sessionId)` pattern with the poisoned sid (ExecuteRelease:62-64, ExecuteRole:175-177, WaitCommand:38-40, MessageService:11-14, DispatchService:19-32, InboxService:38-40 and 70-72). One non-obvious exception: `GuardCommand.Execute` parses sessionId directly from Claude Code's hook input JSON (line 145), so guard-side identity resolution is NOT poisoned — this means LogAuditEvent in the guard correctly identifies the calling agent. The poisoned path is the dydo *subprocesses* spawned by the user's `dydo …` typed commands.
- **Alternative explanations considered:** Could a callsite-by-callsite fix be cleaner than a primitive-level fix? In principle yes, but each callsite would need its own PID-binding logic; the primitive is the natural chokepoint and was already singled out by the April-09 fix for the file-fallback path. The cited list is not exhaustive but the conclusion holds: the bug is at the primitive layer.
- **Issue:** #0183 (this is a systemic-scope statement about F1, not a separate bug; tracked alongside the root-cause issue)

#### F4. Existing tests `GetSessionContext_PrefersDydoAgentEnvVar_OverFile` and `GetCurrentAgent_PrefersDydoAgentEnvVar_OverHintFile` do not differentiate buggy from correct behavior

- **Category:** test encodes the wrong contract (and one is shallow)
- **Severity:** HIGH
- **Type:** obvious (refined by Emma scout)
- **Evidence:** `DynaDocs.Tests/Services/AgentRegistryTests.cs:2861-2944`.
  - **Test 1 (`GetSessionContext_PrefersDydoAgentEnvVar_OverFile`) is shallow and mis-named.** The setup writes `StoreSessionContext("file-session-111")` then immediately overwrites with `StoreSessionContext("agent-session-222")` *before* `ClaimAgent`. After setup, both `.session-context` AND Adele's per-agent `.session` contain `agent-session-222`. The assertion `Assert.Equal(agentSession.SessionId, sessionId)` is true under any implementation — env-var path, file path, or any hypothetical fix. The test does not actually verify env-var priority over file despite its name.
  - **Test 2 (`GetCurrentAgent_PrefersDydoAgentEnvVar_OverHintFile`) does distinguish env vs hint priority** but runs in a single test process where the calling-PID-owns-target invariant is trivially true. A PID-binding fix retains the env path *and* adds ownership verification, so this test still passes.
  - **Net:** Neither test will fail under a correct fix that retains the env-var path and adds PID binding. But the test names and bodies codify "env var is authoritative" as desired intent. A maintainer reading these tests will conclude the env path is a feature, not a bug. The tests need rewriting alongside any fix — not to delete the assertions but to add contrast tests where DYDO_AGENT names a *different* agent than the calling process's actual claim and assert ownership verification rejects the env-var hint.
- **Judge ruling:** CONFIRMED
- **Files examined:** `DynaDocs.Tests/Services/AgentRegistryTests.cs` lines 2861-2944 (both tests); `Services/AgentRegistry.cs` 1039-1049, 924-977 (the implementations under test).
- **Independent verification:** Traced Test 1's setup line-by-line — `StoreSessionContext("file-session-111")` then `StoreSessionContext("agent-session-222")` (clobbers the first) then `ClaimAgent("Adele")` (Adele's `.session` now holds "agent-session-222"). At assertion time both `.session-context` and Adele's per-agent `.session` contain the same string. The env-path returns `GetSession("Adele").SessionId == "agent-session-222"`; the file-path also returns "agent-session-222". The assertion is true under both implementations and under any PID-binding hardening. Test 2 likewise: the sessionId passed in matches Adele's `.session`, and the slow-scan fallback also returns Adele after the hint mismatch — env-path is not load-bearing for the assertion. The names ("Prefers...EnvVar") misdirect a future maintainer.
- **Alternative explanations considered:** Could the tests be intentionally documenting a desired priority order rather than verifying the env-path is load-bearing? Possibly — but if so the names should be "EnvVar wins when present", not "Prefers EnvVar over file/hint". And the inquisition's recommendation (rewrite with a hijack-style setup where DYDO_AGENT names a DIFFERENT agent than the calling process's claim) is the only way the tests can detect the F1 bug. The current tests cannot distinguish the buggy implementation from a correct one.
- **Issue:** #0189

#### F5. `ResolveSessionFallback` does not filter by `AssignedHuman == currentHuman` despite the April-09 plan

- **Category:** bug (latent, multi-human)
- **Severity:** LOW-MEDIUM
- **Type:** obvious
- **Evidence:** `Services/AgentSessionManager.cs:205-233`. The plan (`dydo/project/changelog/2026/2026-04-09/investigate-wait-flag-bug.md:51, 202`) says "Filter by `assignedHuman == currentHuman`. In practice, the initial terminal runs one agent. If ambiguous, return null." The implementation iterates `_agentNames` and returns the single Working agent or null if ambiguous — no human filter. The April-09 review note ("ResolveSessionFallback omits plan's assignedHuman filter — safe in practice") acknowledges this consciously. In single-operator usage today, safe. Worth flagging.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/AgentSessionManager.cs` lines 205-233 (`ResolveSessionFallback`), 200-204 (XML doc claiming the filter), 154-182 (`GetSessionContext` callsite); `Services/AgentRegistry.cs` 107-114 (`GetCurrentHuman`).
- **Independent verification:** Read the loop at 212-225 — it iterates `_agentNames`, accepts any agent whose `state?.Status == AgentStatus.Working`, returns the single matching `session.SessionId` or null on ambiguity. There is no `AssignedHuman` filter and no call to `_getHumanForAgent` or `GetCurrentHuman`. The XML summary at line 202 ("scans all agents for a working agent assigned to the current human") is documentation drift — the method does not do what the comment says.
- **Alternative explanations considered:** Could the ambiguity-guard at line 222 be sufficient? It bounds the case to "exactly one Working agent system-wide" — narrow, but the failure mode is silent cross-human resolution rather than refusal, which is the worse outcome. Severity stays LOW for single-human usage but the comment/code drift is itself a defect.
- **Issue:** #0190

#### F6. `dydo wait` background exit-code-2 stderr is suppressed by resume bodies

- **Category:** missing-doc / observability
- **Severity:** MEDIUM
- **Type:** obvious
- **Evidence:** `Services/LinuxTerminalLauncher.cs:61` (`(dydo wait >/dev/null 2>&1 &)`), `Services/MacTerminalLauncher.cs:63` (same), `Services/WindowsTerminalLauncher.cs:83` (`Start-Process -WindowStyle Hidden -FilePath dydo -ArgumentList 'wait' | Out-Null`). The stderr explanation from `WaitCommand.WaitGeneral:88-91` ("A general wait is already active for {agent} (PID {pid}). Refusing to register a duplicate.") never reaches the operator from a resume context. Combined with S5, this is *exactly* the "exit code 2, no useful stderr" symptom in #0183. **Note:** redirecting stderr is defensible for a background wait — but failing silently in the *registration* phase is a debuggability hole.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/LinuxTerminalLauncher.cs` lines 55-75 (`BuildResumeBashCommand`); `Services/MacTerminalLauncher.cs` 43-66 (`BuildResumeShellComponents`); `Services/WindowsTerminalLauncher.cs` 75-105 (`GetResumeArguments`); `Commands/WaitCommand.cs` 78-91 (`WaitGeneral` registration-phase error path).
- **Independent verification:** Confirmed all three launchers verbatim — Linux line 61 redirects both stdout and stderr to `/dev/null`, Mac line 63 does the same, Windows line 83 pipes through `Out-Null`. The exact stderr string at WaitCommand.cs:88-89 is a complete diagnostic that the operator never sees in a resume context. There are also three other ToolError paths in WaitGeneral (lines 142, 146, 150) and parallel paths in WaitForTask (184, 187, 191) whose stderr is similarly lost.
- **Alternative explanations considered:** Could the suppression be deliberate for noise reduction on a background poll? Yes for the steady-state poll loop, but the *registration-phase* failure is a one-shot diagnostic about why the wait never started. Logging this to a marker file or routing to a side channel is the conventional pattern for background-process diagnostics; nothing in the codebase explains why this one was dropped.
- **Issue:** #0191

#### F7. NOTICE handler has no operator escape hatch when the cited file is in an unreachable inbox

- **Category:** bug (deadlock)
- **Severity:** HIGH
- **Type:** obvious
- **Evidence:** `Commands/GuardCommand.cs:991-1042` self-heals "id in state.md, file absent" but does NOT handle the inverse: "file present in inbox of an agent the calling process can't transition to / clear." `Services/InboxService.cs:67-113` (`ExecuteClear`) has no `--force --file <path>` option to bypass UnreadMessages bookkeeping. Together: a phantom-id NOTICE in agent A's inbox + DYDO_AGENT pointing elsewhere is an unrecoverable file-IO state without manual filesystem intervention. The phantom is a downstream effect of S4; the deadlock is a separable bug from the hijack and warrants its own fix.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Commands/GuardCommand.cs` lines 988-1042 (`NotifyUnreadMessages`); `Services/InboxService.cs` 67-147 (`ExecuteClear`, `ClearAll`, `ClearById`); `Services/MessageService.cs` 53-91 (`DeliverInboxMessage` — phantom-file creator).
- **Independent verification:** Read NotifyUnreadMessages — the self-heal at lines 998-1010 only fires when `FindMessageInfo(workspace, msgId) == null` (file absent). The inverse branch (file present, calling agent cannot resolve to the inbox owner) has no handler. The NOTICE body at lines 1022-1041 instructs the operator to run `dydo inbox clear --id <id>`, which resolves through ExecuteClear's `GetCurrentAgent(GetSessionContext())` — under hijack this resolves to the wrong agent, ClearById can't find the id, returns "No inbox item with ID". No `--force`/`--file` escape exists in the InboxService surface. The deadlock is structurally complete.
- **Alternative explanations considered:** Could the operator use `dydo guard lift` to bypass NOTICE? The lift mechanism is per-agent RBAC; NOTICE is the pre-RBAC layer in NotifyUnreadMessages and fires regardless of lift. Confirmed by reading the call order in GuardCommand.HandleWriteOperation (NotifyUnreadMessages at line 278 runs before the IsGuardLifted check at line 297). The escape requires manual filesystem intervention outside any guarded process — operationally severe.
- **Issue:** #0192

#### F8. `dydo agent claim X` does not refuse / warn when `DYDO_AGENT` is set to a different agent

- **Category:** missing-defense
- **Severity:** MEDIUM
- **Type:** obvious
- **Evidence:** `Commands/AgentLifecycleHandlers.cs:9-56` (`ExecuteClaim`). The claim succeeds; the env var remains stale; the next role-set hijacks. This is the defensive guardrail Adele suggested as Fix #2 in their brief. With the F1 fix in place, this defense is redundant for correctness but valuable for operator clarity (so the operator unsets the env var instead of getting a silent-then-discovered hijack).
- **Judge ruling:** CONFIRMED
- **Files examined:** `Commands/AgentLifecycleHandlers.cs` lines 9-56 (`ExecuteClaim`); `Services/AgentRegistry.cs` 245-330 (`ClaimAgent`, `ResolveSessionId`, `ValidateClaimPreconditions` per surface S6 references).
- **Independent verification:** Read ExecuteClaim end-to-end — it calls `ClaimAgent(name, …)` (line 43) and `ClaimAuto(…)` (line 15) with no inspection of DYDO_AGENT. The S6 analysis correctly notes that `ValidateClaimPreconditions` will refuse the claim with a misleading message when DYDO_AGENT names a *different* agent that already holds a session, but neither command surfaces the env-var mismatch as the actual cause. The operator workaround (`unset DYDO_AGENT`) is not in any error message I could find.
- **Alternative explanations considered:** Could the claim be intentionally non-defensive on the assumption that DYDO_AGENT is always either unset or correct? That assumption breaks the moment a parent shell exports a stale value (S1, S5, S6). The defense costs one env-var read and one branch, and produces a much better operator experience.
- **Issue:** #0193

#### F9. Round-1 fix (#0108) addressed target selection, not sender identity

- **Category:** scope-narrowing (status note, not a new bug)
- **Severity:** INFORMATIONAL (this is context for the judge)
- **Type:** obvious
- **Evidence:** `Services/AgentSelector.cs` filters senderName from the pool and refuses to==senderName, but the *senderName* itself flows from `DispatchService.Execute:18-32` via `GetCurrentAgent(GetSessionContext())` — the hijacked primitive. If the sender is itself hijacked, AgentSelector's filter operates on the wrong agent. The fix is still correct for what it addresses (target self-dispatch); it's just not the whole class.
- **Judge ruling:** N/A (informational — accepted as context, not a separate bug)
- **Files examined:** `Services/DispatchService.cs` lines 10-75 (`Execute`); `dydo/project/changelog/2026/2026-05-18/fix-issue-0108-self-dispatch-hijack.md`.
- **Independent verification:** Read DispatchService.Execute — sessionId is derived from `GetSessionContext()` (line 19), sender from `GetCurrentAgent(sessionId)` (32), senderName from `sender?.Name` (33). Both feed the poisoned primitives under hijack. The AgentSelector filter at the downstream selection point operates on `senderName` post-hijack, so the round-1 fix correctly closes target self-dispatch when the sender resolution is honest and is correctly characterized as not closing the broader round-2 surface. No bug to file — this is scope clarification.
- **Alternative explanations considered:** N/A — this is a status note about the prior fix's scope, not a new defect claim.

#### F10. Audit logger inherits the hijack — silently falsifies the forensic trail

- **Category:** bug (forensics / observability)
- **Severity:** HIGH
- **Type:** obvious (Emma scout)
- **Evidence:** `Commands/GuardCommand.cs:1453-1475` — `LogAuditEvent` resolves identity via `registry.GetCurrentAgent(sessionId)` (line 1465) and `registry.GetCurrentHuman()` (line 1466). Every audit event under a hijacked session records the *hijacked* agent and the *hijacked* human as the actor. The audit trail is the last line of defense for after-the-fact detection of identity bugs (it's how the inquisition system reconstructs what happened); under hijack the audit corrupts itself rather than leaving a detectable signal. F1's fix closes this transitively — but it's worth a separate finding because the audit-attribution corruption is what makes the bug class hardest to retro-investigate (witness Q1: the LC hijack #2 cannot be precisely reconstructed because the audit attributes the role-set to Charlie, not to the actual calling process). **The fix should be paired with a one-time audit-replay validation against the LC session data to confirm whether the historical attribution can be corrected post-hoc, or whether the LC forensic trail is permanently muddied.**
- **Judge ruling:** CONFIRMED — but the cited evidence is wrong and the severity should be revised down to MEDIUM.
- **Files examined:** `Commands/GuardCommand.cs` lines 1-262 (entry/ParseInput), 627-678 (`HandleDydoBashCommand`), 1453-1475 (`LogAuditEvent`); `Services/AgentRegistry.cs` 2499-2520 (`LogLifecycleEvent`), 445-452 (Claim call), 561-565 (Release call), 760-765 (SetRole call).
- **Independent verification:** Read `GuardCommand.ParseInput` (line 131) and `Execute` (167) — the sessionId is parsed *directly* from Claude Code's hookInput JSON at line 145, not derived from `GetSessionContext()`. Therefore the `registry.GetCurrentAgent(sessionId)` call inside `LogAuditEvent` is fed the *truthful* session id of the calling process; the env-path gate at AgentRegistry.cs:934 then correctly fails (because the hijacked DYDO_AGENT's stored sid doesn't match the truthful sid) and the slow scan finds the actual owner. **The guard's own audit events therefore correctly identify the calling agent under hijack.** The real audit-attribution corruption is at `Services/AgentRegistry.cs:2504` (`LogLifecycleEvent`) callers — `ClaimAgent:445`, `ReleaseAgent:561`, and especially `SetRole:760`. Those three callers pass the *poisoned* sid (sourced from `GetSessionContext()` upstream) together with the explicit `agent.Name` that was already resolved against that poisoned sid. So Role/Release/Claim audit events DO record the hijacked agent — but every other event in the same session does not. Brian's claim ("every audit event") is too strong; the corruption is bounded to lifecycle events, and a cross-correlated audit replay can still reconstruct who issued the offending command (because the preceding bash-intercept event uses the truthful sid). The DYDO_HUMAN part of the claim is also wrong under the canonical hijack scenario (only DYDO_AGENT is spoofed; DYDO_HUMAN stays correct).
- **Alternative explanations considered:** Brian may have skimmed `LogAuditEvent` without tracing where `sessionId` originates — an easy mistake given the function looks identical to the lifecycle helper. The error is the citation, not the underlying observation that *some* audit attribution is broken. Filing a corrected issue rather than disputing the spirit of the finding.
- **Issue:** #0194

#### F11. Duplicate-wait DoS — hijacked `dydo wait` can hold a victim's wait slot indefinitely

- **Category:** bug (denial of service, hijack-class variant)
- **Severity:** HIGH
- **Type:** obvious (Emma scout)
- **Evidence:** `Commands/WaitCommand.cs:78-91` (`WaitGeneral`). The duplicate-wait refusal at line 85-91 keys on the *hijacked* agent's `.waiting/_general-wait.json` marker. An attacker process that sets `DYDO_AGENT=X` and runs `dydo wait` once registers a Listening marker for X with the attacker's PID (`CreateListeningWaitMarker`, line 108). Every subsequent `dydo wait` for X — including X's own legitimate terminal's re-arm — hits the duplicate-wait check, sees the attacker's PID alive, and exits 2. The attacker has no Claude ancestor (running from a plain shell), so the ancestor-death gate at lines 145-146 never trips. The attacker can hold the slot for an arbitrarily long time, blocking X from re-arming its inbox listener. **This is a separate hijack-class variant** — it doesn't mutate state, it withholds wait-channel availability. Reachable as soon as F1 is exploitable. Closes in tandem with F1 if the wait command verifies caller ownership; otherwise needs its own ownership check at line 108 (refuse to register a wait marker for an agent the caller doesn't own).
- **Judge ruling:** CONFIRMED
- **Files examined:** `Commands/WaitCommand.cs` lines 35-156 (`Execute`, `WaitGeneral`); `Services/AgentRegistry.cs` 1063-1099 (`GetWaitingDir`, `CreateWaitMarker`, `CreateListeningWaitMarker`); `Utils/ProcessUtils.cs` (via grep of `FindAncestorProcess` usage).
- **Independent verification:** Walked the WaitGeneral path step-by-step. With DYDO_AGENT=X set by attacker: `GetSessionContext` returns X's stored sid (poisoned), `GetCurrentAgent(X-sid)` returns X via the env path (envSession.SessionId == X-sid is vacuously true), then `agent.Name = X` flows into `GetWaitMarkers(X)` (line 83) and `CreateListeningWaitMarker(X, …, Environment.ProcessId)` (line 108). The attacker's PID is stored. The two PID-liveness gates inside the wait loop check the *attacker's* `parentPid` and `claudePid` (lines 141, 145) — if the attacker has no claude ancestor, `claudePid` is null and that gate never trips; if `parentPid` is a persistent shell, the loop never exits. X's legitimate terminal then attempts the same path, hits the duplicate-wait refusal at lines 85-91 (attacker's PID alive), exits 2. Confirmed by inspection — no caller-ownership verification anywhere in the pre-CreateListeningWaitMarker path.
- **Alternative explanations considered:** Could the existing PID-liveness gate at lines 85-91 protect against this? It only protects against a stale marker (dead PID); a live attacker PID passes the check. Could the operator notice via `whoami` mismatch? Not from inside the attacker's tab (whoami would also lie under hijack). The variant is real and separately filable. With F1 fixed, the attacker still gets X resolution under env hijack and the DoS path remains — needs the ownership check at line 108 even after F1.
- **Issue:** #0195

#### F12. Phase-1 / Phase-2 `StoreSessionContext` race window publishes unverifiable state

- **Category:** bug (concurrency, latent)
- **Severity:** MEDIUM
- **Type:** obvious (Emma scout)
- **Evidence:** `Commands/GuardCommand.cs:629-673`. Phase 1 (line 630) writes `.session-context` in single-line legacy format (no agent name); Phase 2 (line 672) writes the verified two-line format. Between phases, a concurrent reader's `AgentSessionManager.GetSessionContext()` parses single-line format and returns the sessionId AS-IS (lines 167-168 of `AgentSessionManager.cs`) — bypassing verification and `ResolveSessionFallback`. Two concurrent dydo commands from different terminals interleave their phase-1 writes, each briefly clobbering the other's sessionId for any reader racing into the window. Verified-resolution catches mismatches only after phase 2 has completed. Combined with F5 (missing human filter in `ResolveSessionFallback`), an adversarial spammer who keeps the file in legacy format can steer the fallback toward whichever single Working agent is on the system. **Reachable without F1** — this is a separate race surface that the April-09 fix knowingly left in place ("Option B — safe and avoids restructuring the guard flow"). Worth filing as a separate issue but lower priority than F1.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Commands/GuardCommand.cs` lines 627-678 (`HandleDydoBashCommand`); `Services/AgentSessionManager.cs` 149-198 (`GetSessionContext`, `ParseSessionContext`), 239-259 (`StoreSessionContext`).
- **Independent verification:** Confirmed both writes: line 630 unconditional `registry.StoreSessionContext(sessionId)` (no agent name → single-line format per `StoreSessionContext`'s ternary at line 245), and lines 672-673 `if (agent != null) registry.StoreSessionContext(sessionId, agent.Name)` (two-line format). Then in `GetSessionContext` (line 167-168): `if (string.IsNullOrEmpty(agentName)) return sessionId;` — yes, returns the single-line value AS-IS with no verification and no fallback. The window between the two writes contains the agent-resolution work (lines 632-668) and is non-trivial in duration on a busy filesystem. Race surface confirmed.
- **Alternative explanations considered:** Could the verification on the per-agent `.session` file at lines 171-176 catch the bad value downstream? Only when the reader gets to phase 2's two-line write — between phases the reader takes the unverified return path. The April-09 changelog explicitly chose this tradeoff ("Option B") so the race is documented; the inquisition's recommendation to file separately is correct because it's a distinct surface from F1.
- **Issue:** #0196

#### F13. Watchdog child processes inherit DYDO_AGENT with a brief unverified window (Windows-specific)

- **Category:** bug (env-var leakage, lower-impact)
- **Severity:** LOW-MEDIUM
- **Type:** obvious (Emma scout)
- **Evidence:** `Services/WatchdogService.cs:153-164` builds `ProcessStartInfo` with no env-var scrubbing. WatchdogService doesn't consume DYDO_AGENT itself (grepped clean), but launches resume terminals via the launcher classes. The injected `$env:DYDO_AGENT='{agentName}'` (Windows: `WindowsTerminalLauncher.cs:80`) overrides only after PowerShell parses the `-Command` string. During PowerShell startup (profile scripts), DYDO_AGENT still holds the watchdog's inherited value. On Linux the window is narrower (export is the first bash statement); on Mac, effectively zero (Terminal.app's own bash bookkeeping). **Concrete unsafe scenario:** balazs's terminal A runs as Adele (`DYDO_AGENT=Adele`); A dispatches Brian; watchdog inherits DYDO_AGENT=Adele as a child; watchdog auto-resumes Brian (crashed); the launched wt.exe inherits Adele; a PowerShell profile script that invokes `dydo whoami` during startup runs as Adele for that instant. Closes by adding `psi.Environment["DYDO_AGENT"] = agentName` (or `Remove("DYDO_AGENT")` for agent-agnostic spawns) in every launcher `ProcessStartInfo`. Defense-in-depth, not a primary attack surface; not a F1 prerequisite.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/WatchdogService.cs` lines 140-180 (`ProcessStartInfo` build); `Services/WindowsTerminalLauncher.cs` 75-105 (`GetResumeArguments`); `Services/LinuxTerminalLauncher.cs` 55-75; `Services/MacTerminalLauncher.cs` 43-66.
- **Independent verification:** Read the watchdog `ProcessStartInfo` block — it sets `UseShellExecute = false`, `CreateNoWindow = true`, `WorkingDirectory`, and the standard-stream redirects; no `Environment` property manipulation. Emma's grep result that WatchdogService doesn't itself consume DYDO_AGENT is consistent with the code I read. On the Windows launcher, `agentEnv` is the first segment of the `-Command` string (line 80, 95, 104), executed after `wt.exe` and PowerShell startup parse the script — profile scripts run before that statement. The Linux export is the first statement of the bash command (line 60), so its window is genuinely narrower but not zero. The Mac path uses Terminal.app's `do script` which also delays env application until the script runs.
- **Alternative explanations considered:** Could the brief Windows window be operationally negligible? It's small in real time but real in mechanism — any profile script that touches `dydo` (even `dydo whoami` in a starship-prompt-style config) executes under the inherited identity. Defense-in-depth is cheap (add one line to each launcher's PSI). Severity LOW-MEDIUM stands.
- **Issue:** #0197

### Hypotheses not reproduced

- **HP1:** `HandleExistingSession` Path-B (same-session reclaim) leaves residual hijackable state. Not reproduced; analysis (S10) shows `.pending-session` carries the calling-process sid through the guard interception, so the same-session check operates on the correct sid. **Status:** not an issue in production.
- **HP2:** `ResolveSessionFallback` (S9, F5) produces actual cross-agent confusion in single-human projects. Not reproduced — the fallback short-circuits to null when ambiguous, which surfaces as an error rather than a hijack. **Status:** latent for multi-human, not active in current scope.
- **HP3:** Watchdog auto-resume mis-identifies and resumes the wrong agent, baking DYDO_AGENT=wrong into the resumed shell. Not reproduced in this inquisition — depends on #0181 / #0143 racing, which are open but separate. **Status:** plausible indirect hijack vector; flagged as adjacent.

### Open questions

- **Q1:** What is the precise sequence of events in LC's hijack #2 (the spontaneous Charlie → code-writer / migration-edit-protection-tooling rewrite while waiting)? The mechanism is clear (some other process with DYDO_AGENT=Charlie ran `dydo agent role`), but the *which process and how it got DYDO_AGENT=Charlie* is not directly observable from the report. **Audit replay won't resolve this** — per F10, the audit logger inherits the hijack and records the hijacked agent (Charlie) as the actor, not the actual calling process. The audit trail for LC hijack #2 is corrupted at the source. To reconstruct the actual chain, you'd need either (a) the process ancestry tree at the moment of the role-set (lost), or (b) reproducing the conditions in a controlled environment with PID-level instrumentation. **The bug class is *retro-investigation-resistant* — meaning fixing F1 is more important than diagnosing LC hijack #2.**
- **Q2:** Is there a third bug class adjacent to F1 — e.g., does the `.session-agent` hint file ever get out of sync with both `.session-context` and `.session` in a way the verified-fallback can't detect? Not investigated; the LC report doesn't mention hint-file corruption, but a deliberate scan of `.session-agent` writers vs. claim/release sequences would be a good follow-up inquisition.
- **Q3:** Does the hint file scan in `GetCurrentAgent` (`AgentRegistry.cs:939-953`) introduce a different hijack vector if a stale hint persists across a release? `ReleaseAgent` deletes `.session` (line 555-556) and the hint (line 558) — but if the hint deletion fails (try/catch swallows), the next caller may resolve to a stale agent name whose .session is gone. Not investigated in detail; flagged for code-writer.
- **Q4:** The April-09 fix wrote `.session-context` in two phases (Phase 1 no agent name, Phase 2 with agent name). What happens if a concurrent claim writes its own Phase 1 between the first claim's Phase 1 and Phase 2? `GuardCommand.cs:629-673` shows the Phase-1 → resolve → Phase-2 sequence without a lock; under contention this can produce a `.session-context` with sid_A and agentName_B if interleaving is just right. Did April-09's risk analysis consider this? `WhoamiConcurrencyTests` covers single-overwrite races but not interleaved two-phase writes. Likely fine because the verified-fallback rejects mismatch and falls back to the scan, but worth confirming.

### Test-coverage gaps

- **TG1.** **No test exercises the hijack scenario** — DYDO_AGENT points at agent X while the calling process owns agent Y with a different session id. The two existing DYDO_AGENT tests (`AgentRegistryTests.cs:2861, 2906`) set DYDO_AGENT to the same agent that's claimed; they cannot detect the bug. **A regression test for the fix should** set DYDO_AGENT to Charlie, claim Zelda, call `GetCurrentAgent(zeldaSid)`, and assert Zelda is returned. See Dexter's repro test in Scout reports.
- **TG2.** **The two encoded-bug tests** (`GetSessionContext_PrefersDydoAgentEnvVar_OverFile`, `GetCurrentAgent_PrefersDydoAgentEnvVar_OverHintFile`) need revision — not deletion. Rewrite their setup so the DYDO_AGENT agent is actually owned by the calling process (verify via mock PID hook) and re-assert. Without this rewrite, the fix will produce a false regression and either the tests will be deleted (losing coverage) or worked around (entrenching the bug).
- **TG3.** **No test for `ResolveSessionFallback` under multi-human** (F5). When/if dydo grows multi-human support, this needs a "fallback returns my-human's session, not someone-else's" test.
- **TG4.** **No test for the phantom-inbox NOTICE deadlock** (F7). A test would set up agent A with UnreadMessages=[x], file present, then assert the NOTICE behavior from a process resolving to agent B — current behavior is the deadlock; desired behavior is either NOTICE-on-A-only-when-resolving-to-A, or an operator escape hatch.
- **TG5.** **No test for cross-process role-set racing.** Two `dydo agent role` invocations against different agents from two processes — current behavior depends on `DYDO_AGENT` per process; a regression test would assert each process mutates its own agent's record regardless of env-var inheritance.
- **TG6.** **No integration test for stderr-suppressed wait failure** (F6). A test that runs `dydo wait` background under a stale-DYDO_AGENT condition and asserts the failure mode is reported visibly (whatever the chosen mechanism: log file, marker file, stderr-to-file fallback).
- **TG7.** **No test for F11 duplicate-wait DoS.** A test that has process A register a Listening marker for agent X with A's PID, then has process B (with `DYDO_AGENT=X` set) attempt `dydo wait`, and asserts that B does not register a wait marker for X (or that B's wait registration verifies caller ownership and refuses).
- **TG8.** **No test for F12 phase-1 race window.** A test that interleaves two `HandleDydoBashCommand` phase-1 writes from different sessions and asserts a concurrent reader either gets verified-correct identity or gets a clean `null` (no half-baked sessionId).
- **TG9.** **No test for F10 audit attribution under hijack.** A test that triggers an identity-hijacked operation (post-fix, this would be impossible; pre-fix it's a regression test for the corruption) and asserts the audit event names the actual calling agent, not the env-var agent.
- **TG10.** **`IdentityHijackRoleSetTests.cs` (Dexter scout, new file)** — pre-fix the file contains a `[Fact(Skip = "hijack reproducer — un-skip after fix")]` reproducer for F1. Removing the `Skip` after the fix lands flips the test into a regression. **Confirm this file persists post-merge** (it's in the worktree).

### Scout reports

#### Dexter (test-writer, `identity-hijack-repro-role-set`)

**Result:** MIXED — hypothesis partially confirmed; the bug exists, but one layer deeper than the brief located it.

**Test file:** `DynaDocs.Tests/Services/IdentityHijackRoleSetTests.cs` (new). Marked `[Fact(Skip = "hijack reproducer — un-skip after fix")]` on the failing assertion to keep gap_check green until the fix lands; assertion shape preserved.

**Test 1 — `SetRole_DydoAgentMismatchesPassedSession_WritesToActualSessionOwner`:**
Setup: Charlie and Zelda both claimed with distinct session ids; `DYDO_AGENT=Charlie`; call `registry.SetRole(sid_zelda, "co-thinker", "test-task", ...)` *directly*. Asserts Zelda's state.md gets role=co-thinker.
**Result: PASS.** SetRole called with a truthful session id does NOT hijack.

**Test 2 — `ExecuteRoleFlow_DydoAgentMismatchesActualClaim_HijacksRoleToEnvAgent`:**
Same setup; replays `AgentLifecycleHandlers.ExecuteRole`'s sequence verbatim: `sessionId = registry.GetSessionContext()` → `registry.SetRole(sessionId, ...)`. Asserts Zelda's state.md gets role=co-thinker.
**Result: FAIL.** Zelda's state.md has `role: null`. The role landed on Charlie. Failure message: `Assert.Contains() Failure: Sub-string not found "role: co-thinker"` against Zelda's state.md. Hijack reproduced through the user-observable code path.

**Finding (Dexter's diagnosis, paraphrased):**
- `GetCurrentAgent`'s DYDO_AGENT fast path (`AgentRegistry.cs:929-936`) gates on `envSession?.SessionId == sessionId`. With distinct sessions for Charlie/Zelda, when the caller passes `sid_zelda` the fast path is BYPASSED (Charlie's session id ≠ `sid_zelda`); the slow scan (lines 959-969) then correctly finds Zelda. So a direct `SetRole(sid_zelda, ...)` is NOT vulnerable.
- `GetSessionContext` (`AgentRegistry.cs:1039-1049`) trusts DYDO_AGENT unconditionally — no session-id guard. It returns Charlie's session id, `ExecuteRole` feeds that to `SetRole`, and the write lands on Charlie. **This is the root of the hijack.**
- Net: the hijack manifests through any caller that uses `GetSessionContext` to derive the session id (`ExecuteRole`, and every other "dydo agent ..." subcommand that follows the same pattern).

**Hypothesis verdict (Dexter):** PARTIALLY CONFIRMED.
- At the `SetRole` entry point with a truthful sessionId: NOT REPRODUCED (Test 1 passes).
- At the `AgentLifecycleHandlers.ExecuteRole` user surface: CONFIRMED (Test 2 fails).

This is incorporated into F1 above — the fix surface narrows to `GetSessionContext` alone.

#### Emma (reviewer, `identity-hijack-surface-scan`)

Full surface scan. Key results:

**1. No other DYDO_AGENT reads in production.** Confirmed: `Services/AgentRegistry.cs:930` and `:1041` are the only production reads. All other matches are writes (terminal launchers) or test fixtures.

**2. DYDO_HUMAN and DYDO_WINDOW.**
- `DYDO_HUMAN` is the explicit "human identity" env var, read in 8+ places (`AgentRegistry.GetCurrentHuman()`, claim/release flows, `GuardLiftCommand`, `GuardCommand` audit logger, etc.). Same env-var-as-identity pattern: no PID/uid verification anywhere. A process that sets `DYDO_HUMAN=otheruser` is treated as `otheruser` by `claim`, `claim auto`, `role`, `guard lift`, audit attribution. Consistent with design intent ("DYDO_HUMAN identifies the operator of this terminal") but worth flagging because the same failure pattern applies. Single-human projects don't expose this; multi-human projects need an audit before exposure.
- `DYDO_WINDOW` is window-routing only (`DispatchService.cs:448`), not an identity surface.
- No other `DYDO_*` env var is read in production.

**3. The two encoded-bug tests are weaker than they appear.**
- `GetSessionContext_PrefersDydoAgentEnvVar_OverFile` (`AgentRegistryTests.cs:2861-2892`) is **shallow and mis-named.** The setup overwrites `StoreSessionContext("file-session-111")` with `StoreSessionContext("agent-session-222")` *before* `ClaimAgent`, so by the time the assertion runs both the `.session-context` file AND Adele's `.session` contain the same value `agent-session-222`. The assertion `Assert.Equal(agentSession.SessionId, sessionId)` is vacuously true under any implementation that reads either source. A PID-binding fix would still pass this test. **The test does not actually differentiate env-var priority from file priority despite its name.**
- `GetCurrentAgent_PrefersDydoAgentEnvVar_OverHintFile` (`AgentRegistryTests.cs:2906-2944`) DOES distinguish the priority (env=Adele vs hint=Brian, asserts Adele wins), but it runs in a single test process so the calling-PID-owns-target invariant is trivially true. A PID-binding fix would still pass this test. The test name and body codify "env var beats hint file" as a desired invariant, which a maintainer will read as "the env-var fast path is intended — don't remove it". The bug isn't in the priority order; it's in the missing ownership check.
- **Net (Emma):** Neither test will fail under a correct fix that retains the env-var path and adds PID binding. But the test names mislead a future maintainer into thinking the env path is itself the intended invariant. They need rewriting alongside any fix.

**4. `ResolveSessionFallback` missing human filter — confirmed (F5).** The XML doc on line 202 claims the filter exists; the body does not implement it. Ambiguity-guard at line 222 mitigates by returning `null` when ≥2 working agents exist — so the cross-human resolution only fires in the narrow case of exactly-one-working-agent-system-wide. On a single-human machine with exactly one active agent (a common moment between dispatches), the bug fires.

**5. Watchdog env inheritance.** `Services/WatchdogService.cs:153-164` builds the watchdog `ProcessStartInfo` with no env-var manipulation. WatchdogService itself does NOT call GetCurrentAgent/GetSessionContext (confirmed via grep), so the leaked env var isn't directly consumed there. But watchdog is long-lived and launches resume terminals. **Windows**: `wt.exe` host and the brief PowerShell-startup window hold inherited DYDO_AGENT before the injected `$env:DYDO_AGENT='{agentName}'` overrides. Profile scripts that invoke `dydo` see the leaked value. **Linux**: the inheritance window is shorter (export is the first statement of the resume body). **Mac**: effectively immune (Terminal.app spawns its own bash bookkeeping).

**6. `WaitCommand.cs` exit-code-2 paths — there are FOUR, not one.** Brian's brief identified line 90 (duplicate-wait refusal); Emma also found:
- Line 142: parent PID gone.
- Line 146: claude ancestor gone.
- Line 150: post-cancellation exit.
- Plus line 44 (no agent identity assigned), pre-WaitGeneral.
`WaitForTask` mirrors at 184, 187, 191. **New finding — duplicate-wait DoS under hijack:** an attacker who sets `DYDO_AGENT=X` and runs `dydo wait` once registers a Listening marker for X with the attacker's PID. All subsequent `dydo wait` calls for X (including X's legitimate terminal's re-arm) hit the duplicate-wait refusal and exit 2 as long as the attacker's PID is alive. The attacker can hold X's wait slot indefinitely from a plain shell (no Claude ancestor → no ancestor-death gate at lines 145-146 of WaitGeneral). **This is a denial-of-wait variant of the hijack class.**

**7. Two-phase StoreSessionContext race (`GuardCommand.cs:629-673`).** Phase-1 writes the legacy single-line format (no agent name); Phase-2 writes the verified two-line format. Between phases, a concurrent reader's `GetSessionContext()` returns the unverified single-line value — bypassing verification and fallback. Two concurrent dydo commands from different terminals interleave their phase-1 writes, each briefly clobbering the other's sessionId for any reader racing into the window. The verified-resolution path catches mismatches *only when phase-2 has completed* — phase-1 publishes an unverifiable state by design. Honest race + adversarial race exploit the same primitive: an attacker that spams phase-1 writes can hold the file in legacy format and steer the fallback (which, missing human filter per #4, has a wider blast radius than intended).

**8. Audit logger inherits hijack (`GuardCommand.cs:1453-1475`).** The audit `LogAuditEvent` resolves identity via `registry.GetCurrentAgent(sessionId)` (line 1465) and `registry.GetCurrentHuman()` (line 1466). Under hijack, every audit event records the hijacked agent's name as the actor — silently falsifying the audit trail. **This is significant because audit is the forensic backstop the inquisition system depends on; the hijack corrupts the evidence trail rather than leaving a trace.** Not a new identity primitive, but the highest-leverage consequence of S0/S7 that I had not specifically called out.

**9. No CWD/path-based identity escape hatches in the guard.** Every identity resolution flows through `GetCurrentAgent`. `EmitWorktreeAllowIfNeeded` is CWD-based but emits an allow-clause for the worktree path regardless of identity (not an identity surface). `HandleClaimSessionStorage` parses the agent name from the bash command for claim destination, not for caller identity. **The guard has a single identity chokepoint — fixing F1 closes every guard-level consequence in one place.**

### Confidence: high

The bug-class definition, the primary primitive (F1, narrowed to `GetSessionContext` alone via Dexter's repro), and the systemic callsite issue (F3) are well-evidenced from direct code reading + an in-process failing test. The Surface map S0–S13 covers every identity-resolving command I could find via grep + Commands/Services walk. Emma's scout closed several blind spots (audit-attribution corruption F10, duplicate-wait DoS F11, phase-1 race F12, watchdog env-leak F13) that I had not surfaced in my own recon.

Areas of lower confidence:
- **S13 / F13 (watchdog auto-resume identity)** is sketched from architecture.md + #0143/#0153/#0181 context + Emma's WatchdogService grep rather than full re-read of WatchdogService.cs in this pass. The hijack vector via DYDO_AGENT export in resume bodies is real and Emma confirmed no production code consumes the leaked env in WatchdogService itself; the upstream conditions (when can the watchdog pick the wrong agent?) need a separate slice.
- **R2 (wait exit 2)** is theoretically tight but I did not run a live repro — confirmed by code reading only. Emma found additional ToolError paths in WaitCommand (4 paths in WaitGeneral alone, not 1) and identified the duplicate-wait DoS variant F11.
- **Open questions Q1–Q4** are flagged where I'd want resolution before committing to a final fix shape; none change the bug-class conclusion. Q1 is **unresolvable** post-hoc because audit attribution is also hijacked (F10).
- **DYDO_HUMAN parallel** (S12): Emma confirmed same env-var-as-identity pattern; severity is bounded by current single-human usage but the failure pattern is identical and should be considered for a parallel fix if multi-human support lands.

The judge has the full case. Per the brief, this inquisition explicitly does NOT propose a fix shape — that's the next slice's job. Two fix vectors are noted in the surfaces but not endorsed:
- **(a)** Add caller-PID verification in `GetSessionContext` (and symmetrically, defense-in-depth, in `GetCurrentAgent`). Cheapest verification: compare DYDO_AGENT-named agent's `.session.ClaimedPid` against the calling process or its claude ancestor (walk via `ProcessUtils.FindClaudeAncestor()`). Matches the April-09 verified-fallback philosophy on the file side.
- **(b)** Drop the DYDO_AGENT fast path in `GetSessionContext` entirely; rely on the verified `.session-context` two-line file path. Cost: one extra file read per command, already deemed negligible per April-09 risk analysis. Trade-off: loses the optimization, gains certainty.

Either fix should be paired with:
- Defense per F8 (refuse `dydo agent claim X` when `DYDO_AGENT` is set to a different agent, with an actionable error pointing at the unset command for the operator's shell).
- Test rewrite per F4 (rewrite or add contrast cases to the two encoded-bug tests).
- Operator escape hatch per F7 (e.g., `dydo inbox clear --force --file <path>` for phantom-inbox deadlocks) — even with the hijack closed, a one-off scope bug elsewhere could put a file in an unreachable inbox.
- Wait-marker ownership check per F11 (refuse to register a wait marker for an agent the caller doesn't own).
- Audit-event ownership pinning per F10 (so a future hijack-class bug doesn't corrupt the trail).

This list is enumeration for the next slice; the inquisition does not endorse any particular shape.

---

## 2026-05-19 — Zelda (live-incident addendum)

### Why this section exists

Brian's 2026-05-19 survey above is a code-level enumeration of identity-resolution surfaces (S0–S13, F1–F13). This addendum is a **first-person live incident** — the bug class manifested inside the inquisitor's own opening session, before any investigation had begun. The user explicitly directed that the session itself be filed as evidence ("This itself might be evidence"). Nothing here re-derives Brian's surfaces; the goal is corroboration + a UX-layer surface Brian's grep wouldn't catch.

### Scope

- **Entry point:** Live incident. User opened a session with the prompt `Emma --inbox`. The process running that prompt was not Emma.
- **Files investigated:** none — behavioural evidence only. Affected surfaces from Brian's map: S0 (identity-resolution asymmetry) and a candidate new surface S14 (see below).
- **Docs cross-checked:** `dydo/index.md` lines 13–22 (the "check your prompt for an agent name" onboarding step); `dydo/agents/Dexter/workflow.md` (step 1 claim); `dydo/agents/Zelda/modes/inquisitor.md` lines 60–74 (worktree marker check).
- **Scouts dispatched:** none. Sub-dispatch would have mutated the state under investigation.

### Findings (numbered to continue Brian's F-series)

#### F14. Dispatch-prompt name and process-claim name diverged in the wild

- **Category:** bug (dispatch routing) + onboarding-doc footgun
- **Severity:** high — this is the eponymous hijack signature, observed unprompted
- **Type:** obvious (first-person transcript)
- **Evidence:** The user's opening message was the literal string `Emma --inbox`. `dydo whoami` reported the process identity as **Dexter** at that moment. Verbatim:

  ```
  Agent identity for this process: Dexter
    Assigned human: balazs
    Role: (none set)
    Status: working
    Workspace: ...\dydo\agents\Dexter
  ```

  At the same moment Emma was NOT in the "Free agents" list (`whoami` taken seconds later) — Emma was claimed by some other session. So the dispatch carrier (the prompt text) was addressed to a real agent that exists elsewhere, but the actual process claim was Dexter.

  An obedient onboarding pass following `dydo/index.md` lines 13–22 ("Check your prompt for an agent name … Open `agents/<your-name>/workflow.md`") would have read **Emma's** workflow file while claimed as **Dexter** and then either (a) called `dydo agent claim Emma` from a Dexter-claimed process — testing exactly the hijack primitive Brian's S0/F1 documents — or (b) processed `dydo inbox show` under Dexter, consuming whatever Emma's inbox should have routed.

  The guard *did* block the Emma-workflow read in this session, with the message `Agent Zelda has no role set` — but the diagnostic was misleading: it referred to Zelda (claimed later) and to a missing role, not to the name mismatch. The guard does not detect "prompt names X, process claimed as Y" as a class.

- **Relation to Brian's surfaces:** This finding lives one layer above S0–S13. Brian's map covers what happens once a command runs; this covers how the wrong-name prompt arrives at the process in the first place. The fix space includes: a startup-time check that the prompt-named agent matches `DYDO_AGENT`/`whoami`; or a refusal in the onboarding doc itself ("if your prompt says X but `dydo whoami` says Y, stop and report"). Currently neither exists.
- **Judge ruling:** [pending]

#### F15. Claim silently disappeared from a working process with no `dydo agent release` issued — live corroboration of S0's failure mode

- **Category:** bug (claim lifecycle)
- **Severity:** high
- **Type:** obvious (two `whoami` calls in the same process bracketing the loss)
- **Evidence:** Same uninterrupted process, ~30 seconds apart, no `dydo agent release` call between them.

  Call 1 — `dydo whoami`: returns `Dexter`, `status: working`.

  Call 2 — `dydo whoami` (no release issued, no dydo state mutation in between): returns `No agent identity assigned to this process` and lists Dexter back among **Free agents**.

  Adjacent observation: a `dydo agent status` between the two `whoami` calls *already* reported `No agent identity assigned to this process` — i.e. `whoami` and `agent status` disagreed about the same process at the same instant, then both converged to "no identity". The two commands take different code paths to answer the same question (`whoami` reads the in-process claim, `agent status` reads registry state), and they were out of sync.

- **Relation to Brian's surfaces:** This is S0's asymmetry visible from the *outside*. Brian shows that `GetSessionContext` and `GetCurrentAgent` can disagree under env-var manipulation. This finding shows that the two user-facing commands wrapping those primitives can also disagree, and that a process's claim can vanish under it during routine work — meaning Brian's F1 fix (PID-binding in `GetSessionContext`) needs a companion question: *what cleared the claim?* Per `architecture.md` lines 79, 134–141 the Claim audit event carries `recovery_kind` and `resume_predecessor_session` fields — a follow-up scout should join those against the audit JSONs for this session to determine whether the watchdog/auto-resume path reclaimed Dexter under a different SessionId.
- **Why it matters operationally:** An agent that loses its claim mid-task cannot `dydo msg` (no identity), so it cannot report the failure back to whoever dispatched it. The user's first-suggested recovery path in this session — "report back to the agent that dispatched you, have them re-dispatch, then release" — was **not executable from inside the affected process** because (a) `dydo msg` requires an identity and (b) the first `whoami` had no role/task/dispatcher fields recorded (see F17).
- **Judge ruling:** [pending]

#### F16. `dydo agent claim auto` livelocks on a contended head-of-list slot under concurrent claim pressure

- **Category:** bug (claim allocation race — adjacent to but distinct from Brian's bug class)
- **Severity:** medium
- **Type:** obvious (reproduced in 4 consecutive shell calls, verbatim)
- **Evidence:** Four consecutive `dydo agent claim auto` invocations from this process, no other commands interleaved:

  | # | Output |
  |---|--------|
  | 1 | `There are dispatched agents waiting to be claimed. … 'auto' is probably not meant for you. If you intentionally want auto-assignment, run the command again.` |
  | 2 | `Agent Adele is already claimed by another session. Claimable agents for human 'balazs': Adele, Brian, Charlie, …` |
  | 3 | `There are dispatched agents waiting to be claimed. …` (same wording as #1) |
  | 4 | `Agent Adele is already claimed by another session. …` (same wording as #2) |

  Two issues:
  1. **Stale head-of-list retarget.** Auto-claim tried Adele on call #2 *and again* on call #4. The free-agent list at the previous `whoami` listed Adele as free; between #1 and #2 another process won Adele. By #4 the allocator was still pointing at Adele as the first candidate — no advancement past a CAS-loss to the next free name.
  2. **"Dispatched agents waiting" gate is too coarse.** The gate fires whenever *any* dispatch is outstanding system-wide, not whenever a dispatch is outstanding *for the calling human/process*. In this session the gate produced a misleading "auto is probably not meant for you" when the operator unambiguously wanted auto.

  Workaround taken: `dydo agent claim Zelda` (specific name, far down the alphabet) — succeeded immediately on first try.

- **Relation to Brian's surfaces:** Not directly an identity-resolution hijack — auto-claim allocates a *new* claim rather than resolving an existing one. But it's the same family of split-brain: the allocator's view of the free list lags reality, and the operator can be locked out of claiming any of 25 free agents because the allocator keeps choosing the one that just got taken. Worth treating as a sibling case in the same prosecution because the symptom (an operator who follows instructions still can't make forward progress) is the same.
- **Judge ruling:** [pending]

#### F17. `status: working` written with no role, no task, no dispatcher pointer — the post-hoc recovery path is impossible

- **Category:** bug (dispatch metadata not bound to claim) — possible new identity surface
- **Severity:** medium
- **Type:** obvious
- **Evidence:** First `whoami` (Call 1 above) showed:
  ```
    Role: (none set)
    Status: working
  ```
  `status: working` is normally a side effect of dispatch with a brief. But role was unset and task was unrecorded. So either (a) the dispatch routed `status: working` to the wrong agent (Dexter instead of Emma), or (b) Dexter was already in `status: working` from a prior orphaned session that never released and the registry didn't reconcile.

  Either branch is a bug-class symptom. There is no field that records "who dispatched me" — so when (as happened here) an agent ends up in working state without context, the operator's natural recovery ("ping the dispatcher and have them re-dispatch cleanly") is structurally impossible.

- **Suggested follow-up:** A `dispatch_origin` field on the agent claim state, populated at dispatch time, surfaced by `dydo whoami` and `dydo agent status`, so the hijacked or context-lost agent can at least notify its originator. Pair with F14: if the dispatcher-recorded name and the actual claim diverge, refuse the dispatch or warn loudly.
- **Judge ruling:** [pending]

#### F19. `dydo dispatch` "confirm intent" rejection (exit 2) appears to mutate state anyway — same split-brain shape

- **Category:** bug (dispatch lifecycle / error-path state leak)
- **Severity:** high — discovered while writing this report; same bug-class shape as F15
- **Type:** obvious (first-person, two consecutive shell calls)
- **Evidence:** Attempting to hand off to a judge per the inquisitor workflow:

  Call 1 — `dydo dispatch --no-wait --auto-close --role judge --task identity-hijack-bug-class-ruling --brief "…"`:
  ```
  Exit code 2
  Oversight roles should use --wait so dispatched agents' replies route back to you.
  If you really mean --no-wait (fire-and-forget), run again and it will pass.
  ```
  The exit code, the wording ("run again and it will pass"), and the convention elsewhere in dydo all signal "no action taken; we want you to confirm intent." A user reading this is led to believe nothing happened.

  Call 2 — same command, byte-for-byte, re-run to confirm intent:
  ```
  Exit code 2
  Dexter is already working on task 'identity-hijack-bug-class-ruling'.
  If you need to re-dispatch, have them release first.
  ```
  So Dexter has the task. Either:
  - (a) Call 1 *did* mutate state (assigned the task to Dexter, possibly created an inbox item) before printing the "use --wait" prompt and exiting 2 — i.e. the "confirm intent" gate runs *after* state mutation, not before; or
  - (b) Some other concurrent process independently dispatched the same task name to Dexter in the gap between Call 1 and Call 2.

  Either branch is bug-class behaviour: in (a), an error-coded command performs the side effect it nominally refused; in (b), the task-name uniqueness was decided in a race the operator can't observe.

  Notable side detail: Dexter is also the agent whose claim silently disappeared from this same process earlier (F15). It would not be surprising if Dexter's "still working" state from before the silent claim loss is the same state now blocking the new dispatch — i.e. F15 left orphaned `status: working` metadata, and F19 is the downstream consequence (the registry refuses to re-target Dexter because Dexter's prior state never got reconciled).

- **Relation to Brian's surfaces:** Possibly extends S0/F2 — `GetCurrentAgent` and dispatch-side validation may be using inconsistent views of "is agent X busy?". Also a candidate **S15** in Brian's surface map: error-path state-mutation leaks, where a command that exits non-zero has already performed observable side effects on the registry/inbox.
- **Why it matters operationally:** Inquisitor workflow could not complete cleanly. The judge dispatch is the workflow's final step ("Hand Off to the Judge"). If dispatch's exit-2 path is silently mutating state, every workflow that uses a confirm-intent dispatch is exposed.
- **Judge ruling:** [pending]

#### F18. Candidate new surface S14 — inquisitor worktree-marker check disagrees with cwd reality on manual claim

- **Category:** workflow doc / guard split-brain (same shape as the bug class, smaller blast radius)
- **Severity:** low (operationally) / high (signal)
- **Type:** obvious
- **Evidence:** `dydo/agents/Zelda/modes/inquisitor.md` lines 60–74 instruct the inquisitor to verify worktree presence via:
  ```bash
  ls dydo/agents/<self>/.worktree 2>/dev/null && echo "OK" || echo "NO_WORKTREE"
  ```
  In this session the process cwd was unambiguously a worktree:
  ```
  …\dydo\_system\.local\worktrees\identity-hijack-bug-class-inquisition\
  ```
  …but the per-agent marker `dydo/agents/Zelda/.worktree` did not exist, because Zelda was claimed manually (`dydo agent claim Zelda`), not via `dydo dispatch --worktree`. The check returned `NO_WORKTREE`. By the letter of the workflow ("Do not proceed without a worktree. … Read your inbox to recover the original brief …") I would have looped: the inbox was empty (no brief to recover), and re-dispatching would just create another manual-claim with the same missing marker.

- **Why it matters / relation to Brian's surfaces:** Same split-brain pattern as S0. Two subsystems (the marker file vs. the process cwd) answer the same question ("am I in a worktree?") inconsistently, and the workflow doc treats the marker as authoritative. A strict-following agent stalls. **Candidate addition to Brian's surface map as S14** — "context-presence checks that key off per-agent markers instead of process state".
- **Judge ruling:** [pending]

### Hypotheses Not Reproduced

None. Every finding above is a verbatim transcript from a single live session. The investigation is exclusively "this happened, here it is" — root-cause attribution is deferred to follow-up scouts (and is largely covered already by Brian's S0–S13 analysis, except F16 and F18 which are arguably new surfaces).

### Suggested Follow-Up Scouts (additive to Brian's enumeration)

1. **reviewer scout — claim disappearance audit.** What code paths can clear a process's in-memory claim *without* an explicit `dydo agent release`? Suspects: stale-session sweeper, guard read-block side effects, claim-on-claim collision, watchdog auto-resume reclaim. Cross-reference Claim audit `recovery_kind`/`resume_predecessor_session` for this session.
2. **reviewer scout — `claim auto` allocation logic.** Read the allocator, confirm/disprove "no advancement past CAS-loss" (F16's primary mechanism). If confirmed, a one-line fix (continue the loop) closes the livelock.
3. **reviewer scout — dispatch-prompt vs claim binding.** Where is the prompt text ("Name --flag") generated? Is the same `Name` variable used to write the registry claim? F14 says they can diverge; trace the dispatch entry-point that produced the `Emma --inbox` text and identify where the claim drifted to Dexter.
4. **test-writer scout — concurrent auto-claim.** Property test: N processes call `claim auto` in parallel; assert all N succeed with distinct names (current behaviour: at least one livelocks). This would lock F16 down with a regression test.
5. **co-thinker — onboarding doc redesign.** `dydo/index.md` lines 13–22 instructs the agent to obey the prompt's name. That instruction is the user-facing edge of F14. Should it be amended to "check the prompt name matches `dydo whoami`; if not, stop and report"? This is a design decision, not a bug fix.

### Confidence

- **High** for everything in the "Evidence" blocks — verbatim transcripts.
- **Medium** for "this corroborates Brian's S0/F1" — the failure modes match the shape Brian described, but I have not confirmed which exact primitive (`GetSessionContext` vs. `GetCurrentAgent`) was hit in F14/F15.
- **Low** for root cause of F15 (claim disappearance) — five candidate mechanisms, no scout has narrowed it.

### What this addendum does not do

- Does **not** propose a fix shape. Brian's section is explicit that fix-shape is the next slice's job; this addendum honors that.
- Does **not** re-derive S0–S13. Those stand as-is; F14–F18 are additive.
- Does **not** dispatch sub-scouts. The evidence was first-person; dispatching from a session that was itself the evidence would have mutated the state under investigation.

### Session metadata (reproducibility)

- Inquisitor: Zelda (this addendum)
- Process workspace: `dydo/agents/Zelda`
- Worktree: `dydo/_system/.local/worktrees/identity-hijack-bug-class-inquisition`
- Branch: `worktree/identity-hijack-bug-class-inquisition`
- Initial process identity (first `whoami`): Dexter — **silently lost** before the second `whoami` ~30s later
- Final identity at write time: Zelda (manually claimed to file this addendum)
- Trigger: live incident — the user's first prompt of the session was `Emma --inbox` to a non-Emma process
