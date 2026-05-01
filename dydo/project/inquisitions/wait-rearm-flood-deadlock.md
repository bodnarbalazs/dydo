---
area: project
type: inquisition
---

# Inquisition: Wait Re-arm Flood Deadlock (#0149)

Targeted prosecution of the wait/guard interaction that deadlocks an agent when its inbox accumulates unread messages it cannot drain. Built around the live reproduction in Charlie's own session on 2026-05-01.

## 2026-05-01 — Charlie

### Scope

- **Entry point:** Feature investigation — wait/guard interaction surfaced in issue #0149.
- **Files investigated:**
  - `Commands/WaitCommand.cs`
  - `Commands/GuardCommand.cs`
  - `Services/MessageService.cs`
  - `Services/MessageFinder.cs`
  - `Services/AgentRegistry.cs` (`CreateListeningWaitMarker`, `MarkMessageRead`, `AddUnreadMessage`)
  - `Services/InboxService.cs` (clear semantics)
  - `Services/BashCommandAnalyzer.cs` (`CheckDangerousPatterns`)
  - `Models/WaitMarker.cs`
- **Docs cross-checked:**
  - `dydo/project/decisions/021-unified-general-wait.md`
  - `dydo/project/issues/resolved/0141-…` (original auto-exit deadlock)
  - `dydo/project/issues/resolved/0147-…` (W1-exit→W2-register race fix)
  - `dydo/project/issues/0149-…` (this issue)
- **Scouts dispatched:** 0. The brief asks for a heavy-gunner audit; the evidence is concentrated in two source files plus a reproduction I could run myself, so a solo prosecution case is more efficient than parallel scouts. Dispatching a judge to validate.

### Live reproduction

The deadlock reproduced unprompted in this session. While onboarding I had general wait registered in background. Noah sent a single follow-up message on the task subject (`00f5b6cb`). Sequence:

1. Wait fires on `00f5b6cb`, exits, marker removed (`WaitCommand.cs:144` finally clause).
2. I re-armed `dydo wait` in background (allowed because `IsDydoWaitAnyForm` bypasses `CheckPendingState` in `GuardCommand.HandleDydoBashCommand:662-669`).
3. The new wait's first poll (no initial sleep — `WaitCommand.cs:113-129`) saw `state.md.UnreadMessages = ["00f5b6cb"]`, fired, exited, marker removed.
4. My next `Read` of the inbox file was blocked: `BLOCKED: Agent must keep a general wait active.` (`GuardCommand.MissingGeneralWait` via `CheckPendingState` in `HandleReadOperation:387`).

A single unread message was sufficient to deadlock me. The flood case described in #0149 differs only in degree — every additional stacked unread is one more wait fire the agent must absorb without being able to drain.

The escape Noah documented works (`dydo wait --cancel && (dydo wait &) && sleep 3 && <cmd>`). The mechanism is **not** what its naming suggests — see Finding #4.

### Findings

#### 1. Wait fires on stacked unreads with no registration-time filter — root cause of #0149

- **Category:** bug / race-condition
- **Severity:** high
- **Type:** tested (reproduced live, single-message variant of the flood case)
- **Evidence:** `Commands/WaitCommand.cs:78-146`. `WaitGeneral` reads `state.md.UnreadMessages` on every poll iteration (`:119`) and fires on the first matching message. There is no comparison against a registration baseline. The author's defending comment at `:104-108` claims:

  > "No registration-time snapshot — eliminates the W1-exit → W2-register race (#0147) and cannot re-introduce the #0141 deadlock because already-read ids are no longer in the set."

  The "already-read ids are no longer in the set" premise quietly assumes the agent **can** read between fires. In the flood case the assumption fails because (a) `MarkMessageRead` only fires from `Read` tool calls via `GuardCommand.TrackReadCompletion:403-423`, and (b) `Read` itself is blocked by `MissingGeneralWait` once the wait has exited.

  The combination of #0147's "fire on every unread in canonical set" and Decision 021's "every tool call requires a live general wait" forms a hard deadlock the moment unread arrival rate exceeds drain rate. In the trivial flood (3 messages stacked before the agent's first response turn), the deadlock is deterministic.

