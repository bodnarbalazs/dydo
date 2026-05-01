---
area: project
type: inquisition
---

# Auto-Resume Inquisition

Investigation of the watchdog auto-resume mechanism (Decision 022) covering two reported defects: #0150 (auto-resume sometimes fails to fire after a crash) and the companion #0144 (when resume does fire, it always opens a new window instead of reusing the original window's tab group). Both share `Services/WatchdogService.cs` and the per-OS `LaunchResume` paths.

## 2026-05-01 — Brian

### Scope

- **Entry point:** Heavy-gunner inquisition into auto-resume defects #0150 and #0144 as a single investigation.
- **Files investigated:**
  - `Services/WatchdogService.cs` — poll loop, anchor lifecycle, `PollAndResumeForAgent`, `ResolveResumeWorkingDirectory`, `ParseStatusAndResumeAttempts`.
  - `Services/AgentRegistry.cs` — `ResolveClaimedPid`, `RefreshClaimedPid`, `HandleExistingSession`, `IncrementResumeAttempts`, `SetupAgentWorkspace`, `ReleaseAgent`, `WriteStateFile`.
  - `Services/ProcessUtils.cs`, `Services/ProcessUtils.Ancestry.cs`, `Services/ProcessUtils.CommandLine.cs` — `IsProcessRunning`, `FindAncestorProcess`, `MatchesProcessName`, `GetParentPid*`.
  - `Services/TerminalLauncher.cs`, `Services/WindowsTerminalLauncher.cs`, `Services/LinuxTerminalLauncher.cs` — `LaunchResume*` paths and resume-arg builders.
  - `Commands/AgentLifecycleHandlers.cs`, `Services/DispatchService.cs` — `EnsureRunning` call sites.
  - `DynaDocs.Tests/Services/WatchdogServiceTests.cs` — auto-resume test surface (cap, dead/live PID gating, repeated-poll behaviour).