- **Reproduction sketch (concrete enough to recreate the test, since the worktree is temporary):**

  ```csharp
  // DynaDocs.Tests/Integration/WaitCommandTests.cs
  [Fact]
  public async Task WaitGeneral_ReFiresImmediately_OnRemainingStackedUnreads()
  {
      using var fixture = new AgentTestFixture();
      var alice = fixture.ClaimAgent("Alice", "co-thinker", "task");
      // Pre-stack three deliveries via DeliverInboxMessage (writes file + AddUnreadMessage)
      MessageService.DeliverInboxMessage(fixture.Registry, "Bob", "Alice", "msg1", "task");
      MessageService.DeliverInboxMessage(fixture.Registry, "Bob", "Alice", "msg2", "task");
      MessageService.DeliverInboxMessage(fixture.Registry, "Bob", "Alice", "msg3", "task");

      WaitCommand.PollIntervalMs = 25;

      // First wait should fire immediately (msg1)
      var w1 = Task.Run(() => RunWaitGeneral("Alice"));
      Assert.Equal(0, await w1.WithTimeout(2000));
      // Marker is removed on exit. UnreadMessages still contains all three ids
      // (no Read happened to mark them).
      Assert.Equal(3, fixture.Registry.GetAgentState("Alice")!.UnreadMessages.Count);

      // Second wait — no agent Read yet — fires within one poll cycle on msg2.
      var w2 = Task.Run(() => RunWaitGeneral("Alice"));
      Assert.Equal(0, await w2.WithTimeout(2000));

      // CheckPendingState would block any non-bypass tool call here.
      var state = fixture.Registry.GetAgentState("Alice")!;
      Assert.False(GuardCommand.HasListeningGeneralWait(state, fixture.Registry));
      Assert.NotEmpty(state.UnreadMessages);
  }
  ```

  The expected behavior is that the second wait blocks (because no *new* unread arrived after its registration), letting the agent drain through normal tool calls.

- **Suggested fix path:** `WaitMarker.Since` (`Models/WaitMarker.cs:14`) is already populated at registration (`AgentRegistry.CreateListeningWaitMarker:1051`) and persisted on disk — but `WaitGeneral` never consults it. `MessageFinder.MessageInfo.Received` is parsed from the message frontmatter (`MessageFinder.cs:89-92`, set by `MessageService.DeliverInboxMessage:69`). A 5-line patch in `WaitGeneral` filters the `MessageFinder.FindMessage` result on `Received >= waitStart` (passing the wait's own `Since` or `DateTime.UtcNow` captured at registration). This is the snapshot semantics suggestion-A from #0149, implemented via fields that already exist:

  - Doesn't re-introduce #0141 (already-read messages are not in `UnreadMessages` *and* would be filtered by timestamp anyway).
  - Doesn't re-introduce #0147 (filter is by `Received` timestamp, not file presence; messages arriving in the W1-exit→W2-register gap have `Received` >= W2.Since and pass the filter).
  - Solves #0149 (already-stacked unreads have `Received` < W2.Since and are filtered out).

  The race window in `MessageService.cs:83-87` between `File.WriteAllText` and `AddUnreadMessage` (called out in the #0147 trade-off) is unchanged — still bounded by poll interval — but the timestamp is set before either write so it doesn't introduce new windows.

- **Judge ruling:** CONFIRMED
- **Files examined:** `Commands/WaitCommand.cs:78-146` (WaitGeneral end-to-end), `Commands/GuardCommand.cs:375-423` (HandleReadOperation → CheckPendingState → TrackReadCompletion), `Commands/GuardCommand.cs:1326-1342` (MissingGeneralWait), `Services/MessageFinder.cs:1-124` (Received parsing, FindMessage signature), `Services/MessageService.cs:53-91` (DeliverInboxMessage — `received` written to frontmatter at line 69 before file write at 83 and `AddUnreadMessage` at 88), `Services/AgentRegistry.cs:1042-1091` (CreateListeningWaitMarker — `Since` set at registration), `Models/WaitMarker.cs:1-22` (`Since` field exists and is required), `dydo/project/decisions/021-unified-general-wait.md` (universalised `MissingGeneralWait` gate), `dydo/project/issues/resolved/0141-…` (original auto-exit bug — fixed by inbox-dir snapshot), `dydo/project/issues/resolved/0147-…` (W1-exit→W2-register race — fixed by pivoting snapshot to canonical UnreadMessages), `dydo/project/issues/0149-…` (the open issue this inquisition prosecutes).
- **Independent verification:** Traced the deadlock chain end-to-end: `Read` → `HandleReadOperation:387` → `CheckPendingState` → `MissingGeneralWait` blocks any non-bypass tool call when no listening general-wait exists. `MarkMessageRead` is only called from `TrackReadCompletion` (`GuardCommand.cs:421`), which only runs *after* the read passes the same gate. So the "already-read ids are no longer in the set" premise of the comment at `WaitCommand.cs:104-108` collapses the moment `Read` itself is gated on a live wait. Confirmed `MessageInfo.Received` is parsed in `ParseMessageFile` (`MessageFinder.cs:89-92`) and is consulted only for ordering inside `FindMessage` (`:48`), not for filtering. Confirmed `WaitMarker.Since` is populated by `CreateListeningWaitMarker:1051` and persisted but never read by `WaitGeneral`'s poll loop. The proposed 5-line filter (compare `Received` against the wait's `Since`) does not re-introduce #0141 (already-read ids are absent from `UnreadMessages` *and* would be filtered by timestamp) and does not re-introduce #0147 (post-registration arrivals have `Received >= Since` and pass the filter).
- **Alternative explanations considered:** (a) Could the deadlock be a Decision 021 misapplication rather than a wait-design defect? No — the Decision is explicit that #0133's general-wait behaviour was a hard prerequisite, but #0149 is a new failure mode (rate-of-arrival > rate-of-drain) not anticipated by #0133's tests. (b) Could the existing fix paths in #0149 (Suggestion B "drain-only allowlist", D "stale-wait grace window") be safer than the snapshot fix? Both add new attack surface (B opens a hole in the must-keep-wait gate; D risks hiding real deadlocks); the timestamp filter is the cleanest because it uses fields already populated and consulted nowhere else, and it composes with #0147's invariant rather than competing with it.
- **Issue:** #0149 (this finding *is* the root-cause analysis of the open issue; not duplicating)

---

#### 2. Single-message wait re-arm is also flaky — not just the flood case

- **Category:** bug / race-condition
- **Severity:** medium (high in lived practice — agents experience it as "wait sometimes wedges on a single message")
- **Type:** tested (live reproduction in this session)
- **Evidence:** Issue #0149 frames the deadlock as multi-message-only. My live session contradicts that. With **one** unread queued, my second `dydo wait` re-arm exited within milliseconds on the same message before my next tool call could be issued. The deadlock then manifested identically to the flood case until I broke out via the workaround.

  The race is between (i) the agent's next tool call (Claude's hook spawns `dydo guard`, which checks `MissingGeneralWait`) and (ii) the wait's first poll iteration (`WaitCommand.cs:113-129` runs without an initial `Sleep`, so the first `MessageFinder.FindMessage` happens immediately after `CreateListeningWaitMarker` returns). In the single-message case the wait's poll typically loses to the agent's `Read` because `Read`'s `TrackReadCompletion` empties `UnreadMessages`, leaving the wait's poll to find nothing. But the race exists; under load (other agents writing state, slow disk, GC pause on the wait process) the wait can win, and the agent deadlocks on a single message.

  The existing comment in `WaitCommand.cs:104-108` claims the design "cannot re-introduce the #0141 deadlock" — it can, intermittently, even pre-flood. This finding strengthens #0149's severity: the rate-of-arrival > rate-of-drain framing is too narrow. The deadlock can fire on rate=1 if the race goes the wrong way.