- **Decisions cross-checked:** 020 (worktree power-option, basis for inquisitor's worktree default), 022 (auto-resume spec).
- **Prior fixes reviewed:** #0143 (`397011f`, RefreshClaimedPid on same-session reclaim), #0138 (`473af47`, workingDirectory threaded into resume launch), #0145 (`3808f37`, PowerShell allow-list).
- **Scouts dispatched:** none. Direct code reading produced multiply-corroborated findings; sub-dispatches would have duplicated work without adding evidence.

### Findings

#### 1. Watchdog never registers anchors on Windows; orphan-cap (24h) is the only thing keeping it alive

- **Category:** bug
- **Severity:** high
- **Type:** obvious (code analysis only)
- **Evidence:**
  - `Services/WatchdogService.cs:107` — `EnsureRunning` calls `RegisterAnchor(dydoRoot, ProcessUtils.FindAncestorProcess("claude"))`.
  - `Services/ProcessUtils.Ancestry.cs:60-63` — `MatchesProcessName` is `Path.GetFileNameWithoutExtension(actualName).Equals(needle, OrdinalIgnoreCase)`. **Exact** match on the basename. Comment at `:58-59` explicitly cites #0128 to justify exact (not substring) matching.
  - `Services/WatchdogService.cs:19-22` — the watchdog's own kill-target whitelist documents the platform asymmetry: *"Linux/Mac claude binary is 'claude'; on Windows it ships as a Node script so the resolved process name is 'node'."* `ClaudeProcessNames = { "claude", "node" }`.
  - `Services/AgentRegistry.cs:187-189` — `ResolveClaimedPid` repeats the same single-token search: `FindAncestorProcess("claude") ?? GetParentPid(Environment.ProcessId)`. Same Windows blind spot.
  - `Services/WatchdogService.cs:184` — `RegisterAnchor` returns immediately on `!anchorPid.HasValue`. With a null anchor the directory stays empty, `ScanAnchors` returns 0, `hasSeenLiveAnchor` never flips to true.
  - `Services/WatchdogService.cs:309-313` — orphaned-watchdog cap: `if (!hasSeenLiveAnchor && DateTime.UtcNow - startedAt >= maxOrphanAge) { exitReason = "max_orphan_age"; break; }`. Default `MaxOrphanAge = 24h`.
  - **Net effect on Windows:** every dispatcher's `EnsureRunning` call passes a null anchor; the watchdog runs orphaned regardless of whether claude is alive; at 24h the watchdog process exits. Until it exits, auto-resume works. After it exits, any subsequent crash silently fails to resume until the next dispatch (which spawns a fresh watchdog via `EnsureRunning`).
  - The `WatchdogServiceTests` only exercise this path with `ProcessUtils.FindAncestorProcessOverride` injecting a synthetic PID (e.g. `1172: (_, _) => 99999`). Production has no such fixture.
- **Repro recipe (Windows):**
  1. Dispatch any agent with `--auto-close` to start a watchdog.
  2. Confirm `dydo/_system/.local/watchdog-anchors/` exists but is empty (no `.anchor` files).
  3. Either advance the wall-clock 24h+ or set `WatchdogService.MaxOrphanAgeOverride` low; then watch the pid file at `dydo/_system/.local/watchdog.pid` disappear and the watchdog process exit with `exitReason = "max_orphan_age"` in the structured log.
  4. Crash a working agent; observe no resume terminal launches.
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/WatchdogService.cs (lines 1-345, full file read for orphan path & Run loop), Services/ProcessUtils.Ancestry.cs (full file), Services/AgentRegistry.cs:187-189, Commands/AgentLifecycleHandlers.cs:80, Services/DispatchService.cs:213.
- **Independent verification:** Grepped every call site of `FindAncestorProcess` and `RegisterAnchor` across the repo (`*.cs`). Every production caller passes the literal `"claude"` — no caller ever passes `"node"`. The only `"node"` reference for ancestry purposes is `WatchdogService.ClaudeProcessNames`, which is a *kill-target* whitelist, not an *ancestor-search* set. Confirmed `MatchesProcessName` is exact-equal on the basename without extension (`Path.GetFileNameWithoutExtension(actualName).Equals(needle, OrdinalIgnoreCase)`) — substring match would have absorbed the `node`/`claude` mismatch but the comment on lines 58-59 explicitly forbids substring (closes #0128). Verified `RegisterAnchor` returns immediately on `!anchorPid.HasValue` (line 184) and the orphan-cap exit at :313 fires only when `!hasSeenLiveAnchor`.
- **Alternative explanations considered:** (a) Could a parent shell `powershell.exe` inadvertently match? No — `powershell` is in `ShellProcessNames`, not `ClaudeProcessNames`, and `FindAncestorProcess("claude")` searches for "claude" only. (b) Could the watchdog be functional even with an empty anchors dir? Yes — and that is exactly what the inquisitor observed: it *runs* until the 24h orphan cap, masking the bug for normal-use days. (c) Could `claude.exe` exist as a Windows process? Not for the official npm `@anthropic-ai/claude-code` distribution, which is a Node script per the in-code comment.
- **Issue:** #0151

#### 2. Resume-attempts cap is exhaustible by a single crash because the watchdog re-fires every 10s while the resumed claude is still warming up (RefreshClaimedPid only runs after the resumed agent calls `dydo agent claim`)

- **Category:** bug / race
- **Severity:** high
- **Type:** obvious (code analysis; reinforced by existing test `PollAndResumeForAgent_RepeatedPolls_StopAtCap`)
- **Evidence:**
  - `Services/WatchdogService.cs:415-484` — `PollAndResumeForAgent` increments `resume-attempts` and launches the resume terminal, but does **not** mark `.session.ClaimedPid` as alive yet. The resumed claude must run `dydo agent claim` before `RefreshClaimedPid` (#0143 fix) updates the PID.
  - `Services/AgentRegistry.cs:321-329` — `RefreshClaimedPid` runs only inside the same-sessionId branch of `HandleExistingSession`, which is entered only when `ClaimAgent` is called. Comment from #0143 explicitly notes that, before this fix, *"The next watchdog tick saw a dead PID and fired another resume."*
  - **Race window:** Claude Code on `--resume` rehydrates the prior conversation before invoking any tool. In practice this takes several tens of seconds — much longer than the 10s poll interval. During the gap, every tick: status=working ✓, `.session.ClaimedPid` still dead ✓, `attempts < cap` ✓ → another `IncrementResumeAttempts` + `LaunchResumeTerminal`.
  - `DynaDocs.Tests/Services/WatchdogServiceTests.cs:1454-1470` (`PollAndResumeForAgent_RepeatedPolls_StopAtCap`) directly demonstrates the loop: 6 polls with a stuck dead PID produce 3 launches and `ResumeAttempts = 3`. The test is intentional but it *also* models the production race when `ClaimedPid` is not yet refreshed.
  - **Symptom doubling:** during the gap, the user sees 1–3 redundant terminals (the symptom #0143 was filed for, only partially mitigated). Worse — and silently — `resume-attempts` reaches the cap, so **subsequent unrelated crashes never resume** (see also Finding 3, which removes the eventual reset).
- **Repro recipe:**
  1. Set `WatchdogService.PollIntervalOverride = 1s` and `ResumeAttemptsCapOverride = 3`.
  2. Crash a long-conversation agent (one whose `claude --resume` warmup will exceed 3s).
  3. Observe 3 resume terminals spawn within ~3s; only the first survives once the resumed claude finally calls `dydo agent claim` and `RefreshClaimedPid` runs.
  4. After recovery, kill the resumed claude again. Watchdog now sees `attempts=3 ≥ cap` → no resume.
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/WatchdogService.cs:415-484 (full `PollAndResumeForAgent`), Services/AgentRegistry.cs:191-205 (`RefreshClaimedPid`), Services/AgentRegistry.cs:313-351 (`HandleExistingSession`), DynaDocs.Tests/Services/WatchdogServiceTests.cs:1453-1470 (the cap-stop test), Issue #0143 resolution note.
- **Independent verification:** Traced the full code path post-`LaunchResumeOverride`: after `LaunchResume` runs the watchdog only logs (`WatchdogLogger.LogResume` :483) — there is no immediate placeholder write to `.session.ClaimedPid`. Confirmed `RefreshClaimedPid` is *only* called from the same-session branch of `HandleExistingSession` (no other call sites in `AgentRegistry.cs` — grep confirmed). Read the cap-stop test and confirmed it models the dead-PID race exactly: `IsProcessRunningOverride = _ => false` keeps `IsProcessRunning(pid)` returning false across all 6 polls, mirroring the production warmup gap. Issue #0143's `RefreshClaimedPid` fix only closes the post-claim path, not the pre-claim race that this finding describes.
- **Alternative explanations considered:** (a) Could the watchdog skip via `attempts >= cap` early? Yes after the 3rd tick, but that is the failure mode (cap exhausted from a single crash). (b) Could the resumed claude somehow refresh `.session` *before* its first claim? No — `RefreshClaimedPid` is unreachable without entering `ClaimAgent` → `HandleExistingSession`. (c) Could `claude --resume` warmup complete inside one 10s tick? In synthetic tests yes; in real use with a long conversation context, observably not — and this is precisely the lived-practice symptom that motivated #0143.
- **Issue:** #0152

#### 3. `resume-attempts` is not reset on same-session reclaims, so the counter accumulates across crash episodes

- **Category:** spec/code mismatch + bug
- **Severity:** high
- **Type:** obvious (code analysis)
- **Evidence:**
  - **Decision 022 §"Retry cap" (`dydo/project/decisions/022-auto-resume-crashed-agents.md:55-57`):** *"The field resets to 0 on `dydo agent claim` (i.e. when the human or the workflow reclaims fresh) and on `dydo agent release`."*
  - **Code, fresh-claim path (`Services/AgentRegistry.cs:353-387`):** `SetupAgentWorkspace` writes `s.ResumeAttempts = 0` (line 381). This branch runs only when there is no existing session OR the incoming sessionId differs (decision-018 stale-working reclaim).
  - **Code, same-session path (`Services/AgentRegistry.cs:321-329`):** when `existingSession.SessionId == sessionId` the method calls `RefreshClaimedPid` and returns. No state mutation, no `ResumeAttempts = 0`.
  - **Result:** every successful auto-resume goes through the same-session path. The counter set by `IncrementResumeAttempts` during the resume launch is never wiped. An agent that lived through, say, two prior crashes (counter=2) now has only one resume budget left for the rest of its life — until a human runs `dydo agent release` (which resets it via `:526`) or a different session id arrives (extremely rare in practice).
  - Compounded by Finding 2: a single noisy crash can saturate the counter to 3 before the agent ever recovers, silencing every future crash.
  - No test in `WatchdogServiceTests` or `AgentRegistryTests` asserts a reset on same-session reclaim — the closest test (`ClaimAgent_SameSessionIdReclaim_RefreshesClaimedPid`) only checks the PID refresh.
- **Suspect intent vs. spec:** Decision 022 reads as if "claim" means "every claim" — same-session reclaims included. The implementation interprets it as "fresh claim only". Either the spec needs to narrow, or the same-session branch needs a `ResumeAttempts = 0` write.
- **Judge ruling:** CONFIRMED
- **Files examined:** dydo/project/decisions/022-auto-resume-crashed-agents.md:55-57 + :74-75 (spec language and Consequences), Services/AgentRegistry.cs:321-329 (same-session branch), :353-388 (`SetupAgentWorkspace` fresh-claim reset at :381), :516-531 (`ReleaseAgent` reset at :526), :1586-1601 (`IncrementResumeAttempts`).
- **Independent verification:** Read decision 022's "Code changes" section (:74-75) — the spec explicitly lists ``Services/AgentRegistry.cs`` — clear `resume-attempts` in the claim and release write-state paths.`` (plural). The same-session reclaim is a claim — the spec narrative does not exempt it. Verified that `IncrementResumeAttempts` (:1593) only ever bumps the counter; nothing else in `AgentRegistry.cs` decrements or zeroes `ResumeAttempts` outside of `SetupAgentWorkspace` (:381) and `ReleaseAgent` (:526). Confirmed the same-session branch (:321-329) writes nothing — it just calls `RefreshClaimedPid` and returns.
- **Alternative explanations considered:** (a) Could decision 022's parenthetical ``(i.e. when the human or the workflow reclaims fresh)`` be read as narrowing to fresh-claim-only? Linguistically possible, but the surrounding "Code changes" enumeration says "claim and release write-state paths" without exception, and the rationale ("absorb transient crashes... exits a poisoned-state crash-resume-crash loop within ~2 polling intervals after hit") is incoherent if the counter never resets across a successful recovery — every long-lived agent would saturate within its first few crash episodes. So the implementation is the side that diverged, not the spec. (b) Could the existing test `ClaimAgent_SameSessionIdReclaim_RefreshesClaimedPid` (only checks PID refresh) be intentional cover for a "PID-only refresh" interpretation? Possible, but no decision artefact says so — and the asymmetry (`SetupAgentWorkspace` does reset, same-session reclaim does not) makes the agent's resume budget depend silently on whether sessionId rotated.
- **Issue:** #0153

#### 4. On Linux/Mac, the watchdog exits via `anchor_gone` once all dispatchers' claudes have exited, even if dispatched (leaf) agents are still alive

- **Category:** bug
- **Severity:** medium
- **Type:** obvious (code analysis)
- **Evidence:**
  - On Linux/Mac the binary name is "claude" so `FindAncestorProcess("claude")` succeeds and the dispatcher's claude is registered as an anchor.
  - **Anchor sources (production):** `RegisterAnchor` is called from `EnsureRunning` only (`WatchdogService.cs:107`). `EnsureRunning` is called from two sites: `Services/DispatchService.cs:213` (only when `--auto-close` is set on dispatch) and `Commands/AgentLifecycleHandlers.cs:80` (release). Neither fires on a dispatched agent's claim — only on its dispatcher's actions.
  - **Implication:** dispatched agents do not anchor themselves. The watchdog's lifetime is bounded by the lifetimes of agents that have invoked `dydo dispatch --auto-close` or `dydo agent release`.
  - **Common scenario:** orchestrator dispatches a leaf code-writer with `--auto-close`. Watchdog starts, anchor = orchestrator's claude. Orchestrator releases and exits before the leaf finishes. Anchor count → 0 → `WatchdogService.cs:312 anchor_gone` exit. Leaf crashes → no watchdog → no resume.
  - On Windows this finding doesn't add anything (Finding 1 already disables anchoring), but on Linux/Mac the issue is real and silent.
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/WatchdogService.cs:99-178 (`EnsureRunning` + `RegisterAnchor`), :281-326 (Run loop / anchor_gone gate at :312), :353-408 (cleanup poll, no anchoring), :415-484 (resume poll, no anchoring), Services/DispatchService.cs:212-213, Commands/AgentLifecycleHandlers.cs:80, Services/AgentRegistry.cs:230-388 (full claim path).
- **Independent verification:** Grepped `EnsureRunning` and `RegisterAnchor` across all `*.cs` files. The only production callers of `EnsureRunning` are `DispatchService.cs:213` (gated on `effectiveAutoClose`), `AgentLifecycleHandlers.cs:80` (release), and `WatchdogCommand.cs:15` (manual `dydo watchdog start`). Confirmed `ClaimAgent` → `SetupAgentWorkspace` does not call `EnsureRunning` or `RegisterAnchor` for the dispatched-or-queued path. Verified the anchor_gone exit at :312 fires the moment the live anchor count drops to 0 once `hasSeenLiveAnchor` is true, so the orchestrator-anchored watchdog dies on the first poll after the orchestrator's claude exits — even if leaf agents are mid-task.
- **Alternative explanations considered:** (a) Could the leaf inadvertently anchor itself via some other path (e.g. `dydo wait` fork, `dydo dispatch` nested)? Only if the leaf itself runs `dispatch --auto-close`, which is unusual for terminal/leaf agents in this workflow. (b) Could the dispatcher's parent shell hold an anchor? No — anchors are only PIDs explicitly written by `RegisterAnchor`, sourced exclusively from `FindAncestorProcess("claude")`. (c) Could the 24h orphan cap save the day? No — once `hasSeenLiveAnchor` flipped true, the loop takes the `anchor_gone` exit (:312), not `max_orphan_age` (:313). The cap is unreachable after first-anchor.
- **Issue:** #0154

#### 5. `LaunchResume` is missing the `useTab`/`windowName` parameters that `Launch` accepts; Windows hardcodes a fresh GUID windowName so resume always opens a new window

- **Category:** bug (issue #0144)
- **Severity:** low (polish)
- **Type:** obvious (code analysis)
- **Evidence:**
  - `Services/TerminalLauncher.cs:188` — `LaunchNewTerminal(agentName, workingDirectory, useTab, autoClose, worktreeId, windowName, ...)` accepts `useTab` and `windowName`.
  - `Services/TerminalLauncher.cs:223-228` — `LaunchResumeTerminal(agentName, sessionId, workingDirectory)` takes only three parameters. No `useTab`. No `windowName`.
  - `Services/WindowsTerminalLauncher.cs:87-106` (`LaunchResume`):
    - Line 93: `var windowName = Guid.NewGuid().ToString("N")[..8];`
    - Line 94: `var wtAction = $"--window {windowName} new-tab";`  
    `--window <name>` with a brand-new name forces wt.exe to spawn a fresh window. Compare to `Launch` (`:131-140`) where `useTab=true` produces `-w {windowName} new-tab`, attaching to the existing window with that name.
  - `Services/LinuxTerminalLauncher.cs:90-110` (`TryLaunchResume`) always uses `terminal.GetArguments(...)` — the window-mode invocation. The tab-mode arg builder `terminal.GetTabArguments` is never consulted on the resume path, even when the original launch used it.
  - **The data already exists:** `Services/AgentRegistry.cs:1628` writes `window-id: {state.WindowId ?? "null"}` into `state.md`, and `WatchdogService.cs:682-708 ParseStateForWatchdog` already extracts `windowId` (consumed by the auto-close path). The resume path uses a *different* parser, `ParseStatusAndResumeAttempts` (`:498-511`), that ignores window-id.
  - **Why fix sketches still need care:** the original `windowName` was generated at dispatch time (`WindowsTerminalLauncher.cs:138-140`) and persisted into `state.md`. The watchdog can plumb it through unchanged. But on Linux, `TryLaunchResume` would also need to remember which terminal binary the dispatcher chose (currently it tries each in order on every resume), and whether the original used the tab-mode args.
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/TerminalLauncher.cs (full file, signatures of `Launch`/`LaunchResume` at :188-253), Services/WindowsTerminalLauncher.cs (full file, especially `LaunchResume` :87-121 vs `Launch` :123-170), Services/LinuxTerminalLauncher.cs (full file, especially `TryLaunchResume` :90-110 vs `TryLaunch` :112-143), Services/AgentRegistry.cs:1610-1638 (`WriteStateFile` confirms `window-id` is written), Services/WatchdogService.cs:498-511 (`ParseStatusAndResumeAttempts` ignores window-id) vs :682-708 (`ParseStateForWatchdog` extracts `windowId`).
- **Independent verification:** Diffed `Launch` and `LaunchResume` signatures: `Launch(agentName, workingDirectory, useTab, autoClose, worktreeId, windowName, ...)` (TerminalLauncher.cs:193) vs `LaunchResume(agentName, sessionId, workingDirectory)` (TerminalLauncher.cs:228). Confirmed `WindowsTerminalLauncher.LaunchResume` always builds `--window {fresh-guid} new-tab` (:93-94), while `Launch` builds `-w {windowName} new-tab` only when `useTab=true` and a non-null `windowName` is supplied (:132-140). With a fresh GUID, wt.exe never matches an existing window, so a new window opens. Confirmed `LinuxTerminalLauncher.TryLaunchResume` always uses `terminal.GetArguments(...)` (:98) — never `GetTabArguments` — and iterates each terminal in the LinuxTerminals array order, with no memory of the dispatcher's choice. Confirmed the `state.md` line `window-id: {state.WindowId ?? "null"}` is written at :1628 of `WriteStateFile`, and `ParseStateForWatchdog` already extracts it (:697-699), but the resume path uses the simpler `ParseStatusAndResumeAttempts` (:498-511) which does not.
- **Alternative explanations considered:** (a) Could the missing thread-through be intentional (e.g. fresh-window-on-resume is a desired isolation property)? No artefact says so — issue #0144 (already filed manually) describes this exact behaviour as a polish defect. (b) Could the Linux side of this finding be subsumed by issue #0144? Yes, the underlying cause is the same — the resume API simply doesn't accept `useTab`/`windowName`/terminal-name parameters — which is why I'm not filing a new issue and pointing to #0144 instead. (c) Could `wt.exe`'s `--window <new-name>` actually attach to some existing window-by-name? It can — but the GUID is regenerated every resume, so the original window's name is unrecoverable from this path.
- **Issue:** #0144 (already open; finding confirms the existing issue's root cause without filing a duplicate)

### Hypotheses Not Reproduced

- **Process-liveness false-positive via PID reuse** — `IsProcessRunning` is `Process.GetProcessById(pid).HasExited`, which reflects real OS state. PID reuse within a single 10s poll window is theoretically possible on Linux but the failure mode would be permanent (PID reused for life), not intermittent. No code path or evidence supports it.
- **`.session` file genuinely missing** — every successful claim writes `.session` via `SetupAgentWorkspace` (`AgentRegistry.cs:373`); release deletes it. Cannot reach `working` state without `.session` present in current code.
- **`state.md` parse race** — `ParseStatusAndResumeAttempts` reads with no retry; a partial write can return null status, but the next 10s tick recovers. Adds latency, not chronic miss. The brief listed this candidate; ruled out as a primary cause.
- **Watchdog poll cadence drift** — 10s default with `WaitHandle.WaitOne(pollInterval)`; sub-second drift on a healthy host. Long polls (large agent fan-out × `wmic` calls) can extend a single tick by seconds, but not enough to "miss" a crash.

### Reproduction Recipe (Combined)

For #0150 — minimal Windows repro of the dominant root cause (Finding 1):

1. Start any dispatch with `--auto-close` to spawn the watchdog. Note its PID (`Get-Content dydo/_system/.local/watchdog.pid`).
2. Verify `dydo/_system/.local/watchdog-anchors/` is empty.
3. Patch `Services/WatchdogService.cs:66 MaxOrphanAge` (or use `MaxOrphanAgeOverride` in a debug build) to `TimeSpan.FromMinutes(1)` and re-run the dispatch.
4. Wait 60+ seconds. Observe the watchdog process exits and the pid file disappears, with structured log line `{"event":"exit","reason":"max_orphan_age"}`.
5. Crash a working agent (close its terminal). Observe no resume terminal launches.

For #0150 — Linux/Mac repro of Finding 4 (anchor_gone):

1. Dispatch a single leaf agent with `--auto-close`. Watchdog starts, anchor = your claude.
2. Release your own agent (`dydo agent release`) and exit your claude session.
3. Wait one poll interval. Watchdog detects no live anchors → exits with `anchor_gone`.
4. Crash the leaf. No resume.

For #0144:

1. Dispatch an agent with `--use-tab` (or otherwise into an existing wt window).
2. Crash that agent. Watchdog auto-resumes.
3. Resume terminal lands in a brand-new wt window (fresh GUID), not as a tab inside the original.

### Recommended Fix Sketches (for code-writer dispatches; not shipping in this inquisition)

1. **Finding 1 (Windows anchoring):** make `ResolveClaimedPid` and `RegisterAnchor`'s anchor source platform-aware. On Windows, also accept `node` as a valid claude ancestor (mirroring `ClaudeProcessNames`). Or, more cleanly, replace `FindAncestorProcess("claude")` with a `FindClaudeAncestor()` helper that knows the platform-specific names and is the single source of truth for both anchoring and ClaimedPid capture.
2. **Finding 2 (resume race):** suppress watchdog re-fires during a "warming up" window. Cheapest option: `IncrementResumeAttempts` also writes a "last-resume-launched-at" timestamp into `state.md`; `PollAndResumeForAgent` skips agents whose last launch is within, say, 60s. This is the same shape as the removed `PendingCleanup` two-poll deferral that was load-bearing for auto-close (cf. agent-deaths inquisition §1). Alternative: have `LaunchResumeTerminal` write a placeholder ClaimedPid (the spawned wt.exe pid) immediately so the next tick sees a live pid until the resumed claude refreshes it.
3. **Finding 3 (resume-attempts leak):** add `state.ResumeAttempts = 0` in `Services/AgentRegistry.cs:327` (the same-session branch), inside a `RefreshClaimedPid`-adjacent state mutation. Update `RefreshClaimedPid` or add a sibling that also touches `state.md`. Add a regression test paralleling `ClaimAgent_SameSessionIdReclaim_RefreshesClaimedPid`. Decide explicitly whether decision 022 means "any claim" or "fresh claim only" and reflect in code + spec.
4. **Finding 4 (anchor_gone with live leaves):** also register an anchor when `dydo agent claim` succeeds for a dispatched agent — i.e. plumb `EnsureRunning` (or just `RegisterAnchor`) into `SetupAgentWorkspace` for the dispatched-or-queued path. This binds watchdog lifetime to the population of working agents, not just dispatchers.
5. **Finding 5 (#0144 resume window):** thread `useTab` and `windowName` through `LaunchResume*` from `state.md`. `ParseStatusAndResumeAttempts` already reads `state.md`; extending it to return windowId is one line. Then `WindowsTerminalLauncher.LaunchResume` chooses `-w {windowName} new-tab` when windowId is non-null, falling back to the current `--window <fresh-guid> new-tab` otherwise. Linux/Mac analogous.

### Confidence: medium-high

- Findings 1, 3, 5 are direct code reads with clear citations and no plausible alternative interpretation. Confidence high.
- Finding 2 is reasoning from existing code + a passing test that already models the race; the only missing piece is empirical timing of `claude --resume` warmup vs. the 10s poll. Confidence medium-high.
- Finding 4 is structural and obvious from the call graph; no audit-log forensics performed for the inquisition window. Confidence medium-high.
- Areas not examined: the v1.4 unified general-wait re-arm (`WaitCommand`) in relation to resume; the worktree-resumed cleanup sequence (resume body has no `worktree cleanup` invocation, unlike the dispatch path — orthogonal to the brief but worth its own pass); MacTerminalLauncher's `LaunchResume` (Windows + Linux were sufficient to establish the #0144 root cause).
- No live crash repro performed — findings derive from code/spec analysis. The reproduction recipes above are concrete enough for a code-writer or test-writer to drive proof-or-disproof in a sub-dispatch.