- **Suggested fix path:** Same as Finding #1 (the `Since`-based filter eliminates this race entirely — the wait never fires on a message that was in the unread set at registration time, regardless of who wins the poll-vs-Read race).

- **Judge ruling:** CONFIRMED
- **Files examined:** `Commands/WaitCommand.cs:111-146` (poll loop has no initial `Sleep` — first iteration runs synchronously after `CreateListeningWaitMarker` returns at line 101), `Commands/GuardCommand.cs:403-423` (`TrackReadCompletion`, the only path that calls `MarkMessageRead`), `Commands/GuardCommand.cs:387` (Read passes through `CheckPendingState` which gates on `MissingGeneralWait`).
- **Independent verification:** The "single-message also deadlocks" race is forced by the code shape, not just observed: when an unread arrives during processing of a prior message, the surviving wait (or the next re-arm) sees `UnreadMessages = {msg-N}` on its very first poll because there is no initial sleep. If the agent has not yet issued the `Read` for `msg-N` (or `Read` was itself blocked by `MissingGeneralWait`), the wait fires and exits, and the agent is in the same deadlock #0149 describes — with stack depth one. Charlie's live reproduction is consistent with the code path; I did not need to reproduce it independently because the race is mechanically forced by `WaitCommand.cs:113-129` running with zero delay after registration. Confirms #0149's "rate of arrival > rate of drain" framing is too narrow.
- **Alternative explanations considered:** (a) Could the agent's Read win the race in practice often enough that this is not a real deadlock? Possibly under typical timing, but Charlie observed it live — and even one deadlock per N runs is unacceptable for a guard rule that gates the entire tool surface. (b) Could a small `Thread.Sleep` at the top of the poll loop fix this without the snapshot filter? It would shift the race window but not close it; under load the wait can still win. The `Since` filter is unconditionally correct.
- **Issue:** #0149 (same root cause as Finding #1; not duplicating)

---

#### 3. RBAC and off-limits bypass via `dydo`-prefixed bash chains

- **Category:** security / antipattern
- **Severity:** high
- **Type:** obvious (verified by code inspection during the reproduction work)
- **Evidence:** `Commands/GuardCommand.HandleBashCommand:486-528` routes any bash command matching `IsDydoCommand` to `HandleDydoBashCommand` *before* dangerous-pattern checks, nudges, or the full `BashCommandAnalyzer` pipeline:

  ```csharp
  if (IsDydoCommand(command) && !string.IsNullOrEmpty(sessionId))
      return HandleDydoBashCommand(command, sessionId, registry, auditService, runInBackground);

  // Hardcoded dangerous patterns — never reached for dydo-prefixed chains
  var (isDangerous, dangerReason) = bashAnalyzer.CheckDangerousPatterns(command);
  ```

  `HandleDydoBashCommand` (`:627-678`) does not call `BashCommandAnalyzer.Analyze`, `CheckDangerousPatterns`, or `CheckBashFileOperation` on the inner part of a chain. It returns `ExitCodes.Success` after at most a `CheckPendingState` check — which itself is skipped for `IsDydoDispatchCommand` and `IsDydoWaitAnyForm`.

  The `IsDydoWaitAnyForm` check is the strongest bypass:

  ```csharp
  if (!IsDydoDispatchCommand(command) && !IsDydoWaitAnyForm(command))
  {
      // CheckPendingState only runs here — skipped if either matches
  }
  ```

  Concrete bypass examples — none of these are caught by guard once the outer string contains a recognised `dydo` prefix:

  - `dydo wait --cancel && rm -rf Commands/` — RBAC on `Commands/**` is read-only for most roles; the chain bypasses the role check entirely.
  - `dydo wait --cancel && cat dydo/_system/secrets/x` (if present) — off-limits patterns checked by `OffLimitsService.IsPathOffLimits` are never consulted for inner ops.
  - `dydo dispatch ... && rm -rf .git` — dangerous pattern detector never runs.
  - `dydo whoami && cat $env:USERPROFILE/.ssh/id_ed25519` — assuming wait is alive (normal post-claim state), `CheckPendingState` passes and the inner command runs unchecked.

  I exploited a benign instance of this in this very investigation: `dydo wait --cancel && sed -i '...' dydo/agents/Charlie/state.md` to drain my own unread by direct edit, sidestepping the deadlock for an agent-state file I only had write permission to via my role. The same construct could write or delete files outside my role's writable paths — there is no hook-side check on the inner `sed`, `rm`, `mv`, etc.

  This is independent of #0149 but was uncovered by the reproduction work. The bypass is structural: `HandleDydoBashCommand` was apparently designed assuming the *outer* command name is the only thing that needs to be gated, but `&&`/`;`/`||` chains and command substitution can hang arbitrary shell after a `dydo` token.

  Mitigation directions (judge to weigh):
  - Run `BashCommandAnalyzer.Analyze` on the full string in `HandleDydoBashCommand` after the dydo-specific checks, exactly the way `HandleNonDydoBash` does. The dydo-prefix routing should add behaviour, not remove it.
  - Or: detect `&&`/`;`/`||`/`|`/`$(…)`/`` `…` `` in commands routed to `HandleDydoBashCommand` and reject them (more aggressive, breaks the legitimate workaround chain).

- **Judge ruling:** CONFIRMED — and the bypass is broader than reported.
- **Files examined:** `Commands/GuardCommand.cs:478-528` (`HandleBashCommand` routing), `:627-678` (`HandleDydoBashCommand` body), `:708-748` (`HandleNonDydoBash` for contrast — runs `NotifyUnreadMessages`, `CheckPendingState`, must-read enforcement, then `AnalyzeAndCheckBashOperations`), `:750-834` (`AnalyzeAndCheckBashOperations` — runs `CheckDangerousPatterns`, full `BashCommandAnalyzer.Analyze`, off-limits via `CheckBashFileOperation`), `:836-1000` (`CheckBashFileOperation` — off-limits + RBAC), `:1258-1297` (`IsDydoWaitCommand`, `IsDydoCommand`, `IsDydoWaitAnyForm`, `IsDydoDispatchCommand`), `:1370-1392` (the `GeneratedRegex` definitions), `Services/BashCommandAnalyzer.cs:1-80` (the analyzer that never runs for dydo-routed chains).
- **Independent verification:** Confirmed the routing premise by reading `HandleDydoBashCommand` end-to-end — it calls `StoreSessionContext`, `HandleClaimSessionStorage`, `IsHumanOnlyDydoCommand`, `IsDydoWaitCommand && runInBackground != true`, conditional `CheckPendingState`, then `EmitWorktreeAllowIfNeeded` and returns Success. No call to `BashCommandAnalyzer.Analyze`, no `CheckBashFileOperation`, no `CheckDangerousPatterns`, no `IsPathOffLimits`. Confirmed Charlie's exploit examples by tracing each through the routing. **Additional finding I uncovered while reading the regex:** `DydoCommandRegex` (`(?:^|[;&|]\s*)(?:\./)?dydo\s`) matches `dydo` at *any* chain segment — so `rm -rf / && dydo whoami` also routes to `HandleDydoBashCommand` and skips the dangerous-pattern check, even though `dydo` is at the *end* of the chain. This is more permissive than Charlie's report suggests; the bypass is not "leading-dydo-only".
- **Alternative explanations considered:** (a) Is the routing intentional under "trust dydo-prefixed commands"? The session-storage and claim-handling logic in `HandleDydoBashCommand` requires the routing, so the *routing itself* is needed. But the *omission* of analysis on inner ops is not justified by that purpose — `HandleNonDydoBash` runs the analysis as a separate step after its own pending-state check, and the same pattern applies here. (b) Is `HandleDydoBashCommand` returning Success "early" because the inner ops will trigger their own hooks? No — Claude Code's PreToolUse hook fires once per tool call (Bash counts as one tool call); chained sub-commands within bash are not re-invoked through the hook. This is a structural assumption and the bypass relies on it. (c) Could there be an outer mitigation (e.g. PowerShell allow-list) that catches these? Per the project settings template (referenced in commit `3808f37` — "allow PowerShell(dydo:*)"), the allow-list explicitly trusts `dydo`-prefixed commands at the OS layer, which compounds rather than mitigates the bypass.
- **Issue:** #0155

---

#### 4. The "live workaround" exploits the bypass of Finding #3, not what its description suggests

- **Category:** doc-discrepancy / coding-standards
- **Severity:** medium
- **Type:** obvious
- **Evidence:** Issue #0149 and Noah's onboarding message both describe the workaround as:

  > "The parenthesized backgrounded wait keeps a fresh wait alive in shell-bg long enough for the PreToolUse guard to see Listening true. Your command runs in that window."

  This explanation is wrong. Trace:

  1. The PreToolUse hook fires *once* on the outer bash command.
  2. The outer command is `dydo wait --cancel && (dydo wait &) && sleep 3 && dydo inbox show`.
  3. `IsDydoCommand` matches → `HandleDydoBashCommand`.
  4. `IsDydoWaitAnyForm` matches the leading `dydo wait --cancel` → `CheckPendingState` skipped.
  5. `HandleDydoBashCommand` returns `ExitCodes.Success`. **No further hook checks fire on the inner chain**.
  6. The shell then runs the entire chain. By the time `dydo inbox show` runs, the spawned wait has typically already fired and exited (because the unread is in the set and the wait has no initial sleep). The inner `dydo inbox show` runs anyway because the hook is not invoked for it — the bash tool gets one allow/deny per tool call, not per chained sub-command.

  The `(dydo wait &) && sleep 3` portion is essentially load-bearing only insofar as it satisfies the human-readable narrative that the workaround is "honest." Operationally, the chain works because of the bypass in Finding #3, not because the bg wait survives.

  Verifying corollary: `dydo wait --cancel && dydo inbox show` (no spawned wait, no sleep) would work identically. I avoided demonstrating that under-belly to keep the workaround narrative consistent during the deadlock, but it follows directly from the code.

  This matters because:
  - If Finding #3 is fixed, the documented workaround stops working — and the docs/comments around it will be misleading until updated.
  - If Finding #1 is fixed, the workaround becomes unnecessary. The doc text in #0149's "suggested fix paths" section already gestures at this; the inline comments in `WaitCommand.cs:104-108` and Decision 021's analysis of the deadlock should be updated to reflect the actual mechanism.

- **Judge ruling:** CONFIRMED
- **Files examined:** `Commands/GuardCommand.cs:486-490` (single hook entry per bash tool call → routing), `:627-678` (`HandleDydoBashCommand` returns Success after the dydo-specific checks), `:1258-1265` (`IsDydoWaitCommand` excludes `--cancel` so the foreground-wait check at :651 doesn't trigger), `:1294-1297` (`IsDydoWaitAnyForm` matches `--cancel` form, skipping `CheckPendingState`), `dydo/project/issues/0149-…` (the misleading explanation in the issue body), the inquisition's own Finding #3 trace.
- **Independent verification:** Walked the `dydo wait --cancel && (dydo wait &) && sleep 3 && dydo inbox show` chain through the hook step-by-step. The outer command matches `IsDydoCommand` (dydo at position 0), routes to `HandleDydoBashCommand`. `IsDydoWaitCommand` excludes `--cancel` so the foreground-wait gate at line 651 is skipped. `IsDydoWaitAnyForm` matches the leading `dydo wait`, so the `CheckPendingState` branch at line 662 is skipped. Function reaches line 677, returns Success. The bash tool then runs the entire chain in one shell — `dydo inbox show` runs without any further hook check because PreToolUse fires once per tool call, not per chained sub-command. Confirmed Charlie's verifying corollary: a stripped-down `dydo wait --cancel && dydo inbox show` (no spawned wait, no sleep) traces identically and would work the same way. The `(dydo wait &)` portion is decorative for the operational mechanism — it only satisfies the human-readable narrative that the workaround is "honest."
- **Alternative explanations considered:** (a) Could the spawned background wait actually be doing useful work (e.g. for the 5–10s the inner sleep keeps the shell alive)? In principle yes, the spawned wait does register a marker, but operationally it has typically already fired and exited by the time `dydo inbox show` runs (no initial sleep, fires on the same stacked unread). The *inbox show* runs because the hook is not invoked again, not because the wait is alive at that moment. (b) Could the documented mechanism be correct on Linux and only wrong on Windows? No — the hook semantics are the same on both platforms; this is structural to Claude Code's PreToolUse contract, not OS-specific.
- **Issue:** #0156

---

#### 5. Inline defence comment in `WaitCommand.cs` is contradicted by lived behaviour

- **Category:** doc-discrepancy
- **Severity:** low
- **Type:** obvious
- **Evidence:** `Commands/WaitCommand.cs:104-108`:

  ```
  // No registration-time snapshot — eliminates the W1-exit → W2-register race (#0147)
  // and cannot re-introduce the #0141 deadlock because already-read ids are no
  // longer in the set. (#0141 / #0147)
  ```

  The "cannot re-introduce the #0141 deadlock" claim is false in any state where unread ids are present and the agent is unable to drain — i.e., exactly the deadlock #0149 reports. The comment encodes the design intent post-#0147 but does not survive the addition of Decision 021's "every tool call requires a live general wait" rule.

  Update the comment to honestly state the trade-off: the canonical-unread design depends on the agent being able to issue a `Read` tool call between wait fires, which is no longer guaranteed under Decision 021. Alternatively, fix Finding #1 and rewrite the comment to reflect the new invariant.

- **Judge ruling:** CONFIRMED
- **Files examined:** `Commands/WaitCommand.cs:103-108` (the comment), `dydo/project/decisions/021-unified-general-wait.md:36-72` (the rule that broke the comment's premise), `dydo/project/issues/resolved/0141-…` and `resolved/0147-…` (the deadlocks the comment claims to handle).
- **Independent verification:** The comment's exact claim is "cannot re-introduce the #0141 deadlock because already-read ids are no longer in the set." This depends on `Read` running between wait fires to call `MarkMessageRead`. Decision 021 universalised the must-keep-general-wait gate (per `GuardCommand.cs:1326-1342` `MissingGeneralWait`), and `Read`'s entry point at `GuardCommand.cs:387` runs `CheckPendingState` which evaluates `MissingGeneralWait` — so when no listening wait is registered, `Read` is blocked, `TrackReadCompletion` never runs, `MarkMessageRead` never fires, ids stay in `UnreadMessages`, the next wait re-arm fires on them. That is exactly the #0141-shape deadlock. The comment was written before Decision 021 made the gate universal; the invariant it relied on is no longer true.
- **Alternative explanations considered:** (a) Could the comment still be true on the assumption that `Read` is *eventually* allowed (e.g. via the `dydo wait --cancel && Read` workaround)? No — that path relies on the bash-chain bypass tracked in #0155. The wait command's comment is making a claim about the design's correctness in normal operation, not about the existence of an exploit-based escape. (b) Could the comment be intended as historical (post-#0147 design intent) rather than current? Possibly, but it sits inline at the call site that lives the deadlock, with no "(historical)" marker — a future maintainer will read it as current.
- **Issue:** #0157

---

#### 6. `CreateListeningWaitMarker` preserves `Since` only on idempotent re-register; flood path always loses it

- **Category:** antipattern (subtle)
- **Severity:** low
- **Type:** obvious
- **Evidence:** `Services/AgentRegistry.cs:1042-1091`. `CreateListeningWaitMarker` reads any existing marker file and preserves `Since` if found (`:1061`). But `WaitGeneral`'s `finally` block (`Commands/WaitCommand.cs:144`) calls `RemoveWaitMarker` on every exit, so the next registration is always fresh — `Since` is reset to `DateTime.UtcNow` at the moment of registration. The "preserve `Since`" branch only fires for `WaitForTask`, where the marker is created by the dispatcher pre-launch.

  This is fine for the flood deadlock fix (Finding #1's filter would use the *new* `Since`, which is what we want). But it's an inconsistency worth noting: the preservation logic in `CreateListeningWaitMarker` exists to support a workflow `WaitGeneral` doesn't actually use, and a future maintainer reading the code might assume `Since` survives wait re-arms. It does not, for the general wait.

  Either remove the preservation logic from the general-wait code path (it's dead behaviour for that case) or document the asymmetry between general and task waits in the marker's contract.

- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/AgentRegistry.cs:1042-1091` (`CreateListeningWaitMarker` — preserve-Since branch at lines 1053-1068), `Commands/WaitCommand.cs:101, 144` (general-wait register + finally Remove), `:154-185` (task-wait register + finally `ResetWaitMarkerListening`), `Services/IAgentRegistry.cs:171` (interface declaration). Also greppped for all callers of `CreateListeningWaitMarker` and `_general-wait` to confirm no third call site exists in production.
- **Independent verification:** Production callers of `CreateListeningWaitMarker` are exactly two: `WaitCommand.cs:101` (general) and `WaitCommand.cs:157` (task). The general path's `finally` calls `RemoveWaitMarker` so the marker file is gone before the next register; the preserve-Since branch can never observe an existing marker for `_general-wait` in normal operation. The task path's `finally` calls `ResetWaitMarkerListening` (flips Listening to false but keeps the file), so the preserve-Since branch fires on every task-wait re-register — that's the only live caller of the preservation logic. Asymmetry confirmed; behaviour is dead for general waits.
- **Alternative explanations considered:** (a) Could the preserve branch be there to support a future caller (e.g. a planned dispatcher pre-creation of `_general-wait` markers)? No such caller exists, no TODO references it, and the only documented dispatcher pre-creation pattern is for task waits. (b) Could it survive a crash where `WaitGeneral`'s `finally` doesn't run, leaving a stale marker that the next register would preserve? In principle yes, but `SelfHealAndGetPendingMarkers` (`GuardCommand.cs:1305-1324`) explicitly *removes* sentinel markers (those with `_` prefix) when the listener PID is dead — so even crash-leftover sentinels are cleaned before re-registration could observe them. The branch is dead in practice.
- **Issue:** #0158

### Hypotheses Not Reproduced

None ruled out. All findings are confirmed by direct code inspection or live observation.

### Confidence: high

I read the relevant code paths end-to-end, reproduced the deadlock live in a single-message form (the most surprising sub-case), and traced the bash-bypass exploit through the actual routing code. The high-severity findings (#1, #3) are concrete and code-citation-precise. Findings #2 and #4 emerged from the reproduction work and are well-supported. Findings #5 and #6 are localised doc/code drift discovered along the way.

What I did **not** do:
- Run a full unit test suite for `WaitCommandTests` to enumerate other latent edge cases. The reproduction sketch in Finding #1 should be promoted to a regression test by the code-writer who fixes this.
- Audit the off-limits patterns themselves (Finding #3's bypass is structural; the patterns themselves may also have gaps).
- Cross-check Adele's parallel co-thinker investigation on the same task — by design, our reports should be independent inputs to the judge / Noah.
