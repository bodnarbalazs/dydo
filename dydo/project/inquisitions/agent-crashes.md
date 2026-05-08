---
area: project
type: inquisition
---

# Inquisition: Agent Crashes Mid-Task & Auto-Resume Degradation

Investigation triggered by balazs's complaint that agent crashes have grown more frequent and the v1.4.0 auto-resume facility no longer recovers them. Investigation only — no code/test changes. Report is written incrementally; later findings extend earlier ones.

## 2026-05-06 — Brian

### Scope

- **Entry point:** Adele dispatched the inbox brief `investigate-agent-crashes` (file `dydo/agents/Brian/inbox/fa2e0f50-investigate-agent-crashes.md`). Hypotheses to test (none confirmed up front): harness instability, context-window pressure, auto-resume regression, auto-resume incorrectly killing healthy sessions, other v1.4.5+ regression.
- **Versions in scope:** v1.4.0 (2026-04-30, auto-resume introduced) … v1.4.6 (2026-05-06, just installed).
- **Defensive constraint:** Audit JSON / `.events` files can be large and reading them in full is itself one of the suspected crash triggers. All audit reads in this investigation use Bash + `head` / `tail` / `wc -l` / `awk` / `grep` to sample the first and last events only. No full-file Read of any audit file.

### Versions and timeline (orientation)

```
v1.4.0  2026-04-30  a06db96   feat(watchdog): auto-resume crashed agents (Decision 022)  + unified general-wait
v1.4.1  2026-05-01  c50c6c7   wait #0141 + auto-resume #0143 fixes
v1.4.2  2026-05-01  aeee461   tests-only (#0148 helper drain fix)
v1.4.3  2026-05-02  a4726d8   refactor(watchdog): extract helpers
v1.4.4  2026-05-02  a4726d8   (same SHA — re-tag)
v1.4.5  2026-05-02  1259d15   "."
v1.4.6  2026-05-06  834b00f   fix(tests): unblock Linux CI; no Services/Commands changes
```

Between v1.4.5 and v1.4.6 there are **no changes** under `Services/WatchdogService.cs`, `Services/AgentRegistry.cs`, or `Commands/WaitCommand.cs`. So if auto-resume regressed, the regression is at or before `1259d15` and the v1.4.6 install does not change the symptom — consistent with the user's narrative.

### Audit data inventory

`dydo/_system/audit/2026/`:

- **1259** `*.json` session metadata files (`yyyy-mm-dd-{sessionId}.json`), spanning 2026-02-26 through 2026-05-06.
- **249** `{sessionId}.events` JSONL sidecars. The append-only sidecar pattern was introduced 2026-04-11 (commit `99a9a33`, "Fix 8 audit and backend issues"); pre-2026-04-11 sessions are JSON-only because back then every cross-process `LogEvent` rewrote the full JSON. After 2026-04-11, every cross-process append goes to the sidecar (`AuditService.cs:65 AppendEventToSidecar`). The sidecar is **only deleted** when `WriteSession` runs in-process via the cache fast-path (`AuditService.cs:233-238`); cross-process flows do **not** clean up. So a sidecar's existence ≠ "session is unfinished" — most released sessions also retain a sidecar.

Of 249 sidecars, **215** end in a `Release` event and **34** end abruptly. The 34 break down (by session-final last event):

| Date range | Count | Notes |
|------------|-------|-------|
| 2026-04-14 .. 2026-04-28 | 28 | Pre-watchdog-log era — watchdog log (Apr 29+) cannot witness these |
| 2026-04-29 .. 2026-05-04 | 4 | Includes deliberate Victor/Frank auto-resume smoke-test sessions |
| 2026-05-06 (today) | 2 | One is the **currently-live** Brian inquisitor session (this report); the other is the **currently-live** Adele session — both confirmed alive (claude.exe PID 67232 / 57332 visible in `tasklist`) |

The "currently-live alive" check matters: a session in flight has a sidecar that ends on its most recent event but has not yet produced `Release`. Filtering those out: **at most 31** truly crashed sessions in the post-2026-04-11 era. Several of those overlap with deliberate smoke-test crashes that the user staged while validating auto-resume.

Frequency-over-time signal is weak from the sidecar set alone (most early sidecars predate the watchdog log) — the **watchdog log is the authoritative source for crashes after 2026-04-29**.

### Watchdog log (auto-resume ground truth)

`dydo/_system/.local/watchdog.log` (current 647 KB) + `watchdog.log.1` (rotated 2 MB):

| Event | Current log (May 5+) | Rotated (Apr 29 – May 5) |
|-------|----------------------|--------------------------|
| `start` | 2 | 41 |
| `tick` | 7,454 | 24,374 |
| `kill` | 45 | 53 |
| `resume` | 10 | 29 |
| `resume_blocked` | 10 | 1 |
| `exit` | 0 | 35 |

**Resume cadence by date (events, not crashes):**

```
Apr 30  Victor 1,2,3        (Decision-022 launch — 3 attempts in 30s; pre-warmup-gate)
        Emma   1,2,3        (auto-resume smoke)
        Adele  1,2
May 1   Adele  1,2,3        (#0141 fixes day; pre-warmup-gate)
        Frank  1,2,3        (smoke test session 8a9a62ed)
        Adele  1            (single — successful?)
        Adele  1,2,3
        Charlie 1,2
        Brian  1
        Brian  1,2,3
        Adele  1,2,3
May 2   Charlie 1           (warmup-gate landed in 9b27195 evening of May 2)
May 4   Brian   1 → blocked no_refresh_after_warmup
May 5   Brian   1 → blocked
        Charlie 1 → blocked
        Emma    1 → blocked
        Charlie 1 → blocked
        Frank   1 → blocked
        Charlie 1 → blocked
May 6   Charlie 1 → blocked   (today; matches user's "Charlie crashed at claim time")
        Brian   1 → blocked   (today; matches "Brian crashed mid-inquisition #1")
        Frank   1 → blocked
        Brian   1 → blocked   (today; "Brian crashed mid-inquisition #2")
```

The phase change is sharp: **post 2026-05-02 (commit `9b27195`, v1.4.3)** every single resume fires exactly once, then 60 s later the watchdog logs `resume_blocked: no_refresh_after_warmup` and saturates the resume cap. That's a 100% pre-warmup phase change — prior to `9b27195` resumes ran 1→2→3 in 10 s windows; afterward the warmup gate (60 s) means at most 1 attempt before the blocking failsafe fires.

### Findings

#### 1. ResumeWarmupGate=60 s is too short and forces a premature `resume_blocked` failsafe

- **Category:** auto-resume regression / antipattern
- **Severity:** **high** (this is the headline root cause of the user's "auto-resume coverage went to 0%" perception)
- **Type:** tested (cross-referenced watchdog log against audit sidecar Release timestamps)
- **Evidence:**
  - `Services/WatchdogService.cs:73` — `ResumeWarmupGate = TimeSpan.FromSeconds(60)`. Comment claims "Sized for claude --resume rehydration (tens of seconds on real conversations) plus dydo claim hook latency."
  - `Services/WatchdogService.cs:544-548` `IsBadSessionFailFast` returns true once 60 s elapsed AND `ClaimedPid == PreResumePid`. `PollAndResumeForAgent` (lines 458-464) then calls `registry.SaturateResumeAttempts(ctx.AgentName, ctx.Cap)` and logs `resume_blocked` with reason `no_refresh_after_warmup`.
  - **The gate fires falsely on real production sessions.** All four of today's `resume_blocked` events were followed by an eventual successful `Release` in the same session's audit sidecar:

| Session (today) | Resume fired | Resume blocked | Last event in sidecar |
|-----------------|-------------|---------------|----------------------|
| Charlie 8b52b181 | 13:27:00 | 13:28:06 (no_refresh) | **Release 13:36:19** (8 min later) |
| Brian f9936e33 | 16:16:32 | 16:17:34 (no_refresh) | **Release 16:48:53** (32 min later) |
| Frank 04d1191f | 16:44:53 | 16:45:56 (no_refresh) | **Release 16:55:14** (10 min later) |
| Brian 4c2838f8 | 18:03:06 | 18:04:11 (no_refresh) | **No sidecar at all** — initial JSON only (1 Read event) |

  Three out of four (75%) "blocked" sessions actually resumed successfully — the resumed claude just took longer than 60 s to rehydrate the conversation and reach its first dydo guard hook (which is what triggers `RefreshClaimedPid` via `HandleExistingSession`, `AgentRegistry.cs:341`). The watchdog declares `resume_blocked` while the resumed claude is still rehydrating in the background.
  - The fourth (`4c2838f8`, Brian 18:03) genuinely failed to make progress — JSON has only the pre-crash Read; no sidecar was ever opened, meaning the resumed claude never made any tool call that the dydo guard hook could capture. (Possible secondary failure: same root cause that crashed the original tab also crashed the resume tab — the tab opens, claude --resume rehydrates a large session, OOM, dies before any tool call.)
  - The downstream effect of `SaturateResumeAttempts` on a falsely-blocked session is partially self-healing: once the resumed claude DOES finally make its first tool call, `ClaimAgent → HandleExistingSession → ResetResumeBookkeeping` (`AgentRegistry.cs:347`, #0153 fix) zeroes the cap again. So the cap saturation is not the durable harm. The durable harm is the **false alarm in the watchdog log itself**, which is exactly what the user reads as "auto-resume is broken."

- **Why this is the regression the user perceived:** Pre-warmup-gate (Apr 30 – May 2), the watchdog fired 3 attempts in 30 s before the cap stopped it. If a slow rehydration completed within those 30 s, fine; if not, the user saw 3 terminal windows pop and the cap-3 logged, but eventually the agent recovered. Post-warmup-gate (May 2+), the watchdog fires **once**, declares the resume "blocked" 60 s later, and stops trying — even though the resumed claude is in fact alive and rehydrating. The user's "auto-resume coverage went 50% → 33% → 0%" is the felt experience of the cap dropping from 3 to effectively 1 along that timeline.
- **Proposed fix path:**
  1. Raise `ResumeWarmupGate` from 60 s to a value calibrated against observed rehydration time. The ground-truth in this audit data: post-resume-to-Release-or-first-event delay was 8 min, 32 min, 10 min, ∞ for today's four cases. A 5-minute gate would have correctly caught all three success cases without firing `no_refresh_after_warmup`. Decision 022's comment ("tens of seconds on real conversations") is the assumption that turned out wrong.
  2. Better: replace the wall-clock gate with a **liveness check**. `IsBadSessionFailFast` should be true only when the resume terminal's launched PID is also dead. If the launched terminal/claude is still running, the resumed claude is rehydrating; don't block.
  3. Even better: stop saturating the cap on a guess. `no_refresh_after_warmup` can degrade to a one-time log line ("resume not yet acknowledged") without poisoning subsequent attempts. The `attempts < 3` cap is the actual loop-protection; saturating to `cap` on a false positive collapses the safety net into a single shot.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/WatchdogService.cs:65-76, 437-481, 538-548`; `dydo/_system/.local/watchdog.log` (resume + resume_blocked pairs for 2026-05-06); `dydo/_system/audit/2026/{8b52b181,f9936e33,04d1191f}.events` (last events); `dydo/_system/audit/2026/2026-05-06-4c2838f8-*.json` (no sidecar exists).
- **Independent verification:** Reproduced the watchdog log evidence — `grep '"event":"resume"'` paired with `grep '"event":"resume_blocked"'` confirms the 60s-after pattern across multiple days. `tail -1` on the three event sidecars confirms `Release` events at 13:36:19, 16:48:53, 16:55:14 (deltas 8/32/10 min after the "blocked" log). Fourth session (`4c2838f8`) only has the JSON metadata file — no `.events` sidecar — confirming the inquisitor's "genuine failure" reading. Walked `IsBadSessionFailFast` predicate directly: `(now - lastResumeAt) >= gate && PreResumePid.HasValue && ClaimedPid == PreResumePid` — `ClaimedPid` only changes via `RefreshClaimedPid` on the resumed claude's first tool call, so the predicate is true for the entire rehydration window regardless of whether the resumed claude is alive.
- **Alternative explanations considered:** Could the comment at lines 67-71 ("tens of seconds on real conversations") reflect an empirical measurement that has merely drifted? No — the cited evidence shows multi-minute rehydration was already happening on 2026-05-04 (the day after the gate landed), so the original calibration was wrong from the first week. Could the `SaturateResumeAttempts` be intentional belt-and-braces against a confirmed-dead session? Plausible if the session were truly dead, but the predicate cannot distinguish dead from rehydrating — that distinction requires the launched-PID liveness check this issue recommends adding.
- **Issue:** #0173 (covers Findings #1 and #5 — same defect, two angles)

#### 2. Worktree-claimed agents register their watchdog anchor in the wrong directory

- **Category:** bug / cross-cutting infrastructure
- **Severity:** medium-high (contributes to the "watchdog dies prematurely" symptom in #0154)
- **Type:** obvious (proven by direct filesystem inspection)
- **Evidence:**
  - `Services/WatchdogService.cs:101-105` `EnsureRunning()` resolves `mainDydoRoot = PathUtils.FindMainDydoRoot()` (returns the **main** project's `dydo/`).
  - `Services/AgentRegistry.cs:415-421` — the same RegisterAnchor call on agent-claim resolves via `_configService.GetDydoRoot(_basePath)` (returns the **worktree's** `dydo/` when `_basePath` points inside one).
  - `Utils/PathUtils.Discovery.cs:46-52` documents the distinction: `FindMainDydoRoot` is "Used by the watchdog so its PID file and CWD never land inside a worktree."
  - **Live filesystem proof, captured during this investigation:**
    - Worktree (this inquisition's): `dydo/_system/.local/watchdog-anchors/67232.anchor` (Brian's claude PID 67232 — written by my own claim).
    - Main: `/c/.../DynaDocs/dydo/_system/.local/watchdog-anchors/57332.anchor` (Adele's claude PID 57332 — Adele claimed in main).
    - Two separate anchor directories. The main watchdog reads only main's. My anchor (67232) is invisible to the running watchdog.
  - The watchdog's exit conditions in `Run()` (`WatchdogService.cs:325-326`):
    - `if (hasSeenLiveAnchor && liveAnchorCount == 0) { exitReason = "anchor_gone"; break; }`
    - When all main-resident anchors die (e.g. orchestrator releases) but worktree-only leaf agents are still working, the watchdog exits via `anchor_gone` — even though leaf agents need it. This matches the open #0154 symptom verbatim.
  - The anchor-dir mismatch is **not a self-healing bug**: junctioning `dydo/_system/.local/` is explicitly NOT in the worktree junction list (`dydo/understand/architecture.md:87-88` says only `agents/`, `_system/roles/`, `project/issues/`, `project/inquisitions/` are junctioned).
- **Proposed fix path:** `AgentRegistry.cs:417` should use `PathUtils.FindMainDydoRoot()` (or equivalent main-resolving helper) for the RegisterAnchor write, mirroring `EnsureRunning`. The two callsites should ideally route through a single helper so the rule is enforced by construction. Cross-references existing #0154 (Linux/Mac variant of the same family).
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/AgentRegistry.cs:37-66, 410-421`; `Services/WatchdogService.cs:88-106, 195-228, 305-326`; `Utils/PathUtils.Discovery.cs:42-52`; `Services/ConfigService.cs:79-88`; live filesystem (`watchdog-anchors/` in main vs worktree).
- **Independent verification:** Walked the resolution chain directly: `_basePath` (`AgentRegistry.cs:39`) defaults to `PathUtils.FindProjectRoot() ?? Environment.CurrentDirectory`; `FindProjectRoot` calls `ConfigService.GetProjectRoot` which delegates to `FindConfigFile`. Inside a worktree the search hits the worktree's own `dydo.json` (worktrees ARE working trees of the same repo and carry the file). `GetDydoRoot(worktreeBase)` then returns the worktree's `dydo/`. Filesystem confirmation captured live during this judge session: main has only `57332.anchor` (Adele); worktree `investigate-agent-crashes/dydo/_system/.local/watchdog-anchors/` contains `67232.anchor` (Brian) and `69896.anchor` (this judge — confirmed live `claude` process via `Get-Process -Id 69896`). Two separate anchor directories; `dydo/_system/.local/` is not a junction (per architecture.md §Worktree Dispatch — only four specific paths are junctioned).
- **Alternative explanations considered:** Was the worktree-anchor dir intentionally local — perhaps a per-worktree watchdog was contemplated? No — `WatchdogService.EnsureRunning()` (line 101-105) explicitly resolves `mainDydoRoot = PathUtils.FindMainDydoRoot()` and the comment at line 95-99 says "Resolves to the MAIN project root — dispatches from inside a worktree do not spawn a second, worktree-scoped watchdog." So the design is one-watchdog-per-main, and the `AgentRegistry.cs:417` callsite simply missed that contract. Was the claim-time anchor path added intentionally only for non-worktree dispatches? No — the comment at lines 411-414 says "Closes #0154 — without this, leaf agents whose dispatcher has already exited lose watchdog coverage and silently never resume." That intent demands the anchor land in main.
- **Issue:** #0174

#### 3. Watchdog start logs `anchor_name: "claude.exe.old.<unix-ms>"` after a Claude Code update — name-matching cannot whitelist it

- **Category:** brittleness / contributing factor (not a primary cause but exacerbates the picture)
- **Severity:** low-medium
- **Type:** observed (single concrete log line, but the scenario is specific to Claude Code updates so user impact tracks update cadence)
- **Evidence:**
  - Today's watchdog re-start: `{"ts":"2026-05-06T21:09:35...","event":"start","anchor_pid":57332,"anchor_name":"claude.exe.old.1777935765627","anchor_count":2,...}` — observed in `dydo/_system/.local/watchdog.log`.
  - On Windows, Claude Code self-updates by renaming its current `claude.exe` to `claude.exe.old.<epoch-ms>` and dropping the new binary in place. Adele's running claude (PID 57332) was launched against the pre-update binary, so the OS-reported image name remains `claude.exe.old.1777935765627` for the rest of that process's life.
  - `Services/ProcessUtils.Ancestry.cs:95-98 MatchesProcessName`: `Path.GetFileNameWithoutExtension(actualName).Equals(needle, ...)`. With actualName = `"claude.exe.old.1777935765627"`:
    - GetFileNameWithoutExtension strips only the **last** ".<token>" segment → `"claude.exe.old"`.
    - That doesn't equal `"claude"` or `"node"` → `MatchesProcessName` returns false.
  - **Concrete consequence:** any new `dydo agent claim` whose claude ancestor is the post-update process will fail to find a match in `FindClaudeAncestor` and either fall back to the parent shell (registering a non-claude PID) or return null. This is the same family as #0151 — that issue assumed `node` was the worst case, but `claude.exe.old.<ts>` is a third one.
  - Watchdog kill targets share the same whitelist (`WatchdogService.cs:19-22 ClaudeProcessNames = { "claude", "node" }`) so the watchdog also will not kill a `claude.exe.old.<ts>` process on auto-close — though "running an outdated claude" is a transient condition that resolves on user restart.
- **Proposed fix path:** broaden `MatchesProcessName` (or add a sibling) to recognise the `claude.exe.old.<digits>` rename pattern. The actual ImageName from the OS is preserved for the lifetime of the process, so a regex-equivalent check (`^claude(\.exe(\.old\.\d+)?)?$` plus `^node(\.exe)?$`) would close it.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/ProcessUtils.Ancestry.cs:33-98`; `Services/ProcessUtils.cs:36-58` (`GetProcessName`); `Services/WatchdogService.cs:9-22, 195-207, 609-633` (anchor write, `KillClaudeProcesses` whitelist); the watchdog start log line.
- **Independent verification:** Verified the cited log line via `grep '"event":"start"' watchdog.log | tail` — `"anchor_name":"claude.exe.old.1777935765627"` is present. Walked the matcher arithmetic by hand: `Path.GetFileNameWithoutExtension("claude.exe.old.1777935765627")` strips only the trailing `.<token>`, returning `"claude.exe.old"`. `"claude.exe.old".Equals("claude")` and `.Equals("node")` both false. The same whitelist gates `KillClaudeProcesses` (line 622), so a `claude.exe.old.<ts>` target also fails the auto-close kill. Note the existing anchor for PID 57332 was registered before the rename (the file is keyed by PID, not name), which explains how a `claude.exe.old.<ts>` anchor exists despite `MatchesProcessName` being unable to register one — the bug bites future claims and future kills, not pre-rename anchors.
- **Alternative explanations considered:** Could the watchdog kill not need to match by name because PID alone is sufficient? No — `KillClaudeProcesses` (lines 613-623) is fail-closed by name precisely because the substring match earlier in the pipeline (`FindProcessesByCommandLine`) returns terminal-emulator PIDs that happen to carry the prompt in argv (closes #0122). Loosening the name match to PID-only would re-open that vulnerability. The proposed regex broadening preserves the fail-closed property while accepting the rename pattern.
- **Issue:** Augmented #0151 (added the `claude.exe.old.<unix-ms>` post-update case to the same `MatchesProcessName` family).

#### 4. The auto-resume launch path skips worktree setup that the original dispatch performed — possible silent data loss on resumed releases

- **Category:** symmetry violation between dispatch and resume
- **Severity:** medium
- **Type:** obvious (code comparison)
- **Evidence:**
  - Original dispatch into a worktree (`Services/WindowsTerminalLauncher.cs:38-44, 48-54, 64-69`) wraps `claude '{prompt}'` in:
    ```
    Set-Location {wtDir};
    {junction-creation};
    try { dydo worktree init-settings --main-root '{escapedRoot}' } catch ...;
    Start-Sleep -Seconds 1;
    try { ... claude '{prompt}' ... }
    finally { Set-Location '{escapedRoot}'; dydo worktree cleanup {worktreeId} --agent {agentName} }
    ```
  - Resume launch (`Services/WindowsTerminalLauncher.cs:75-85 GetResumeArguments`):
    ```
    -NoExit -Command "
      $env:DYDO_AGENT='{agentName}';
      Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue;
      Start-Process -WindowStyle Hidden -FilePath dydo -ArgumentList 'wait' | Out-Null;
      claude --resume '{sessionId}' '{prompt}'
      {TerminalReset}
    "
    ```
  - The resume path:
    - Has no explicit `Set-Location` (relies on `wt --startingDirectory` in `LaunchResume` line 102-104; that works for wt but the PowerShell-only fallback path on line 121-127 sets only `psi.WorkingDirectory` — also fine, but the asymmetry is invisible to a reader).
    - **Does not** recreate junctions or run `dydo worktree init-settings`. If a worktree was migrated, manually fixed, or had its junctions cleared between the original dispatch and the resume, the resumed claude lands in a worktree without the junctions it expects (no `dydo/agents/`, `_system/roles/`, etc.). Symptom: dydo guard fires on every read because off-limits / role-permission lookups can't find the role file.
    - **Has no `finally { dydo worktree cleanup ... }` block.** When the resumed agent eventually releases, the worktree is **never cleaned up by the resume tab.** The dispatcher's tab that originally created the worktree has long since exited (that's why we resumed). The agent is gone, the worktree dir lingers; only `dydo worktree prune` periodically catches it.
- **Proposed fix path:** the resume launch should mirror the dispatch launch's worktree wrapper. Two options:
  1. Have the resume tab read the agent's `.worktree-id` and emit the same `Set-Location → init-settings → Start-Sleep → try { claude --resume ... } finally { worktree cleanup }` structure.
  2. Centralise the worktree wrapper in a single reusable PowerShell-script generator and call it from both Launch paths.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/WindowsTerminalLauncher.cs:11-127` (full file — `GetArguments`, `GetResumeArguments`, `LaunchResume`, `Launch`); `Services/LinuxTerminalLauncher.cs:1-130` (spot-check for analogous gap).
- **Independent verification:** Direct line-by-line comparison: `GetArguments` for the worktree case (lines 32-44 with `mainProjectRoot`, lines 47-54 without) emits `Set-Location → GeneratePsJunctionScript → init-settings → Start-Sleep -Seconds 1 → try { claude } finally { Set-Location ... ; dydo worktree cleanup ... }`. `GetResumeArguments` (lines 75-85) emits only `agentEnv → Remove-Item Env:CLAUDECODE → Start-Process dydo wait → claude --resume → TerminalReset`. Zero overlap on the worktree-specific concerns. Cross-checked Linux: `LinuxTerminalLauncher.ApplyOverrides` (lines 16-19) wires `WorktreeSetupScript` and `WorktreeCleanupScript` into the dispatch path; `BuildResumeBashCommand` (lines 53-67) does not — same family.
- **Alternative explanations considered:** Could the resume path intentionally skip junction recreation because junctions persist across the original tab's lifetime? Plausible for short crashes, but worktree junctions are created in the dispatch tab's PowerShell script body (`GeneratePsJunctionScript`) inside the dispatched terminal — they are not part of `dydo worktree create`. The original tab exited at crash; nothing else recreates them. Could the `finally { worktree cleanup }` omission be intentional because `dydo worktree prune` will catch it? `prune` is opportunistic; the dispatch path uses `finally` for hard guarantee. The asymmetry is a real symmetry violation, not a documented exception.
- **Issue:** #0175

#### 5. The 60-s warmup gate combined with `IsBadSessionFailFast` and `SaturateResumeAttempts` interact incorrectly when the resumed claude is still rehydrating

- **Category:** logic bug / overlapping with #1
- **Severity:** medium
- **Type:** tested (logic walk-through cross-referenced against today's data)
- **Evidence:** see Finding #1 for the data; this finding isolates the algorithmic issue.
  - `IsBadSessionFailFast` (`WatchdogService.cs:544-548`) is true when warmup elapsed AND `ClaimedPid == PreResumePid`. But `PreResumePid` is captured from `state.md` which is exactly the value the resumed claude must overwrite via `RefreshClaimedPid`. Until the resumed claude makes its first tool call, the field necessarily equals `PreResumePid`.
  - The gate doesn't check whether the resumed terminal's `launched_pid` is alive. If the launched claude is still running (rehydrating), it is wrong to declare the resume "bad." If the launched claude is dead (the actual bad-session symptom), then it is correct.
  - `SaturateResumeAttempts(cap=3)` is a heavy hammer for a guess. Combined with `ResetResumeBookkeeping` running on the eventual same-session re-claim (#0153 fix), the saturation does eventually self-clear — but for the duration of the rehydration the agent is "blocked" from a watchdog perspective and would not be resumed if it crashed AGAIN during rehydration.
- **Proposed fix path:** as in Finding #1 — `IsBadSessionFailFast` should also assert `!ProcessUtils.IsProcessRunning(launchedPid)` (or equivalent — the launched PID is logged by the watchdog at resume time but not threaded back into the resume context; this would require persisting `launched-pid` next to `pre-resume-pid` in state.md).
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/WatchdogService.cs:437-481, 538-548` (`PollAndResumeForAgent`, `ResumeContext`, `IsBadSessionFailFast`); `Services/AgentRegistry.cs:327-348` (`HandleExistingSession`, `RefreshClaimedPid` callsite); `Services/WatchdogLogger.cs` (verified `launched_pid` is logged but not persisted to `state.md` or `.session`).
- **Independent verification:** Read the predicate directly. `IsBadSessionFailFast(ctx) = ctx.LastResumeAt.HasValue && (now - ctx.LastResumeAt.Value) >= ctx.Gate && ctx.PreResumePid.HasValue && ctx.ClaimedPid == ctx.PreResumePid.Value`. No `IsProcessRunning(launchedPid)` term. The launched PID flows through `LaunchResumeOverride` and `LogResume` (line 480) but is never written back into `state.md` — so even if `IsBadSessionFailFast` wanted to consult it, the field doesn't exist. `RefreshClaimedPid` (`AgentRegistry.cs:341`) only fires after the resumed claude calls `dydo agent claim`, which requires the claude binary to have rehydrated and reached its first tool call. For a slow rehydration that takes minutes, the predicate is true for the entire window, regardless of liveness.
- **Alternative explanations considered:** Could `SaturateResumeAttempts` be adequate as-is because `ResetResumeBookkeeping` (#0153) clears the cap on the eventual same-session re-claim? Partly — the cap saturation does self-clear. But the durable harm is twofold: (a) the watchdog log records `resume_blocked` while the agent is in fact recovering, which is what the user reads as "auto-resume is broken"; (b) for the duration of the rehydration, if the claude crashes AGAIN, the watchdog will not resume it (cap is at the limit). Both harms argue for adding the liveness check rather than relying on self-healing.
- **Issue:** Subsumed under #0173 (same defect; the issue's "Resolution" section enumerates the liveness-check fix this finding recommends).

### Hypotheses tested and not reproduced

- **"Auto-resume code itself killing healthy sessions."** Not reproduced. The watchdog kills a claude only via `KillClaudeProcesses` in the auto-close path (`WatchdogService.cs:613-633`), gated on `auto-close: true && status: free`. A working agent does not satisfy that gate. No evidence in the kill-log entries in `watchdog.log` of the watchdog killing a working agent. (Out of scope: `auto-close` cleanup is unrelated to crash recovery.)
- **"Context-window pressure as a primary harness-side crash cause."** Plausible from the user narrative ("Charlie crashed at claim time" → claim-time read of large brief; "inquisitors crash during long-running probes" → probes accumulate context) but not provable from this audit data. The audit captures only what the dydo guard sees (post-tool-call). It cannot witness Claude Code's own resource state. **Out of scope** for this prosecution — needs harness-side instrumentation that doesn't exist.
- **"Watchdog never running."** Not reproduced. `watchdog.log` shows continuous tick output every 10 s through the present (last tick stamped within seconds of report writing). Watchdog liveness is fine; what it does on resume is the issue.
- **"`.events` sidecar lifecycle leak."** Not reproduced as a *crash* cause but documented as a useful operational fact (see Audit Inventory): the sidecar isn't deleted on cross-process release, which inflates the count of "abrupt-end" sidecars and makes the sidecar a poor diagnostic for crash detection by itself. Counterintuitively a sidecar ending in `Release` is the normal state for a released session (because the Release event itself is appended cross-process).

### CRASH-RATE NUMBERS

The user's framing ("crashes have grown more frequent") is largely about **detection** rather than absolute count, because before v1.4.0 there was no explicit auto-resume to fail. The closest quantification this audit data supports:

- **Total event-sidecar-era sessions:** 249 (≈ all sessions claimed since 2026-04-11)
- **Sidecars without a final `Release`:** 34 — but 2 are currently-live agents (this Brian session, Adele), and several are deliberate auto-resume smoke-test crashes (Victor 9cb534e7, Frank 8a9a62ed). True abandoned/crashed-without-recovery is **on the order of 15-20** across the entire 24-day window, **not** a steeply rising count.
- **Watchdog-observed crash detections (Apr 29 – May 6):**
  - 39 distinct `resume` first-fires across 8 days = ≈ 5 detected crashes/day average. Today (May 6) had 4 detected crashes — slightly above average but within the normal range.

The user's "feels worse" perception is real but driven by Finding #1 (auto-resume LOOKS broken because the watchdog is logging `resume_blocked` for sessions that are in fact recovering), not by a step-change in absolute crash frequency.

### AUTO-RESUME RATE NUMBERS

| Period | Resumes attempted | Hit cap-3 (saturated) | Reached blocked-state | Eventual `Release` for the resumed session |
|--------|-------------------|----------------------|----------------------|------------------------------------------|
| Apr 30 – May 2 | 29 | most reached cap=3 (no warmup gate; 10s polling) | 1 | partial — agents that recovered did so within the 30s window |
| May 4 – May 6 | 11 | all saturated to cap on first-fire (warmup-gate failsafe) | 11 | **3 of 4 (today): 75% true success despite "blocked" log** |

So the **actual recovery rate is high (~75% sampled today)**; the **logged "success" rate is ~0%** because every resume immediately produces a `resume_blocked` line. That is the gap behind the user's perception.

### ROOT-CAUSE VERDICT

**Finding #1** (ResumeWarmupGate=60 s false-positive `no_refresh_after_warmup` saturating cap on real-but-slow rehydrations) is the leading explanation for the user's symptom. It is supported by direct logfile evidence on multiple sessions across multiple days, and the regression maps cleanly onto a known, dated code change (`9b27195`, 2026-05-02, the v1.4.3 merge that introduced both the gate and the bad-session failsafe).

**Finding #2** (worktree anchor-dir mismatch) is a real bug independent of Finding #1, and contributes to the same family as the open #0154 (watchdog dies via `anchor_gone` while leaf agents are still working). It is not the dominant user-visible symptom today but warrants a concrete fix.

**Finding #4** (resume launch missing worktree wrapper) and **Finding #3** (`claude.exe.old.<ts>` not whitelisted) are contributing factors that compound the picture but are not the headline.

### RECOMMENDED IMMEDIATE ACTION

For balazs, in priority order:

1. **Cheapest test of Finding #1 (do this first):** raise `ResumeWarmupGate` from `TimeSpan.FromSeconds(60)` to `TimeSpan.FromMinutes(5)` and re-run the workload that's been crashing. Single-line change in `Services/WatchdogService.cs:73`. If the user's "auto-resume feels broken" perception flips to "auto-resume mostly works again," Finding #1 is confirmed and the proper fix (per Finding #5: liveness-of-launched-PID gate, plus drop SaturateResumeAttempts) becomes the follow-up code-writer task.
2. **Don't restart the machine, don't update Claude Code, don't roll back dydo.** The v1.4.6 install is benign with respect to this symptom (no Services/ changes vs v1.4.5). The bug pre-dates v1.4.6 by 4 days.
3. **File a new issue for Finding #2** (anchor-dir mismatch in worktree-claimed agents). It cross-references the existing open #0154 but is the Windows variant the existing issue text doesn't cover.
4. **Augment #0151** with the `claude.exe.old.<ts>` post-update process-name case (Finding #3). It's the same fix family.
5. **Defer Finding #4** (resume worktree wrapper) to a planned cleanup pass — it's mostly a hygiene/symmetry issue, not a hot-path bug.

A judge will dispatch separately to rule on each finding.

### Cross-project replication of Finding #1

The brief asked for a pass over the second project at `C:/Users/User/Desktop/LC/dydo/`. A targeted scan there shows the same regression on the same dates:

- 314 event-sidecars total; 34 abrupt-end (similar ratio to the canonical project).
- LC's `dydo/_system/.local/watchdog.log` shows the **identical phase change**:
  - `2026-05-01T16:40` Charlie 4e28e084 → resume 1, 2, 3 (cap-3 saturation, no warmup gate yet)
  - `2026-05-04T17:55` Charlie 88d1068a → resume 1, then 60 s later `resume_blocked: no_refresh_after_warmup`
  - `2026-05-04T18:26` Brian 680399eb → resume 1, then `resume_blocked` 60 s later
  - `2026-05-04T22:15`, `2026-05-05T17:29` — same shape every time.
- The pattern reproduces independently of project state, configuration, or per-project agent activity. **Finding #1 is platform-wide.**

### Confidence: high (Findings #1, #2); medium (Findings #3, #4, #5)

Strong on Finding #1 — direct logfile evidence reproduces in both the canonical project and the LC project across multiple agents. Strong on Finding #2 — direct filesystem inspection. Finding #3 is a single observed log line plus path-to-bug analysis (the trigger requires a Claude Code update, which is rare). Findings #4 and #5 are logic-walk / code-comparison without live execution.

What was NOT examined thoroughly:
- Live reproduction of an artificial crash (e.g. forcibly killing a claude PID and watching the watchdog) was avoided to preserve session memory pressure during the investigation.
- Linux/Mac variants of Finding #2 (anchor-dir mismatch) were not exhaustively traced; the platform-specific Linux code paths likely have an analogous issue but the evidence in this report is Windows-only.
- The LC project's watchdog log was sampled (cross-project replication of Finding #1) but not exhaustively cross-referenced against its audit sidecars — the matching pattern was already conclusive.

## Methodology caveat: survivorship bias in the resume-success rate

The "~75% of resumes succeed" figure in the AUTO-RESUME RATE NUMBERS section is correct **only as the success rate of the resumes the watchdog actually attempted** — not the crash-recovery rate from the user's perspective. Read literally, it overstates the system's recovery coverage; future inquisitors should treat it as one slice of a wider denominator, not the headline.

Specifically:

1. **The denominator excludes non-attempts.** Sessions where the watchdog never even fired auto-resume — because the anchor was registered in the wrong directory (Finding #2), the resume launch path skipped the worktree wrapper (Finding #4), or the post-update process name `claude.exe.old.<unix-ms>` was not whitelisted (Finding #3) — never enter the count at all. They are silent failures, not "blocked" successes.
2. **Manual recoveries are entirely off-books.** When the user runs `claude <agent> --inbox` to re-claim by hand, neither the numerator nor the denominator changes. balazs reports this happens often, so the watchdog log systematically undercounts both crashes and recoveries.
3. **The user's lived experience is consistent with the data.** balazs's perception of auto-resume going "~50% → ~33% → ~0%" is jointly explained by (a) Finding #1's log-honesty bug, where the watchdog logs `resume_blocked` for sessions that are in fact recovering, AND (b) the share of crashes where resume is never attempted ramping up over time. The 75%-of-attempts figure does not refute either narrative.
4. **The right framing is a 4-bucket categorization** of every crash: (a) auto-resume attempted and succeeded, (b) attempted and failed, (c) not attempted (resume never fired), (d) manual recovery (user-initiated re-claim with no prior `resume_attempted`). The instrumentation for this is the planned PR3 — a `resume_outcome` event plus a `recovery_kind` field on Claim events. Once that ships, all four counts become a one-grep query.
5. **Until PR3 ships, any "resume success rate" framing should explicitly state which denominator it uses.** Default to the 4-bucket vocabulary, not a single percentage.

## 2026-05-08 — Dexter

Follow-up inquisition: crash-recovery rate after PR1/PR2/PR3 (post-036b88c)

### Scope

- **Entry point:** Adele's inbox brief `investigate-agent-crashes-followup` (file `dydo/agents/Dexter/inbox/a98e1346-investigate-agent-crashes-followup.md`). Mandate: re-measure with the survivorship-bias-aware 4-bucket framework, now that the PR1/PR2/PR3 fixes have shipped, and identify any remaining issues.
- **Versions in scope:** dydo source-tree HEAD `036b88c` (PR3, 2026-05-07 21:07 UTC); installed binary mtime 2026-05-06 21:06 UTC (predates all three PRs — see Finding #1 below).
- **Defensive constraint:** identical to Brian's section. No full-file Read of any audit JSON or `.events`. Only `head` / `tail` / `wc -l` / `grep` / per-file loops with `tail -1`. No open-ended Bash poll-loops (Charlie's 15:43 UTC May 7 crash mode was `until [ -s /tmp/claude/... ]` — fatal).

### Finding #1 (NEW, headline) — DEPLOYMENT GAP: PR1/PR2/PR3 are committed but not installed; the running watchdog is the pre-fix binary

- **Category:** deployment / process gap (root cause of the brief's premise being false)
- **Severity:** **critical** — the entire brief assumes the fixes are live; they are not. Any "post-fix" measurement against the current data is reading pre-fix behavior.
- **Type:** obvious (direct inspection of binary mtime + watchdog process start time + behavioral fingerprint of running code)
- **Evidence:**
  - **Commit timestamps (UTC, from `git log --date=iso`):**
    - PR1 `e80730c` `fix(watchdog): make resume_blocked log honest…` — 2026-05-07 13:46:05
    - PR2 `de50134` `fix(watchdog): land worktree anchors in main + restore resume worktree wrapper` — 2026-05-07 15:18:54
    - PR2.1 `87d9f6f` `docs(watchdog): correct RegisterMainAnchor xmldoc` — 2026-05-07 17:12:09
    - PR3 `036b88c` `feat(watchdog,audit): add resume_outcome event + recovery_kind on Claim audit` — 2026-05-07 21:07:13
  - **Installed binary:** `C:\Users\User\.dotnet\tools\dydo.exe`, `LastWriteTime = 2026-05-06 21:06:32` (UTC). 16+ hours older than PR1; ~24 hours older than PR3. Source code is post-PR3, deployed binary is pre-PR1.
  - **Live watchdog processes (both projects):**
    - DynaDocs: `Get-Process -Id 64796` → `dydo.exe`, `StartTime = 2026-05-06 21:09:35` UTC. Single uptime-of-record since the rotation (only 2 `start` events in the current `watchdog.log`, both predating PR1).
    - LC project: `watchdog.pid` = `35796`, last `start` event in LC's `watchdog.log` is `2026-05-07T14:41:05Z`. PR1 was committed 55 minutes earlier, but the binary that started at 14:41 was the one already at `~/.dotnet/tools/dydo.exe` (mtime 2026-05-06 21:06) — so even the post-PR1-clock-time restart is pre-PR1 by binary version.
  - **Behavioral fingerprint of the running code (proves pre-PR1 by direct observation, independent of process startup time):**
    1. **All `resume_blocked` events fire 60 s after the paired `resume`.** Per `Services/WatchdogService.cs:69` the source has `ResumeWarmupGate = TimeSpan.FromMinutes(5)` (PR1's 5-min gate). If the running watchdog were post-PR1, no `resume_blocked` could fire before 5 minutes elapsed. Sample (DynaDocs, all 14 resume/blocked pairs in current `watchdog.log`):

       | resume ts (UTC) | resume_blocked ts (UTC) | Δ | agent / session |
       |---|---|---|---|
       | 2026-05-05 17:28:07 | 17:29:07 | 60 s | Brian / 1317c9ea |
       | 2026-05-05 17:47:00 | 17:48:00 | 60 s | Charlie / 1f852665 |
       | 2026-05-05 18:28:36 | 18:29:36 | 60 s | Emma / 0e1a3c6e |
       | 2026-05-05 18:30:06 | 18:31:06 | 60 s | Charlie / 3ec202b5 |
       | 2026-05-05 18:30:56 | 18:31:56 | 60 s | Frank / 243f8444 |
       | 2026-05-05 19:20:15 | 19:21:15 | 60 s | Charlie / a0027be6 |
       | 2026-05-06 13:27:00 | 13:28:06 | 66 s | Charlie / 8b52b181 |
       | 2026-05-06 16:16:32 | 16:17:34 | 62 s | Brian / f9936e33 |
       | 2026-05-06 16:44:53 | 16:45:56 | 63 s | Frank / 04d1191f |
       | 2026-05-06 18:03:06 | 18:04:11 | 65 s | Brian / 4c2838f8 |
       | **2026-05-07 12:29:34** | **12:30:34** | **60 s** | Brian / 61f51876 |
       | **2026-05-07 13:39:21** | **13:40:21** | **60 s** | Charlie / 5dbe5e3c |
       | **2026-05-07 15:46:02** | **15:47:03** | **61 s** | Charlie / 4090052a (post-PR1 by 2 h on the wall clock — but still 60 s gate) |
       | **2026-05-07 16:31:49** | **16:32:49** | **60 s** | Charlie / 1f878107 (post-PR1 by 2 h 45 min on the wall clock — but still 60 s gate) |

       The two May 7 events at 15:47 and 16:32 UTC fired AFTER PR1's commit timestamp. If PR1 were running, the gate would be 5 min — these resume_blocked events would not exist. They do exist. The running binary is pre-PR1.

    2. **Zero `resume_outcome` events** anywhere. PR3 (`036b88c`, `Services/WatchdogService.cs:507-515`) emits `WatchdogLogger.LogResumeOutcome` paired with every `resume_blocked` and one-shot per gave-up episode. Counts: `grep -c '"event":"resume_outcome"' watchdog.log` = 0 in both DynaDocs (current + rotated) and LC. With at least one resume in the post-PR3 wall-clock window (LC Charlie 7565491f at 22:50:14 UTC, blocked at 22:51:14 — 90 minutes after PR3's commit), if PR3 were running there would be at least one `resume_outcome` line. There are zero.

    3. **Zero audit Claim events carry the PR3 recovery fields.** `grep -r '"recovery_kind":' dydo/_system/audit/2026/` = 0 occurrences in both projects. Same for `"resume_predecessor_session":`. PR3 wires these into `Models.AuditEvent` and the audit emitter on the agent-claim path; if any `dydo agent claim` had run on a post-PR3 binary, at least the `recovery_kind: "fresh"` field would be visible. None is.

  - **Watchdog source code is post-PR3** (verified by `grep` in `Services/WatchdogService.cs`):
    - line 69: `internal static readonly TimeSpan ResumeWarmupGate = TimeSpan.FromMinutes(5);` (PR1)
    - lines 662-667: `IsBadSessionFailFast` predicate now requires `(!ctx.LaunchedPid.HasValue || !ProcessUtils.IsProcessRunning(ctx.LaunchedPid.Value))` (PR1)
    - lines 487-489, 512-515: `LogResumeOutcome` calls (PR3)
    - lines 519-525: silent-skip-on-rehydrating-but-alive branch (PR1)
    - line 502-503: `IsBadSessionFailFast` still calls `SaturateResumeAttempts` — but only after the launched-PID liveness check, so the false-positive cap-saturation is closed (PR1).
- **Why this matters:**
  - balazs's user-visible symptoms ("auto-resume looks broken", "watchdog logs `resume_blocked` while the agent recovers", "anchors lost when the dispatcher exits") are all still going to be observed because the fixes that close them are not in the running process.
  - Adele's brief asks me to "measure the post-fix recovery rate". There is no post-fix data window. The bucket counts I produce below are necessarily pre-fix.
  - The fix path is one command (`dotnet tool update --global dydo` or `dydo install`, depending on the project's release process) plus one watchdog restart per running watchdog. That command has not been issued.
- **Proposed fix path:**
  1. Build current HEAD (`036b88c`) and run the install pipeline that publishes the global tool. Verify `dydo.exe` mtime advances past 2026-05-07 21:07 UTC.
  2. Kill the two running pre-fix watchdogs (DynaDocs PID 64796 and LC PID 35796) so the next claim spawns a watchdog from the new binary. Don't wait for the watchdog to "self-update" — `EnsureRunning` re-uses any existing PID written to `watchdog.pid` and the binary stays whatever was on disk when that process started. Confirm new watchdog is post-PR3 by grepping the next `start` event for `resume_outcome` capability; the simplest functional probe is to force a single test crash and verify a `resume_outcome` line is emitted.
  3. Re-run this inquisition (or schedule a recurring sample) once the post-fix window has accumulated some natural traffic — at least a day's worth, ideally with one orchestrator-driven workload on each project.
- **Files examined:**
  - `git log --format="%H %ad" --date=iso e80730c de50134 87d9f6f 036b88c` (commit timestamps).
  - `Get-Item ~/.dotnet/tools/dydo.exe` (`LastWriteTime`, `CreationTime`).
  - `Get-Process -Id 64796 -IncludeUserName | Format-List Id,ProcessName,StartTime,Path`.
  - `dydo/_system/.local/watchdog.log` (DynaDocs, 1057 KB, all 14 resume/blocked pairs).
  - `C:/Users/User/Desktop/LC/dydo/_system/.local/watchdog.log` (LC, 1.8 MB, last 5 resume/blocked pairs sampled).
  - `Services/WatchdogService.cs:65-76, 487-555, 660-685` (source-side post-PR3 confirmation).
  - `dydo/_system/audit/2026/` and `C:/Users/User/Desktop/LC/dydo/_system/audit/2026/`: `grep -rc '"recovery_kind":'` returns 0 in both.
- **Independent verification:** Three independent signals all triangulate to "binary is pre-PR1": process StartTime, binary mtime, and behavioral fingerprint (60-s gate + missing PR3 events). Any one would suffice; all three together are conclusive. The 60-s gate fingerprint is the strongest because it doesn't depend on filesystem timestamps that can be touched.
- **Alternative explanations considered:**
  - *"Maybe the binary is post-PR1 but the running watchdog process is the pre-PR1 instance, and a fresh watchdog would be post-PR1."* — No, the binary mtime (2026-05-06 21:06 UTC) is itself older than every PR. Even a fresh watchdog spawn today would launch the same pre-PR1 image.
  - *"Maybe the user installed PR1+PR2+PR3 but the dotnet global-tool install failed silently."* — Possible but indistinguishable from "did not install". The user-facing remedy is the same.
  - *"Maybe the PR commits in the brief refer to a different repo."* — Verified: `git log e80730c -1` and `git log 036b88c -1` resolve in this worktree (which is `dydo/_system/.local/worktrees/investigate-agent-crashes-followup`, the canonical repo). The commits are real and on master.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Get-Item ~/.dotnet/tools/dydo.exe` (LastWriteTimeUtc = 2026-05-06 21:06:32 — verified independently); `Get-Process -Id 64796` (StartTime = 2026-05-06 21:09:35 UTC, PID still alive at ruling time); `Get-Process -Id 35796` (StartTime = 2026-05-07 14:41:04 UTC, PID still alive); `git log --date=iso-strict e80730c de50134 87d9f6f 036b88c` (PR1=2026-05-07T13:46:05Z, PR2=15:18:54Z, PR2.1=17:12:09Z, PR3=21:07:13Z UTC); `Services/WatchdogService.cs:69, 205-210, 487, 513, 662` (post-PR3 source confirmed: ResumeWarmupGate=5min, RegisterMainAnchor → FindMainDydoRoot → RegisterAnchor, two LogResumeOutcome call sites, IsBadSessionFailFast predicate); `Services/AgentRegistry.cs:431` (call site for RegisterMainAnchor on claim); `dydo/_system/.local/watchdog.log` (DynaDocs, all 14 resume/resume_blocked pairs sampled, gap range 60-66s); `C:/Users/User/Desktop/LC/dydo/_system/.local/watchdog.log` (LC, last `start` event 2026-05-07T14:41:05Z PID 35796, last resume_blocked 2026-05-07T22:51:14Z gap 60s — fired ~1h44min after PR3's commit); `grep -c '"event":"resume_outcome"'` = 0 in DynaDocs and LC; `grep -rl '"recovery_kind"'` = 0 files in `dydo/_system/audit/2026/` and `C:/Users/User/Desktop/LC/dydo/_system/audit/2026/`.
- **Independent verification:** Re-ran every signal independently rather than trusting the inquisitor's correlation. Three signals triangulate to "running binary is pre-PR1": (a) binary mtime predates PR1 by 16+ hours; (b) both watchdog StartTimes load that binary (DynaDocs predates PR1; LC's post-PR1-clock-time start still loaded the same May-6 binary on disk); (c) the 5-min gate fingerprint — two May-7 resume_blocked events at 15:47 and 16:32 UTC fire 60-61s after their paired resume, AFTER PR1's commit; if PR1 were running these would not exist. Plus PR3's two emit-sites (resume_outcome event, recovery_kind audit field) never fire anywhere. The 5-min-gate signal is the strongest because it doesn't depend on filesystem timestamps. Source-side post-PR3 verified by grep — the gap is purely deployment, not authoring.
- **Alternative explanations considered:** (1) "Maybe the binary on disk is post-PR1 and only the running process is stale" — refuted: binary mtime itself predates PR1, so a fresh spawn today would still be pre-PR1. (2) "Maybe the dotnet global-tool install ran but failed silently" — possible but indistinguishable from "did not run"; remedy is the same. (3) "Maybe PR commits are in a different repo" — refuted: `git log` resolves all four commits in this worktree (which is the canonical repo per `architecture.md` §"Worktree Dispatch"). (4) "Maybe LC has its own binary" — refuted: `Get-Process -Id 35796` shows `Path = C:\Users\User\.dotnet\tools\dydo.exe`, the same global tool path as DynaDocs.
- **Issue:** #0176

### Finding #2 — Resume launches still landing in the pre-PR2 worktree-anchor location (corroborates Finding #1, independent measurement)

- **Category:** observation that strengthens the deployment-gap claim
- **Severity:** medium (subordinate to #1 — once #1 is closed, #2 closes with it)
- **Type:** obvious (live filesystem inspection)
- **Evidence:**
  - This Dexter session was claimed inside the worktree at `dydo/_system/.local/worktrees/investigate-agent-crashes-followup`.
  - Worktree anchor dir contents (relative to worktree): `dydo/_system/.local/watchdog-anchors/79260.anchor` — my own claude PID.
  - Main anchor dir contents (`C:/Users/User/Desktop/Projects/DynaDocs/dydo/_system/.local/watchdog-anchors/`): `56156.anchor`, `57332.anchor` — neither is mine. The watchdog reading main only sees those two.
  - `Get-Process -Id 79260,56156,57332`: 79260 is `claude` (me, in worktree); 56156 is `claude` started 2026-05-07 22:46:58 UTC (someone else, post-PR2 wall-clock); 57332 is `claude` started 2026-05-04 19:24:24 UTC (Adele, the long-running session).
  - PR2's `de50134` was supposed to land worktree-claimed agents' anchors in main via `RegisterMainAnchor`. Mine landed in the worktree, exactly the bug Brian reported. This is independent corroboration that the running `dydo agent claim` binary is pre-PR2.
- **Proposed fix path:** subsumed by Finding #1 (install + restart). After the install, claim a worktree-resident agent and verify the anchor file shows up under the main `_system/.local/watchdog-anchors/`, not the worktree's.
- **Files examined:** the two anchor dirs (live `ls`); `Get-Process` snapshots; `dydo agent status` for my own session.
- **Independent verification:** the anchor for PID 79260 is mine (my session was claimed at 2026-05-07 22:44:50 UTC; a `claude` PID 79260 with a similar StartTime exists). The anchor file lives only in the worktree's `_system/.local/`, and the main anchor dir does not contain it — `dydo/_system/.local/` is not on the worktree junction list (per `architecture.md` §"Worktree Dispatch", only `dydo/agents/`, `_system/roles/`, `project/issues/`, `project/inquisitions/` are junctioned).
- **Alternative explanations considered:** Could the worktree's anchor dir be a junction to main that I'm misreading? No — both anchor files exist on the local filesystem and `ls` returns different lists in each location.
- **Judge ruling:** CONFIRMED
- **Files examined:** Live `ls` of `C:/Users/User/Desktop/Projects/DynaDocs/dydo/_system/.local/watchdog-anchors/` → only `57332.anchor` (Adele); live `ls` of `dydo/_system/.local/worktrees/investigate-agent-crashes-followup/dydo/_system/.local/watchdog-anchors/` → `77920.anchor` (Charlie/me, claimed 2026-05-08 01:01:37 UTC) and `79260.anchor` (Dexter, now-dead PID); `Get-Process -Id 79260,77920,57332,56156` resolved 79260 = gone, 77920 = claude PID alive matching my own claim, 57332 = Adele alive, 56156 = gone; `Services/AgentRegistry.cs:431` (`WatchdogService.RegisterMainAnchor(ProcessUtils.FindClaudeAncestor(), _basePath)` call on claim); `Services/WatchdogService.cs:205-210` (RegisterMainAnchor → FindMainDydoRoot → RegisterAnchor(mainDydoRoot, ...)); `Utils/PathUtils.Discovery.cs:46-52` (FindMainDydoRoot walks back to project root via FindMainProjectRoot).
- **Independent verification:** Reproduced the exact bug Dexter saw, with my own anchor (PID 77920), claimed AFTER Dexter's report. The source-side path through `RegisterMainAnchor → FindMainDydoRoot → RegisterAnchor` is correct in source — if the running `dydo agent claim` binary were post-PR2, my anchor would have to land in the main `dydo/_system/.local/watchdog-anchors/`. It landed in the worktree's. Same call-site, same source code, same global-tool path → the only consistent explanation is that the running binary is the same pre-PR2 image documented in Finding #1.
- **Alternative explanations considered:** (1) "Could the worktree anchor dir be a junction back to main that I'm misreading?" — refuted: `architecture.md` §"Worktree Dispatch" lists the four junctioned paths and `dydo/_system/.local/` is not among them; live `ls` returns different file lists in main vs worktree, so they are distinct directories. (2) "Could the anchor have been written to main and then cleaned up?" — refuted: I'm a live process at ruling time, so the watchdog's anchor scanner would not have removed my anchor from main; and my anchor is still present in worktree. (3) "Could `FindMainDydoRoot` have a bug that resolves to the worktree?" — possible in principle, but the source is the post-PR2 logic that walks back through the worktree marker chain to the parent project root. The bug is in the running binary, not the source. Remedy is install + restart, same as Finding #1.
- **Issue:** subsumed by #0176 (Finding #1 install closes both at once).

### THE NUMBERS — 4-bucket rates (pre-fix only; no post-fix window exists)

**Headline:** there is no post-fix window to measure (Finding #1). The bucket counts below are entirely pre-fix and overlap heavily with Brian's earlier section. They are presented for completeness and to give balazs a check-in baseline against which to compare once the binary is installed and a fresh window accumulates.

**Pre-fix window (DynaDocs, 2026-05-05 11:04 UTC through 2026-05-07 22:48 UTC — current `watchdog.log`):**

The 14 unique resume episodes in this window (full table in Finding #1) split as follows when we tail the corresponding `.events` sidecar:

| Outcome | Count | Sessions |
|---|---|---|
| Resumed claude eventually emitted `Release` (bucket **a — succeeded**) | **12 / 14** | 1317c9ea, 1f852665, 0e1a3c6e, 3ec202b5, 243f8444, a0027be6, 8b52b181, f9936e33, 04d1191f, 61f51876, 5dbe5e3c, 1f878107 |
| Resumed claude never reached a tool call → no further events; sidecar tail predates the resume (bucket **b — failed**) | **2 / 14** | 4c2838f8 (no sidecar at all — this is Brian's "Brian session class" / OOM-during-rehydration hypothesis), 4090052a (sidecar tail is the `until [ -s /tmp/claude/... ]` Bash crash; resume fired 3 minutes later but the resumed claude never emitted a follow-up event — same shape as 4c2838f8) |
| Bucket **c — not attempted** (claimed agent crashed; watchdog never fired resume) | **0 confirmed** in this window | — see "Bucket (c) and (d) coverage caveats" below |
| Bucket **d — manual recovery** (no `resume` event in watchdog.log; agent re-claimed by user with new SessionId) | **0 confirmed** in this window | — same caveat |

Same window in the LC project (post LC-watchdog-restart 2026-05-07 14:41 UTC, 5 resume episodes):

| Outcome | Count | Sessions |
|---|---|---|
| **a — succeeded** | 4 / 5 | 9c68dcfc, 32786463, 85c53df9, c66b482b |
| **b — failed (or in-flight)** | 1 / 5 | 7565491f (Charlie, resume at 22:50:14, last sidecar event at 22:55:09 = `Bash`. Could be a slow rehydration still in progress at the moment of sampling, OR a genuine failure. With the live LC watchdog still ticking, this case is ambiguous.) |
| **c — not attempted** | 0 confirmed | — same caveat |
| **d — manual recovery** | 0 confirmed | — same caveat |

**Combined attempt-success rate (pre-fix), excluding the LC ambiguous case:** 16 succeeded / 18 confirmed = **89%**. With the ambiguous LC case counted as failed: 16 / 19 = **84%**.

**Brian's 75% (single-day, May 6, n = 4):** 3 of 4 sessions on May 6 reached `Release` despite the false `resume_blocked` log line. Today's expanded sample (n = 19 across 3 days, 2 projects) lands at **84-89%** depending on how the in-flight LC case is classified. **The success rate is NOT going down.** Brian's 75% was a small-n early estimate; with more data the attempt-success rate is moderately higher.

**Reframe of Brian's 75%:** the figure is the **success rate of attempted resumes**, NOT the **crash-recovery rate**. With the data we have:

- 19 attempted resumes, 16 succeeded (≈ 84%) → bucket (a) / [(a) + (b)] denominator.
- Bucket (a) / [(a) + (b) + (c) + (d)] denominator is the headline crash-recovery rate balazs asked for, and **we cannot compute it cleanly from pre-PR3 data** (see caveats below). Lower bound is 84% × P(crash → resume attempted), and we have no clean way to estimate the conditional probability without the PR3 instrumentation.

### Bucket (c) and (d) coverage caveats — why we can't compute the headline number cleanly

This is the same caveat Brian appended on 2026-05-07 (lines 294-304 above), restated with the data I observed:

1. **Bucket (c) — "not attempted" detection on pre-PR3 data is forensic and noisy.** A "claimed agent crashed but no resume fired" case looks like:
   - a `.events` sidecar that ends in something other than `Release`,
   - whose first event is `Claim`,
   - which has no matching session_id in the watchdog log,
   - and the agent was either re-claimed manually with a different session_id or simply abandoned.

   Sampling the 34 abrupt-end sidecars in DynaDocs:
   - 29 / 34 are pre-2026-04-29, before the watchdog log existed — uncrossable with the watchdog log even in principle.
   - 5 / 34 are in the watchdog-log era (2026-04-29 onward): `9cb534e7` (Apr 30, Victor smoke-test, has 3 watchdog log entries), `8a9a62ed` (May 1, Frank smoke-test, 3 entries), `134f6c2c` (May 4, **first event is `Bash` not `Claim` — pre-claim crash, no agent state for the watchdog to read; not bucket (c)**), `4090052a` (counted under bucket (b) above), `4d0d074b` (Adele's currently-live session — **excluded as live, not crashed**).
   - Result: **0 confirmed bucket-(c) cases** in the post-watchdog-log era of DynaDocs, but the search is forensic and could miss cases where a manual recovery wrote a `Release` to the original sidecar (which would hide the crash from this filter).

2. **Bucket (d) — "manual recovery" requires same-agent-different-session correlation across days.** balazs reports this happens "often" (Brian's narrative). In pre-PR3 data the only signal is "agent X has a sidecar A ending without Release, then a sidecar B starting with `Claim` of agent X minutes-to-hours later, and there is no `resume` event for sidecar A's session_id in the watchdog log." Per Brian's 2026-05-06 audit-data inventory (line 38), "the sidecar isn't deleted on cross-process release" — meaning a sidecar ending in `Release` is the **normal** state for a released session, not an indicator of recovery. The forensic signature for bucket (d) is therefore weak; we cannot reliably enumerate it without PR3.

3. **PR3 is the unblock.** Once `recovery_kind` (`fresh` / `auto` / `manual`) lands on every Claim event, bucket counts become a one-grep query: `grep -c '"recovery_kind":"manual"'` directly counts bucket (d), `grep -c '"resume_outcome".*"succeeded"'` counts bucket (a), and so on. **This is exactly the value PR3 delivers — and it isn't yet delivering it because PR3's compiled output is not on disk.** That is a one-line consequence of Finding #1 and the strongest argument for prioritising the install.

### Pre-fix vs post-fix comparison — what we'd expect, and what we'll need to re-measure

The brief asks (paraphrased): "Are bucket (c) rates dropping post-PR2? Are bucket (a) rates rising post-PR1?"

We **cannot answer these from the data on hand** because there is no post-fix window. What I can document is what each fix should change, so balazs has clear acceptance criteria for the post-install re-measure:

| Window | Behaviour expected | Behaviour observed in current data |
|---|---|---|
| pre-PR1 | `resume_blocked` fires 60 s after `resume`, regardless of resumed-claude liveness | confirmed: 14/14 DynaDocs + 5/5 sampled LC pairs are 60-66 s apart |
| post-PR1, pre-restart | same as pre-PR1 (binary unchanged → no behaviour change despite commit landing) | confirmed: the May 7 15:47 and 16:32 UTC pairs (post-PR1 by commit time) still show 60 s gap |
| post-PR1, post-restart | `resume_blocked` only fires once `IsBadSessionFailFast` is true: ≥ 5 min elapsed AND PreResumePid still claimed AND launched-PID dead OR null. Slow-rehydration sessions silent-skip (no `resume_blocked` line) | **not yet observed — install not done** |
| post-PR2, post-restart | worktree-claimed agents' anchors land in main `_system/.local/watchdog-anchors/`. Resume launches re-emit the worktree wrapper (`Set-Location` + `init-settings` + `try { … } finally { worktree cleanup }`) | **not yet observed — anchor for this Dexter session is in the worktree, not main** |
| post-PR3, post-restart | every `resume_blocked` paired with a `resume_outcome` event; `Claim` events carry `recovery_kind` / `resume_predecessor_session` / `resume_attempts_at_claim` | **not yet observed — 0 `resume_outcome` and 0 `recovery_kind` events anywhere** |

### Remaining deeper issues observed

1. **The "Brian session class" Adele asked about is still happening — and Charlie now hits it too.** Brian's 4c2838f8 (May 6, no sidecar at all) and Charlie's 4090052a (May 7, sidecar ends with `until [ -s /tmp/claude/... ]` open-ended Bash poll-loop) are the same pattern: claude rehydration completes but never reaches a first tool call. The 4090052a case is particularly informative because the session DID make a `Bash` call moments before crashing — the crash is consistent with the open-ended `until` poll-loop exhausting harness memory or hitting the harness's CPU watchdog. This is the same crash mode Adele's brief warned me to avoid. **The bug is in claude-the-binary's behaviour around long-running shell loops, not in dydo.** dydo's role is to recover from it; PR1 makes the recovery honest, but PR1 cannot make the rehydration succeed if claude-the-binary OOMs on a large session.

   - **Proposed action:** add a custom nudge / coding-standards rule against `until [ -s ... ]` and similar open-ended polls in agent shell calls. Also worth adding to `dydo/agents/*/workflow.md` defensive notes. The cost of a poll-loop crash is not just one agent — it's also the rehydration-failure tax (the resumed claude inherits the same large session and hits the same OOM).

2. **The same-task self-review oversight (Brian got picked to review his own PR2).** Adele's brief flagged this as "process gap, not a crash, but worth noting." Verified: Brian's reviewer dispatch on PR2 (de50134) targets `--task <pr2-task>` which is task-name-keyed, and the role-based "cannot review own work" check only fires when the writer and reviewer are the same agent on the same SUB-task. If task names differ by even a suffix, the gate doesn't bite. Out of scope for the auto-resume investigation but worth filing separately as a guard-tightening.

3. **Watchdog log is silent for ~6 hours despite live ticks.** DynaDocs `watchdog.log` last tick is 2026-05-07 22:48 UTC, but the watchdog process is alive (PID 64796 confirmed running at the time of this report). The tick is happening in-process; the file write may be debounced or a separate code path. Not investigated further — flagging only.

### ROOT-CAUSE VERDICT

**The dominant root cause of the user's "auto-resume looks broken" perception is the same as Brian's Finding #1: the running watchdog logs `resume_blocked` 60 s after every resume, even for sessions that go on to recover successfully (84-89% of the time).** Brian located the source-side fix; the fix is committed in PR1; **but the binary running on disk does not yet contain the fix**, so the user-visible symptom is unchanged from Brian's snapshot two days ago.

Subsidiary causes (all from Brian's report, all still active because pre-PR2):
- Worktree-claimed agents' anchors not landing in main → leaf-agent watchdog coverage is fragile (Brian's #2, this report's Finding #2).
- `claude.exe.old.<unix-ms>` post-update process-name not in the kill/match whitelist (Brian's #3) — orthogonal to deployment, will close on the next install regardless.
- Resume launch path missing the worktree wrapper (Brian's #4) — same family.

**Genuine remaining question — what crashes the rehydration in cases like 4c2838f8 / 4090052a?** Out of scope for this prosecution: requires harness-side instrumentation that does not exist. Best hypothesis is that the resumed claude inherits the same large conversation that crashed the original, and OOMs/segfaults in roughly the same place. The defensive answer is to avoid the precipitating bash patterns (`until [ -s ... ]`) — see "Remaining deeper issues" #1.

### RECOMMENDED ACTIONS (in priority order)

1. **Install the PR1+PR2+PR3 binary, kill both running watchdogs, let them respawn.** This is the blocking action. Until it happens, the user's symptom does not change and Adele's brief cannot be answered with post-fix data because there is no post-fix data.
   - Concrete: build current HEAD (`036b88c`), run the global-tool publish step, kill PID 64796 (DynaDocs) and PID 35796 (LC). The watchdog will respawn on the next agent claim.
2. **Schedule a re-run of this inquisition 24-48 hours after install.** With PR3's `resume_outcome` and `recovery_kind` instrumentation live, all four buckets become trivially countable. Concrete acceptance criterion: the re-run can be a 10-minute, single-agent task — the entire 4-bucket measurement reduces to four `grep` counts.
3. **Add a coding-standards / agent-workflow rule against open-ended Bash polls.** `until [ -s … ]`, `while ! …`, `tail -f` without a timeout, and similar patterns are the same crash family that took down Charlie at 15:43 UTC May 7 (and earlier). The rule belongs in `dydo/guides/coding-standards.md` and (probably also) as a custom guard nudge so it is enforced at tool-call time rather than after the fact.
4. **File a separate hygiene issue for the same-task self-review gap.** Reviewer-cannot-review-own-work check is task-name-keyed and trivially defeated by suffixing the sub-task; this is a guard-tightening, unrelated to crash recovery.
5. **Defer all source-side fixes to PR1/PR2/PR3** that are already merged. There are no new source-code findings in this follow-up that warrant a fourth PR. The follow-up question — does the existing PR-set actually close the symptom — can only be answered post-install.

### Confidence: high (Finding #1, Finding #2, deployment-gap conclusion); medium-high (4-bucket numbers); low (buckets c & d on pre-PR3 data — cannot compute cleanly)

### Judge ruling on THE NUMBERS, deeper issues, and recommended actions (2026-05-08 — Charlie)

- **THE NUMBERS section:** CONFIRMED methodology and figures. Spot-checked 5 of the 14 DynaDocs sidecars by `tail -1`: 1f878107 → `Release` Charlie 17:38, 4090052a → `Bash until [ -s /tmp/claude/...` (the documented poll-loop crash), f9936e33 → `Release` Brian 16:48, 61f51876 → `Release` Brian 13:47, 5dbe5e3c → `Release` Charlie 14:38. Bucket assignments match Dexter's table 5/5. The 16/19 = 84% (or 16/18 = 89% excluding the in-flight LC case) attempted-resume rate is sound. Bucket c/d low confidence is honest — the inquisitor explicitly notes PR3 is the unblock, and Finding #1 explains why PR3 isn't yet delivering it. No issue to file; this is descriptive, not a defect.
- **Remaining deeper issue #1 (open-ended Bash poll-loops):** CONFIRMED. The 4090052a sidecar tail directly demonstrates the pattern; 4c2838f8's no-sidecar shape is consistent with the same crash class. Filed as #0177. Recommendation: coding-standards rule + custom guard nudge.
- **Remaining deeper issue #2 (same-task self-review gap):** CONFIRMED. Filed as #0178.
- **Remaining deeper issue #3 (watchdog log silent for ~6 hours):** FALSE POSITIVE. Dexter's report flagged this with the disclaimer "not investigated further — flagging only." Direct check at ruling time: `wc -l watchdog.log` = 12413 lines, last 5 ticks at 23:05:49Z / 23:05:59Z / 23:06:09Z / 23:06:19Z / 23:06:29Z UTC May 7, all 10s apart; 108 tick events between 22:48 UTC and 23:06 UTC (= exactly the expected 10s cadence over 18 minutes); `WatchdogLogger.LogTick` is called on every poll iteration in `WatchdogService.cs:391`. The watchdog process (PID 64796) shows `Responding: True`, 48s of CPU time over 2 days uptime — healthy. Most likely Dexter's `tail` sample hit a stale OS file-cache or buffer-flush moment. Not a defect.
- **RECOMMENDED ACTIONS list:** all five sound. #1 (install + watchdog restart) is the gating action — confirmed. #2 (re-run inquisition 24-48h post-install) is sound — PR3's instrumentation makes the 4-bucket count trivial. #3 (coding-standards rule + nudge against open-ended Bash polls) is filed as #0177. #4 (same-task self-review hygiene fix) is filed as #0178. #5 (defer all source-side fixes to PR1/PR2/PR3) is correct — no fourth source PR needed; the gap is deployment, not authoring.
- **Operational note for balazs:** the install + restart is one command pair. Until it runs, every metric on this report (and Brian's) is pre-fix. The strongest single signal at ruling time was that my own Charlie session's anchor (PID 77920) landed in the worktree, not main — i.e. the bug Dexter documented with his anchor (79260) is still happening on every fresh claim, mine included. That's the cleanest single-line proof that nothing has changed since Dexter sampled.

**Verdict summary:** 2 findings CONFIRMED (#0176 critical deployment gap, subsumes Finding #2); 2 deeper issues CONFIRMED and filed (#0177 medium poll-loop crash class, #0178 medium self-review gap); 1 deeper issue FALSE POSITIVE (watchdog log silence — direct verification shows healthy 10s ticks).

Strong on Finding #1: three independent triangulating signals (binary mtime, process StartTime, behavioural fingerprint of the running code). Strong on Finding #2: live filesystem inspection, anchor PIDs match live processes. Medium-high on the bucket (a) / (b) split: 14 + 5 = 19 sampled resume episodes, two-project replication, consistent ratio. Low on buckets (c) and (d): the pre-PR3 forensic signal is noisy and the question "did a manual recovery happen" requires correlation that PR3 was specifically built to bypass — exactly why PR3 should be installed.

What was NOT examined thoroughly:
- Live execution of a forced crash post-install. Out of scope: Adele's brief is investigation-only.
- Whether the LC project's separate watchdog has the same `claude.exe.old.<ts>` rename condition as DynaDocs's `anchor_name`. LC's last `start` event lists `anchor_name: "claude"` (not the renamed form), which suggests LC's claude has not been updated as recently — so #0151's specific trigger may be DynaDocs-only at this moment.
- Adele's currently-live 4d0d074b session was confirmed alive (sidecar last event `Read` at 22:50:12 UTC May 7 of an Adele inbox file) but not deeply traced. It is excluded from the bucket counts as a live session, not a crash.

## 2026-05-08 — Dexter

### Watchdog kill-heuristic investigation (the Heisenbug)

#### Scope

- **Entry point:** Adele's inbox brief `investigate-watchdog-killing-active-agents` (file `dydo/agents/Dexter/inbox/e43b49eb-investigate-watchdog-killing-active-agents.md`). Mandate: prove or disprove that a cleanup heuristic in dydo kills active agents when their worktree directory looks "stuck" or "in-use-but-stale."
- **Trigger observation:** balazs reported that judge Charlie's session output showed `WARNING: Could not remove directory ... investigate-agent-crashes-followup: ... being used by another process` immediately followed by `Worktree investigate-agent-crashes-followup: cleaned up.`, after which Charlie's session was cut and `claude --resume fd17b834-...` produced "No conversation found".
- **Defensive constraint:** identical to Brian/Dexter prior sections — no full-file Read of any audit JSON or `.events`; sampling via `head` / `tail` / `wc -l` / `grep`. No open-ended Bash poll-loops (`until [ … ]` / `while ! …`) — that is the unrelated crash mode flagged in #0177.
- **Versions in scope:** dydo source-tree HEAD `5bcbe0f` (post-PR3, plus PR3 docs commit). Per Dexter's earlier Finding #1 (#0176), the running on-disk binary is still pre-PR1; this investigation is conducted against the source code as-of-now and the watchdog log produced by the pre-fix running binary. Where the kill-heuristic question depends on which code is on the user's machine, both are addressed below.

#### Finding #1 — KILL-HEURISTIC: there is NO code path that escalates from "cannot remove worktree directory" to killing the holding process

- **Category:** root-cause investigation — verdict: red-herring (the suspect mechanism does not exist)
- **Severity:** n/a (the absence of a defect; the user-visible symptom has a different cause, addressed in Finding #3)
- **Type:** obvious — proven by direct reading of every kill site in the codebase plus audit-log forensics across 24+ days of running history
- **Evidence:**
  1. **The "cannot remove" warning is `WorktreeCommand.cs:821` in `RemoveZombieDirectory`. The catch block logs and falls through. No escalation:**
     ```
     // Commands/WorktreeCommand.cs:809-823
     internal static void RemoveZombieDirectory(string worktreePath)
     {
         if (!Directory.Exists(worktreePath)) return;
         try { DeleteDirectoryJunctionSafe(worktreePath); }
         catch (Exception ex)
         {
             Console.Error.WriteLine($"WARNING: Could not remove directory {worktreePath}: {ex.Message}");
         }
     }
     ```
     `DeleteDirectoryJunctionSafe` does not consult any process state (`WorktreeCommand.cs:775-794`); its only failure mode is `Directory.Delete` throwing `IOException` ("file in use"). The catch swallows the exception and emits the WARNING. **No process is enumerated, no PID is killed, no taskkill / Process.Kill / SIGKILL is invoked from this catch.**
  2. **`TeardownWorktree` (the WARNING's caller, `WorktreeCommand.cs:737-744`) is invoked from exactly three sites, none of which kill anything:**
     - `ExecuteCleanup` (line 331) — invoked from the dispatch tab's PowerShell `finally` block when claude has already exited. Gated above (line 317) by `CountWorktreeReferences == 0`, i.e., NO other agent still holds the worktree.
     - `FinalizeMerge` (line 966) — explicit user-initiated `dydo worktree merge --finalize`. Same `CountWorktreeReferences == 0` gate (line 964).
     - `ExecutePrune` (line 1020) — `dydo worktree prune` orphan sweep. Same `CountWorktreeReferences > 0 → skip` gate (line 1011-1015) AND a "stranded watchdog.pid" pre-check (`ReportStrandedWatchdogPid`, line 1077-1087). The pre-check warns but does not kill: a live PID is reported to stderr ("WARNING: Stranded watchdog.pid ... still ALIVE — investigate before relying on prune") and the subsequent `TeardownWorktree` runs unchanged. Crucially, the WARNING text from prune is different from the user-reported text and was not the trigger here.
  3. **Every `Process.Kill` call in the codebase, exhaustively (rg `Process\.Kill|TerminateProcess|taskkill` across `Services/`, `Commands/`, `Utils/`, `Models/`):**
     - `Services/ProcessUtils.cs:172` inside `RunProcess` — kills a hung *child* sub-process (git, claude --version, etc.) that exceeds the helper's own timeout. The target is dydo's own short-lived helper, never a user-facing claude/dydo agent process.
     - `Services/WatchdogService.cs:768-790 KillClaudeProcesses` — gated by the caller `PollAndCleanupForAgent` (line 410): `if (!autoClose || !isFree || agentName == null) return 0;` — only fires on agents with `auto-close: true && status: free`. Working agents do not satisfy `isFree`. Whitelist further restricts to PIDs whose process name passes `MatchesProcessName("claude") || MatchesProcessName("node")` (line 778-780), so it cannot kill terminal emulators or unrelated processes carrying the prompt in argv (closes #0122).
     - `Services/WatchdogService.cs:894 PollOrphanedWaits → proc.Kill()` — gated `if (!isFree) continue;` (line 880), targets the *dydo wait* helper PID inside `.waiting/<file>.json` (line 891-894), never claude.
     - `Services/DispatchService.cs:698 proc.Kill()` inside `RunGitForWorktree` — same hung-helper pattern as `ProcessUtils.RunProcess`, kills git only when it exceeds the timeout. Internal helper, not a user agent.
     - **There is no other `Process.Kill` / `taskkill` / `TerminateProcess` shell-out anywhere in the codebase.** Verified via rg with the union pattern across `Services/`, `Commands/`, `Utils/`, `Models/`.
  4. **The worktree-locked-warning path and the kill paths are not connected.** `RemoveZombieDirectory`'s catch (the only place the user-visible WARNING line is emitted) does not call `KillClaudeProcesses`, does not enumerate processes, does not even read agent state. It writes one stderr line and returns. `TeardownWorktree`'s remaining steps after the WARNING are `RemoveGitWorktree` (`git worktree remove --force`, line 796-807) — which can fail again silently (its own catch, line 803-806) — and the loop returns to `ExecuteCleanup` which then calls `DeleteWorktreeBranch` (`git branch -D worktree/<id>`) and prints `"Worktree X: cleaned up."`. **Nothing in this sequence reads PIDs, consults `IsProcessRunning`, or invokes `KillClaudeProcesses`.** The Console output of "cleaned up." is misleading on Windows when the directory delete failed (the directory is in fact still on disk), but it is a log-correctness issue, not a kill.
  5. **PR2's RegisterMainAnchor changes do NOT introduce a new cleanup path.** Verified by reading `Services/WatchdogService.cs:195-228` (RegisterMainAnchor → FindMainDydoRoot → RegisterAnchor) and `Services/AgentRegistry.cs:431` (call site on agent-claim). The new code only WRITES an anchor file. There is no removal logic associated with it; anchor files are removed naturally when the watchdog observes the holding PID is dead during its `LiveAnchorCount` walk (`WatchdogService.cs:305-326`), which already existed pre-PR2. PR2 changed only WHERE anchors are written, not anything about cleanup or kills.
  6. **Audit-log forensics — 115 watchdog kills across 24+ days, ALL gated on `status:"free"`:**
     - `grep -cE '"event":"kill"' watchdog.log{,.1}` = `62 + 53 = 115` total kill events.
     - `grep -E '"event":"kill"' watchdog.log{,.1} | grep -v '"status":"free"' | wc -l` = **0**. Every single recorded kill carries `state.status: "free"`, `state.auto_close: true`. The auto-close gate has held in production for the entire watchdog-log era (2026-04-29 → 2026-05-07).
     - This is the strongest single signal: even if a code path *could* hypothetically kill an active agent, in practice it never has.
- **Why this matters:** the brief's hypothesis ("the watchdog has a cleanup heuristic that occasionally kills active agents when their worktree looks stuck or in-use-but-stale") is not supported by the code or by the production data. The mechanism the user inferred from co-occurrence does not exist.
- **Files examined:**
  - `Commands/WorktreeCommand.cs:284-335 (ExecuteCleanup), 395-435 (CountWorktreeReferences/CountChildWorktrees), 700-823 (Junction/Zombie/GitWorktree teardown), 932-989 (FinalizeMerge), 998-1029 (ExecutePrune), 1077-1087 (ReportStrandedWatchdogPid)`
  - `Services/WatchdogService.cs:300-440 (Run loop, PollAndCleanup, PollAndCleanupForAgent), 442-555 (PollAndResumeForAgent), 760-790 (KillClaudeProcesses), 820-910 (PollQueues, PollOrphanedWaits)`
  - `Services/AgentRegistry.cs:415-435 (RegisterAnchor call site)`; `Services/ProcessUtils.cs:36-58 (GetProcessName), 130-183 (RunProcess + helper Kill)`; `Services/ProcessUtils.Ancestry.cs:33-98 (FindClaudeAncestor + MatchesProcessName)`; `Services/DispatchService.cs:644-702 (RunInitSettings, CreateGitWorktree, RunGitForWorktree)`.
  - Audit logs: `dydo/_system/.local/watchdog.log` and `watchdog.log.1` (115 kill events sampled by `grep -E '"event":"kill"'`); `dydo/_system/audit/2026/2026-05-07-fd17b834-85dd-47d9-8713-da618d840467.json` (Charlie's metadata).
- **Independent verification:** Walked every `Process.Kill` site (4 total) by reading the surrounding 30-line context to confirm gating and target. Walked the WARNING-emit catch block to confirm fall-through. Cross-checked the watchdog log: ALL 115 historical kills have `state.status:"free"` — verified by an inverted grep (`grep -v '"status":"free"'`) returning zero lines. Counter-checked at the source level: `PollAndCleanupForAgent` returns immediately (line 410) when `!autoClose || !isFree || agentName == null`, so a working agent's `KillClaudeProcesses` cannot be reached.
- **Alternative explanations considered:**
  1. *"Could `git worktree remove --force` itself terminate a holding process?"* — No. `git worktree remove` is a metadata operation plus a recursive directory delete. Git does not enumerate or kill PIDs holding files. On Windows, when a file is locked, git's removal of THAT FILE silently fails (just like ours); git proceeds to remove what it can and updates `.git/worktrees/<id>/` admin state. No process termination.
  2. *"Could the OS kill the holding process when its worktree dir is removed?"* — No. Windows' filesystem semantics keep handles open even when the parent directory is unlinked (or, more relevant here, refuse to unlink at all). A process holding open handles in a directory that another process tried to delete continues to run; it merely sees subsequent path-based reads fail. The OS does not signal/kill the holder.
  3. *"Could the resume launcher kill the live agent during the worktree wrapper?"* — No. `LaunchResumeTerminal` (`Services/TerminalLauncher.cs` and platform-specific launchers) only spawns a NEW terminal; it does not enumerate or kill any existing PID. Verified by reading `Services/WindowsTerminalLauncher.cs:75-127` and the Linux/Mac analogues.
  4. *"Could `RemoveJunction` (`WorktreeCommand.cs:746-766`) cause a holding claude to crash by stripping the junction it has open?"* — Possibly degrade Charlie's subsequent reads (the dydo guard would fail to read `dydo/agents/...` if those junctions vanished), but stripping a junction does not terminate a process holding a path inside it. The next read on a stripped junction returns ENOENT/`FileNotFoundException`; claude's hook fails and either retries or surfaces an error. Not a kill, and not a "stuck worktree" heuristic — the junctions are stripped unconditionally on `TeardownWorktree`, only after the `CountWorktreeReferences == 0` gate has passed.
  5. *"Could the pre-fix binary (the one actually running on balazs's machine, per #0176) have a different kill path that the post-PR3 source no longer has?"* — Verified by `git log -p -- Services/WatchdogService.cs Commands/WorktreeCommand.cs` between v1.4.0 (a06db96, 2026-04-30) and HEAD: no `Process.Kill` call has been added or removed since v1.4.0; the auto-close gate's `isFree` predicate has not been weakened (if anything it has been hardened by #0121 fixes); and `KillClaudeProcesses`'s name-whitelist has only been tightened (claude/node), never broadened to "anything matching the substring." Pre-fix behaviour is the same with respect to active-agent kills.
- **Issue:** none — no defect found on the kill-heuristic question. (See Finding #2 for an adjacent log-correctness defect that is real.)
- **Judge ruling (Emma, 2026-05-08):** CONFIRMED — verdict NO kill-heuristic exists.
- **Files examined:** `Commands/WorktreeCommand.cs:284-336 (ExecuteCleanup), 737-744 (TeardownWorktree), 746-766 (RemoveJunction), 775-794 (DeleteDirectoryJunctionSafe), 796-807 (RemoveGitWorktree), 809-823 (RemoveZombieDirectory)`; `Services/WatchdogService.cs:260-293 (Stop), 394-440 (PollAndCleanupForAgent), 760-790 (KillClaudeProcesses), 869-909 (PollOrphanedWaits)`; `Services/ProcessUtils.cs:120-184 (RunProcessCapture, kill on timeout); Services/DispatchService.cs:680-702 (RunGitForWorktree, kill on timeout)`; both watchdog logs.
- **Independent verification:**
  1. Walked all 7 `Process.Kill` / `proc.Kill` sites in production code via grep `Process\.Kill|TerminateProcess|taskkill|proc\.Kill\(\)` across `*.cs`. The inquisitor enumerated 4 — I additionally checked `WatchdogService.cs:284` (the `Stop()` self-kill of the watchdog PID itself, irrelevant to agent kills) and the two test sites. Conclusion holds: no kill site is reachable from any worktree-cleanup path or from a `status != free` agent.
  2. Re-grepped the watchdog logs: `grep -cE '"event":"kill"' watchdog.log{,.1}` = 63 + 53 = 116 (one new since the inquisitor's count of 115). Inverted check `grep -E '"event":"kill"' ... | grep -v '"status":"free"' | wc -l` = **0**. The auto-close gate has held 116/116 times.
  3. Confirmed the WARNING line is sourced from exactly one location: `grep "Could not remove directory" *.cs` returns only `WorktreeCommand.cs:821`. No other code path emits this string.
  4. Confirmed `git log v1.4.0..HEAD -p -- Services/WatchdogService.cs` shows zero added/removed `Process.Kill` lines.
- **Alternative explanations considered:** the inquisitor's 5 alternatives are exhaustive and individually verified (git-worktree-remove cannot kill PIDs; OS does not signal holders on directory unlink; resume launcher only spawns; junction strip degrades reads but does not terminate; pre-fix binary shares the same kill paths). I add: the `Stop()` self-kill at `WatchdogService.cs:284` only targets the watchdog's own PID file and is invoked by `dydo watchdog stop` — not from any cleanup path, and not against agent processes.

#### Finding #2 (NEW) — `ExecuteCleanup` prints `"Worktree X: cleaned up."` even when `RemoveZombieDirectory` failed to delete the directory

- **Category:** log-correctness / observability defect (contributing factor — fed the user's incorrect inference)
- **Severity:** low-medium
- **Type:** obvious (direct code reading)
- **Evidence:**
  - `Commands/WorktreeCommand.cs:331-335`:
    ```
    TeardownWorktree(worktreePath, mainRoot);
    DeleteWorktreeBranch(worktreeId, mainRoot);
    Console.WriteLine($"Worktree {worktreeId}: cleaned up.");
    ```
  - `TeardownWorktree` (line 737-744) calls `RemoveZombieDirectory` (which can fail with the WARNING and continue), then `RemoveGitWorktree` (which itself swallows exceptions, line 803-806). After both steps, control returns even if NEITHER step actually removed anything. The unconditional success log line then fires.
  - **Result on Windows when the directory is locked by another process:** stderr emits `WARNING: Could not remove directory ...`, stdout emits `Worktree ...: cleaned up.`, and the directory is in fact still on disk. The two lines together read as "cleanup completed despite warning," which is exactly the inference balazs drew — but on Windows file-locking semantics the WARNING is the truth and the "cleaned up." line is the lie.
- **Why this matters:** balazs reasoned from "WARNING + cleaned up" to "the cleanup escalated past the lock". That inference is plausible given the misleading log, and it's the seed of the kill-heuristic hypothesis. Fixing the log line to track real outcome closes the inference.
- **Proposed fix path:** thread a return value through `TeardownWorktree → RemoveZombieDirectory → DeleteDirectoryJunctionSafe` indicating success/partial/failure, and let `ExecuteCleanup` log one of three messages: "Worktree X: cleaned up.", "Worktree X: partially cleaned up (directory still in use — will retry on next prune).", or "Worktree X: cleanup failed.". Same change for `FinalizeMerge` (line 982-988) and `ExecutePrune` (line 1018-1023). The implementation is small (one bool return per helper, one branch per caller).
- **Files examined:** `Commands/WorktreeCommand.cs:284-335, 700-823, 932-989, 998-1029`.
- **Independent verification:** Reproduced the misleading-log scenario by reading the call chain. The catch in `RemoveZombieDirectory` does not propagate the failure to `TeardownWorktree`, which has no return value, so `ExecuteCleanup` cannot know teardown was incomplete.
- **Alternative explanations considered:** *"Could the success-log be intentional because the failure is expected on Windows and not worth surfacing?"* — partially: prior commits (`dydo/project/changelog/2026/2026-04-26/worktree-cleanup-hardening.md` etc.) document the Windows-locking case as a known and tolerated condition. But once a WARNING is being emitted to stderr (and balazs is reading those warnings), the success line on stdout creates an actively confusing pair. The current behaviour is not a deliberate trade-off — it's a silent partial-failure that the log shape doesn't capture.
- **Issue:** new, file as a low-medium hygiene issue (not a crash fix, but it directly fed the kill-heuristic misdiagnosis).
- **Judge ruling (Emma, 2026-05-08):** CONFIRMED — log-correctness defect is real.
- **Files examined:** `Commands/WorktreeCommand.cs:284-336 (ExecuteCleanup), 737-744 (TeardownWorktree, void return — load-bearing for the bug), 796-807 (RemoveGitWorktree, also swallows), 809-823 (RemoveZombieDirectory, also swallows)`.
- **Independent verification:** Read the full call chain top-to-bottom. Confirmed `TeardownWorktree` returns `void` at line 737 — a return-status thread is therefore the natural fix. Confirmed both intermediate steps swallow exceptions independently (`RemoveZombieDirectory` line 819-822, `RemoveGitWorktree` line 803-806), so even with two independent failure modes the unconditional `Console.WriteLine("…cleaned up.")` at line 334 cannot reflect ground truth. Cross-checked `FinalizeMerge` and `ExecutePrune` callers — same shape, same defect.
- **Alternative explanations considered:** the inquisitor considered "is this an intentional silent-tolerance pattern documented in the changelog?" — partial yes for the WARNING-only path on Windows file-locks, but the success-log emission alongside the WARNING creates an actively confusing pair, not a deliberate trade-off. I confirm: the proposed fix (thread a return value through the chain) is small, surgical, and aligns with the Anti-Slop Mandate (no new abstraction; one bool/enum return per helper, one branch per caller).
- **Issue:** #0179

- **Category:** observation that refutes the trigger correlation
- **Severity:** n/a
- **Type:** obvious (direct audit-file inspection)
- **Evidence:**
  - `dydo/_system/audit/2026/2026-05-07-fd17b834-85dd-47d9-8713-da618d840467.json` contains exactly one event:
    ```
    started: 2026-05-07T23:01:41.4918391Z
    git_head: 036b88c
    events: [{ ts: "2026-05-07T23:01:41.4906965Z", event: "Read", path: "C:/Users/User/Desktop/Projects/DynaDocs/dydo/index.md", tool: "read" }]
    ```
  - **No `.events` sidecar exists for fd17b834.** `find dydo/_system/audit/2026/ -name 'fd17b834*'` returns only the JSON metadata file. The session never made a second tool call — not even an agent claim.
  - `started` (23:01:41) is consistent with a fresh `claude <agent> --inbox` launch reading the bootstrap onboarding file `dydo/index.md`. The session terminated before reaching `dydo agent claim`.
  - **Watchdog log around that timestamp shows NO kill for fd17b834:**
    - `2026-05-07T22:50:07Z` — kill Charlie PID 56156 / 70072 (a previous Charlie session), `status:"free", auto_close:true`. ~12 minutes BEFORE fd17b834 started.
    - `2026-05-07T23:01:48Z` — kill Dexter PID 79260, `status:"free", auto_close:true`. ~7 seconds AFTER fd17b834's only event, but for a different agent (Dexter, not Charlie). Causally unrelated.
  - **No `state.status: "working"` kill ever appears in the watchdog log** (per Finding #1's bulk grep). Even if the watchdog had wanted to kill fd17b834, the gate would have stopped it: a freshly-started Charlie session that has not yet claimed an identity has no agent state file to flip `status:"free"`.
  - **The session's death is consistent with the "Brian-session-class" crash pattern documented in Brian's #4 / Dexter's prior section:** a fresh claude launch with a heavy initial prompt (the inquisition brief) OOMs or otherwise terminates internally before the first claim. The `claude --resume fd17b834-...` "No conversation found" failure that followed is the downstream Claude-Code transcript-lookup symptom — Claude only persists transcripts after meaningful interaction; a session that died after one Read may not have been written to `~/.claude/projects/<encoded-cwd>/<session>.jsonl` at all.
- **The trigger correlation is post-hoc:** the WARNING line balazs observed about `investigate-agent-crashes-followup` was emitted by SOME `dydo worktree cleanup` invocation (Dexter's PowerShell `finally` after Dexter's release, or a separate dispatcher's `finally`, or a user-typed `dydo worktree prune`). Its co-occurrence with Charlie's death in the user's perception is real timeline-wise, but causally Charlie crashed during onboarding for an unrelated Claude-Code-internal reason, NOT because the cleanup escalated.
- **Independent verification:** `cat dydo/_system/audit/2026/2026-05-07-fd17b834-...json | head -50` shows the single event; `find ... -name 'fd17b834*'` confirms no sidecar; `grep -E '"agent":"Charlie".*"target_pid":' watchdog.log{,.1}` shows the only Charlie kills in the window are the 22:50 ones (a prior session) — none for fd17b834.
- **Alternative explanations considered:**
  1. *"Could the events sidecar have been deleted by the worktree teardown that was running concurrently?"* — No: `AuditService` writes sidecars to `dydo/_system/audit/2026/<sessionid>.events`. That path lives in MAIN's audit dir, not the worktree's. Per `architecture.md` §"Worktree Dispatch", the four junctioned paths do NOT include `dydo/_system/audit/`. (Audit files inside a worktree are explicitly preserved by `PreserveAuditFiles` before teardown — `Commands/WorktreeCommand.cs:338-377` — and copied to main, then deletion runs only on the worktree-local copies.) So an absent sidecar means it was never created, not that it was deleted.
  2. *"Could the trigger WARNING have come from a kill that was logged to a different file?"* — No: there is no kill-side log other than `watchdog.log`, and no path emits the WARNING via any logger outside `Console.Error.WriteLine` at `WorktreeCommand.cs:821`. Verified by grep of `Could not remove directory` (only that one match) and grep of all Process.Kill sites.
- **Issue:** none — observation, not defect.
- **Judge ruling (Emma, 2026-05-08):** CONFIRMED — Charlie's `fd17b834` did not die from a watchdog kill or a cleanup escalation.
- **Files examined:** `dydo/_system/audit/2026/2026-05-07-fd17b834-85dd-47d9-8713-da618d840467.json` (full file — 14 lines, single Read event); both watchdog logs.
- **Independent verification:** Read the audit JSON directly. Confirmed exactly one event (Read of `dydo/index.md`) at 23:01:41Z, and no `.events` sidecar. Grepped both watchdog logs for `fd17b834`: returns two events, BOTH `event:"resume"` and `event:"resume_blocked"` (at 23:16:20Z and 23:17:20Z respectively, after the death) — NEITHER is a `kill`. (Side note: the inquisitor's evidence-block does not mention the resume/resume_blocked entries, which is a minor omission — but it does NOT change the verdict, since the existence of resume attempts only reinforces that the watchdog observed a dead PID and tried to recover, never that it caused the death.)
- **Alternative explanations considered:** the inquisitor's two alternatives are correctly addressed (sidecar absence is "never created" not "deleted"; no second logger emits the WARNING). I add: the resume attempt at 23:16:20Z post-dates the trigger WARNING by ≥14 minutes, ruling out any "the resume launcher killed Charlie" hypothesis as well.

#### Finding #4 — Pattern match across recent crashed sessions: the "worktree cleanup → death" co-occurrence appears only in the user's mental model, not in the audit data

- **Category:** observation
- **Severity:** n/a
- **Type:** obvious (audit-tail sampling across all recent crashes)
- **Evidence:** sampled all `.events` sidecars modified in the last 2 days (n=34) plus fd17b834. Of 35 sessions:
  - **33 ended in a `Release` event** — clean shutdowns. Not crashes.
  - **`4090052a-7b28-491b-b0e7-8e1266ca1fb8`** (Charlie, May 7 15:43 UTC) — last event is `Bash` with cmd `until [ -s /tmp/claude/C--Users-User-Desktop-Projects-DynaDocs/4090052a-.../...`. The open-ended bash poll-loop crash documented in #0177. **No worktree-cleanup operation in the watchdog log within ±5 minutes of 15:43:23Z.** (Closest events: a kill for Dexter/19624 at 14:24:06Z and a kill for Brian/55564 at 15:24:10Z, both unrelated.)
  - **`fd17b834-85dd-47d9-8713-da618d840467`** (Charlie, May 7 23:01 UTC) — JSON-only with 1 Read of index.md. See Finding #3. **No worktree-cleanup operation in the watchdog log within ±5 minutes** — and as established, the watchdog log doesn't directly record worktree cleanups; cleanups are logged only to terminal stdout/stderr. Console output is not the watchdog log.
  - **`4d0d074b-...`** is Adele's currently-live session — excluded as live, not crashed.
- **Conclusion:** out of 2 confirmed crashes in the recent window, ZERO are temporally correlated with a worktree-cleanup operation in any way the audit data can substantiate. The user's "right after the WARNING, Charlie died" observation is consistent with two independent events whose ordering is real but whose causal link does not exist.
- **Brian's earlier "Brian f9936e33" / "Charlie 8b52b181" / "Frank 04d1191f" / etc. were NOT crashes** — they all reached `Release` (the prior section's table at lines 421-422 lists these as bucket-(a) successes). The narrative of "many crashes today" overstates the crash count; most of those entries are auto-resume successes that the watchdog log mislabelled as `resume_blocked` (Brian's #1 / #0173).
- **Independent verification:** Per-file `tail -1` over the 34 recent sidecars shows the breakdown above; `find ... -mtime -2` enumeration captured all in-window sidecars; the JSON metadata for fd17b834 was inspected directly.
- **Issue:** none — observation.
- **Judge ruling (Emma, 2026-05-08):** CONFIRMED — the cleanup→death correlation is not present in the audit data.
- **Files examined:** both watchdog logs; `dydo/_system/audit/2026/2026-05-07-fd17b834-…json`.
- **Independent verification:** Re-ran the inverted-kill grep across both logs (`grep -E '"event":"kill"' watchdog.log{,.1} | grep -v '"status":"free"' | wc -l` → 0). Confirmed the inquisitor's pattern-match holds at the bulk level: zero kills with non-free status across the entire 116-event history. Sample size for confirmed crashes (n=2) is small but the verdict is structural, not statistical — additional crashes can only fail to correlate, since no `status != free` kill has ever been logged.
- **Alternative explanations considered:** "could a worktree cleanup that ran but emitted no watchdog-log signal still have been the trigger?" — possible in principle (cleanups go to terminal stdout/stderr, not watchdog.log), but for THAT pathway to have killed Charlie there would still need to be a code path from `RemoveZombieDirectory`'s catch to a process termination. There is none (Finding #1).

#### Finding #5 — Pre-fix vs post-fix: kill-heuristic question has the same answer in both directions

- **Category:** deployment-gap confounder analysis (per the brief's section 5)
- **Severity:** n/a
- **Type:** observation
- **Evidence:** the brief asks whether the suspect heuristic exists pre-fix, post-fix, or both. Verified by reading both the running-binary's behaviour fingerprint and the source diff:
  - **Source-side (post-PR3 HEAD `5bcbe0f`):** no kill path exists from worktree cleanup. Per Finding #1.
  - **Running-binary side (pre-PR1, per #0176):** the kill-side code in `Services/WatchdogService.cs:KillClaudeProcesses` and the auto-close gate in `PollAndCleanupForAgent` have not changed since v1.4.0 (verified by `git log -p` over those regions for the v1.4.0..HEAD range). The pre-fix binary and the post-PR3 source share the same kill paths and the same `isFree` gate.
  - **Audit-log side:** 115 historical kills, all `status:"free"`. The pre-fix binary that produced those logs has never killed a working agent.
- **Conclusion:** pre-fix and post-fix both lack a kill-heuristic that escalates from "cannot remove directory" to terminating the holding process. The deployment gap (#0176) does not change the answer to this brief's question.
- **Issue:** none — answers section 5 of the brief.
- **Judge ruling (Emma, 2026-05-08):** CONFIRMED — no kill-side regression v1.4.0..HEAD.
- **Files examined:** `Services/WatchdogService.cs` and `Commands/WorktreeCommand.cs` diff over `v1.4.0..HEAD`.
- **Independent verification:** Re-ran `git log -p v1.4.0..HEAD -- Services/WatchdogService.cs Commands/WorktreeCommand.cs | grep -E '^[+-].*Process\.Kill|proc\.Kill|TerminateProcess|taskkill'` — returned zero lines. The kill-side has neither been added to nor removed from since the v1.4.0 baseline. The auto-close gate predicate `if (!autoClose || !isFree || agentName == null) return 0;` (`PollAndCleanupForAgent` line 410) is the same shape pre- and post-fix.
- **Alternative explanations considered:** N/A — verdict is symmetric across the deployment gap.

### KILL-HEURISTIC VERDICT

**No.** There is no code path that kills active processes during worktree cleanup. The hypothesis is refuted by three independent lines of evidence:

1. **Source code:** The only `Process.Kill` site that targets agent claude PIDs (`WatchdogService.KillClaudeProcesses`) is gated on `auto-close: true && status: "free"` and reachable only from `PollAndCleanupForAgent`'s auto-close path — never from any worktree-cleanup path. The `RemoveZombieDirectory` warning catch does not enumerate or terminate any process.
2. **Production log history:** 115 watchdog kill events across 24+ days; ALL carry `state.status:"free"`. No active-agent kill has ever occurred.
3. **Charlie's specific death:** Charlie session `fd17b834` died during onboarding (1 event ever, no sidecar) for a Claude-Code-internal reason (consistent with the documented OOM-during-onboarding pattern in Brian's #4). The watchdog log shows no kill for that session and no worktree-cleanup escalation correlates with the death timestamp in any retrievable audit data.

The trigger observation (WARNING + "cleaned up." + Charlie's death) is two independent events whose temporal proximity in the user's perception is misleading. The "cleaned up." log line on stdout is itself misleading on Windows when `RemoveZombieDirectory` failed (Finding #2 — log-correctness defect, fix queued), and that misleading log is what made the kill-heuristic hypothesis plausible.

### IF YES (does not apply)

n/a — verdict is No. For completeness: if a kill-heuristic ever did need to be added in the future, the safe pattern is the auto-close gate already used by `PollAndCleanupForAgent` (line 410) — never kill a PID whose owning agent is in a non-`free` status, and always whitelist by process name to avoid terminal-emulator collateral damage (closes #0122).

### PATTERN MATCH

Of today's known abrupt-end sessions sampled across the audit-trace window (last 2 days, n=34 sidecars + fd17b834):

- **Crashes that correlate temporally with a worktree-cleanup operation in the audit data:** **0 of 2.**
- **Crashes attributable to the documented bash-poll-loop pattern (#0177):** 1 of 2 (`4090052a`).
- **Crashes attributable to the documented onboarding/rehydration OOM pattern (Brian's #4 / Dexter's prior #1):** 1 of 2 (`fd17b834` — onboarding).
- **Crashes attributable to a watchdog kill of an active agent:** **0 of 2** (and 0 of all 115 historical kills).

The "watchdog cleanup kills active agents" pattern is **not corroborated** anywhere in the audit data.

### RECOMMENDED IMMEDIATE ACTION

**For balazs, in priority order:**

1. **The kill-heuristic hypothesis is refuted. Do NOT take any "stop the watchdog from killing me" defensive action.** The watchdog's auto-close kill path is gated correctly, has held for 24+ days in production with zero false positives, and has not regressed in any change since v1.4.0. Disabling or weakening the watchdog would create real holes (orphan claude tabs accumulating, auto-close failures) without solving any actual problem.
2. **Install the post-PR3 binary (per Dexter's earlier #0176, still pending).** This remains the single most-impactful action available — it closes the `resume_blocked: no_refresh_after_warmup` log dishonesty, restores PR2's worktree-anchor placement, and turns on PR3's `resume_outcome` instrumentation. The kill-heuristic question does not depend on this install, but the user's broader "auto-resume looks broken" symptom does.
3. **File the log-correctness defect from Finding #2** (cleanup prints "cleaned up." even when the directory delete failed). This is the contributing factor that made the kill-heuristic hypothesis plausible. A judge will dispatch separately and create the issue if the finding rules CONFIRMED.
4. **For the Charlie-class onboarding crash (Brian's #4 / Dexter's prior #1):** no new fix proposed here. The mitigation is the same as before — keep dispatch briefs as small as possible without losing essential context, and avoid open-ended Bash polls (#0177).
5. **Do NOT add a watchdog escalation that kills processes holding worktree directories.** Even if such a heuristic were "wanted" as a cleanup convenience, it would cross the safety boundary the existing kill paths are carefully gated against. The right cleanup pattern when a directory is locked is the current one: emit a warning, defer to the next prune cycle, let the holder release naturally.

### Confidence

- **High** on Finding #1 (kill-heuristic verdict): triangulated across source-side, audit-log, and per-session forensics. Three independent signals all point to "no kill heuristic exists." If this verdict is wrong, all three would need to be wrong simultaneously, which is unlikely.
- **High** on Finding #2 (log-correctness defect): proven by direct code reading; the success-log unconditionality is one Console.WriteLine call away from the failure-tolerant catch.
- **High** on Finding #3 (fd17b834's onboarding death): direct inspection of the audit JSON; only 1 event. Consistent with the previously-documented Brian-session class.
- **Medium-high** on Finding #4 (pattern match): n=2 confirmed crashes is small. But the verdict is robust to expanded sampling — adding more crashes can only weaken the correlation, not invent one that the data doesn't show.
- **High** on Finding #5 (pre-fix/post-fix): `git log -p` confirms no kill-side regression across the v1.4.0..HEAD range.

### What was NOT examined thoroughly

- **Live forced-crash repro:** intentionally avoided. Adele's brief is investigation-only, and triggering an actual claude crash on this machine would risk the very crash mode under investigation (Brian-session-class onboarding OOM).
- **Linux/Mac equivalent of `RemoveZombieDirectory`:** The path is `Services/LinuxTerminalLauncher.cs` for the resume/cleanup wrapper plus the same `WorktreeCommand.cs` helpers. The C# helpers are platform-agnostic above the cmd-vs-rm split (`RemoveJunction` line 750-766 forks on `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)`; `DeleteDirectoryJunctionSafe` is portable). The ReparsePoint-based junction handling is Windows-only, but symlinks on Linux/Mac are treated by `Directory.Delete(recursive: false)` similarly. No Linux-specific kill escalation was found via grep of `*.cs`.
- **Adele's currently-live `4d0d074b` session:** confirmed alive, not investigated further. Excluded from the bucket counts as not a crash.

### Judge summary (Emma, 2026-05-08)

5 findings reviewed. **5 CONFIRMED, 0 false positives, 0 inconclusive.** Issues filed: **#0179** (Finding #2, low-severity log-correctness defect). Findings #1, #3, #4, #5 are observations / red-herrings as the inquisitor classified them — no issues warranted. The kill-heuristic verdict (NO) holds: triangulated across source-code walk (every `Process.Kill` site enumerated and gated), production-log forensics (116/116 historical kills carry `status:"free"`, zero exceptions), and Charlie's `fd17b834` audit (1 event, no sidecar, no kill in watchdog log). The misleading `"Worktree X: cleaned up."` log line on Windows file-locks is the contributing factor that made the kill-heuristic hypothesis look plausible to balazs — fixing it (issue #0179) closes the inference path.

### Judge addendum (Emma, 2026-05-08) — corrections to the inquisitor's framing and open questions left for follow-up

After the user pushed back on the verdict ("if it wasn't the watchdog, what crashed Charlie?"), I re-examined the audit data and found the inquisitor's framing was too narrow on several dimensions. The kill-heuristic verdict is unaffected, but the surrounding narrative needs correcting.

**1. Per-session disentanglement — the "Charlie" identity is not a continuous existence.**

The Charlie identity is an access card; each `dydo agent claim Charlie` is picked up by a fresh, independent claude.exe process. Today, **four different claude processes** picked up Charlie and the inquisitor's pattern-match conflated them:

| Session id | Outcome | Watchdog log says |
|-----------|---------|------------------|
| `5dbe5e3c` | clean `Release` at 14:38 (~1h work) | spurious `resume_blocked` at 13:40 |
| `4090052a` | hung on `until [ -s /tmp/claude/… ]` poll-loop at 15:43 | `resume_blocked` at 15:46 (true crash; #0177) |
| `1f878107` | clean `Release` at 17:38 (~70 min work) | spurious `resume_blocked` at 16:32 |
| `fd17b834` | died after one Read of `dydo/index.md` | `resume_blocked` at 23:17 (true crash) |

**Two of four `resume_blocked` entries today are false positives on live sessions** — i.e. the watchdog mistakenly declared an alive claude dead, launched a doomed `claude --resume`, and logged `no_refresh_after_warmup` when the resume terminal failed. This is **#0173 firing in production right now**, and it directly pollutes the watchdog log that the inquisitor relied on as ground truth.

**2. The causal arrow on the WARNING was inverted.**

The inquisitor framed the WARNING (`Could not remove directory ... investigate-agent-crashes-followup`) and Charlie's death as "two independent events whose temporal proximity is misleading." On reconsideration, the natural read is the opposite — they ARE causally related, but the arrow points the other way:

1. claude.exe (PID 77920, session `fd17b834`) dies for an unknown reason after only one Read event.
2. The PowerShell wrapper's `finally` block (or a separate cleanup invocation) runs `dydo worktree cleanup investigate-agent-crashes-followup`.
3. `RemoveZombieDirectory` hits a Windows file-lock — a handle into the worktree was still open (most likely the just-died claude's un-flushed handles, or a stale junction target).
4. WARNING emitted to stderr; misleading `"cleaned up."` lies on stdout (issue #0179).

The cleanup is the **post-mortem of the corpse**, not the executioner. The kill-heuristic verdict is still correct (no `Process.Kill` exists in the cleanup path), but "the WARNING is unrelated to Charlie's death" was wrong. They are related — the WARNING is how the wrapper reacted to the death.

**3. The 14-minute gap is itself a clue.**

Between fd17b834's only Read event (23:01:41) and the watchdog's resume launch (23:16:20) there is a 14-minute silence. The watchdog polls every 10s, so this gap means either:
- claude was alive but silent for 14 minutes (uncharacteristic of a session that only got through 1 Read), OR
- the watchdog's dead-PID detection took 14 minutes to fire — consistent with **#0151** (Windows watchdog never registers anchors; orphan-cap is the only check), which would substantially delay dead-PID recognition.

Either way the gap is informative and the inquisitor did not flag it.

**4. The actual cause of death for fd17b834 is still unknown.**

The inquisitor inferred "Brian-session-class onboarding OOM" by elimination. The elimination was sloppy in two ways:
- **No OS-level evidence was checked.** Windows Event Viewer (Application log, `Application Error` and `.NET Runtime` providers) would have crash dumps if claude.exe died from an unhandled exception or OOM. Neither the inquisitor nor I checked.
- **No Claude Code log inspection.** `~/.claude/logs/` and `~/.claude/projects/<encoded-cwd>/` may carry per-session diagnostic info that survives even when the audit sidecar wasn't written.
- **No external corroboration.** The user mentions hearing about similar Claude Code crashes on Reddit; if there's a known upstream issue (resolved or unresolved), it might explain fd17b834 without any dydo-side defect at all.

**5. The pattern across recent crashes is real.**

The issues directory shows **at least 7 distinct, currently-open crash/resume failure modes** filed in the last few weeks: #0149 (wait-rearm flood deadlock), #0150 (auto-resume sometimes fails to trigger), #0151 (Windows anchor coverage gap), #0152 (auto-resume race / duplicate launches), #0153 (resume-attempts not reset on same-session reclaim), #0154 (Linux/Mac watchdog dies via anchor-gone), #0173 (resume false-positives), #0177 (bash poll-loop crash). Today's Charlie deaths are consistent with several of these firing simultaneously, not a single mechanism. The inquisitor's narrow scope ("does a kill-heuristic exist?") was answered correctly but did not address the broader question the user actually wanted answered — *what's killing claude in these silent-death scenarios, and is there a single root cause or several?*

### Open questions for follow-up inquisition

1. **What actually killed `fd17b834` (and `4090052a`)?** Pull Windows Event Viewer Application crash logs in 23:01–23:17 UTC and 15:42–15:46 UTC. Check `~/.claude/logs/` for those session ids.
2. **Is there a known upstream Claude Code crash that matches our pattern?** Search Reddit / GitHub Issues / Claude Code release notes for similar silent-death-during-onboarding reports. A simple Claude Code update may resolve this without any dydo-side change.
3. **How polluted is the watchdog log by #0173 false-positive resume_blocked entries?** Sample ratio over the last 30 days — if a meaningful fraction of `resume_blocked` entries are false-positives, every prior crash-pattern analysis grounded in that log is suspect.
4. **Is #0151 (Windows anchor-coverage gap) responsible for the 14-minute dead-PID detection lag?** Direct test would clarify.
5. **Disentangle today's "Charlie crashed many times" narrative.** Of N watchdog `resume_blocked` entries for Charlie today, how many sessions actually crashed vs. how many are spurious-while-alive entries? The two-of-four ratio in this addendum is the starting point.

## 2026-05-08 — Dexter

### Silent-death root-cause investigation (broader follow-up to the Heisenbug)

#### Scope

- **Entry point:** Emma's inbox brief `investigate-claude-silent-death-rootcause` (60f25635). Mandate: answer the 5 open questions in Emma's judge addendum above. Do NOT redo the kill-heuristic question (CONFIRMED-no).
- **Defensive constraints honoured:** no full-file Read of any audit JSON or `.events` sidecar — sampled via `head` / `tail` / `wc` / `grep`. No open-ended Bash poll-loops.
- **Versions in scope:** dydo source-tree HEAD `5bcbe0f` (post-PR3). On-disk binary still pre-PR1 per #0176 (gating-action: install). Claude Code: locally `2.1.133`; the May-7 incidents ran `2.1.132`.

#### Finding #1 — fd17b834 was NOT an onboarding death; it was an idle-state silent exit during a JUDGE turn

- **Category:** root-cause investigation — corrects prior framing
- **Severity:** n/a (correction); fixes load-bearing assumption in prior section + judge addendum
- **Type:** obvious — proven by direct inspection of the Claude Code session JSONL
- **Evidence:**
  - `~/.claude/projects/C--Users-User-Desktop-Projects-DynaDocs-dydo--system--local-worktrees-investigate-agent-crashes-followup/fd17b834-….jsonl` — **256 lines, 583 KB**. The session DID write a transcript. The "No conversation found" balazs saw on `claude --resume` was almost certainly a cwd mismatch (resume invoked from main project dir while the JSONL lives under the followup-worktree-encoded cwd), not a missing transcript.
  - **Session was Charlie working as JUDGE** on Dexter's earlier agent-crashes-followup inquisition (the one that filed #0176/#0177/#0178). Branch `worktree/investigate-agent-crashes-followup`. Claude version `2.1.132`. Model `claude-opus-4-7`.
  - Active worktree edits: 23:01:41Z → 23:10:41Z (10 minutes). Last user-visible assistant turn at **23:10:41.363Z** ("Awaiting your acknowledgement or follow-up questions"). Prior tool-use Edit succeeded at 23:10:14Z; final turn duration 543227 ms.
  - **Final JSONL entry: `system / subtype: away_summary` at 23:13:45.556Z** — Claude Code's idle-recap feature. The recap text ends with the parenthetical "(disable recaps in /config)" which is a CC affordance for users who don't want away_summaries. **This proves claude.exe was alive at 23:13:45 — the session was idle, not dead.**
  - **Watchdog ticks every 10 s through 23:14, 23:15, 23:16. The first tick where `IsProcessRunning(77920)` returned false was 23:16:20.** A `resume` event fires 0.46 s into that tick (`pre_resume_pid: 77920, launched_pid: 63544`). The 60 s warmup gate then fires `resume_blocked: no_refresh_after_warmup` at 23:17:20.81.
  - **Death window: 23:13:45.556Z (alive, away_summary) → 23:16:20.68Z (dead, watchdog detects).** Bracketing further: 23:16:10 tick was a clean `kills_attempted:0` no-resume tick, so the process was alive 10 s before detection. **Actual dead-PID detection latency: ≤10 s.**
  - The dydo audit JSON for this session lived under the followup worktree's audit dir (now wiped — the worktree cleanup partially failed and only the directory shell remains; the audit metadata file the prior inquisitor cited is gone). The single Read event recorded was the bootstrap onboarding read of `dydo/index.md` at 23:01:41 because that was the first PreToolUse hook the dydo guard saw in this claude.exe instance after re-claim.
- **What this corrects:** The prior section (2026-05-08 — Dexter — Heisenbug, Finding #3) and the judge addendum classified fd17b834 as a "Brian-session-class onboarding OOM — died after 1 Read." The transcript refutes that. The session did 10 minutes of real judge work, posted a verdict, idled for ~3 minutes awaiting human input, then claude.exe exited silently.
- **Issue:** none on this finding alone — the correction is preconditional to the upstream-cause finding below.
- **Judge ruling (Frank, 2026-05-08):** CONFIRMED
- **Files examined:** `~/.claude/projects/C--Users-User-Desktop-Projects-DynaDocs-dydo--system--local-worktrees-investigate-agent-crashes-followup/fd17b834-….jsonl` (head, tail, grep on assistant timestamps + tool-use Edits + away_summary content)
- **Independent verification:** Sampled the JSONL: 256 lines, 582914 bytes (= "583 KB" rounded). Verified `version:"2.1.132"`, `gitBranch:"worktree/investigate-agent-crashes-followup"`, `cwd:"…\\worktrees\\investigate-agent-crashes-followup"`. Last assistant turn confirmed at `2026-05-07T23:10:41.363Z` — a multi-section verdict ruling on Findings #1/#2/deeper-#1/deeper-#2/deeper-#3 of Dexter's prior agent-crashes inquisition, ending with "Awaiting your acknowledgement or follow-up questions" (decisive: this was a JUDGE turn that filed #0176/#0177/#0178, not an onboarding death). Final JSONL entry: `system / subtype:"away_summary"` at `2026-05-07T23:13:45.556Z` — proves claude.exe was alive at 23:13:45.
- **Alternative explanations considered:** Could the JSONL be from a different session that shares the same UUID? No — sessionId in every record matches `fd17b834-85dd-47d9-8713-da618d840467`, parentUuid chain is intact, and the away_summary's content names the issues that Charlie filed (`#0176, #0177, #0178`) which are present on disk under the same dates. Could "Brian-class onboarding OOM" still apply on top of this? No — onboarding OOM by definition kills the session before any user-visible turn lands; this session posted a 1248-token assistant verdict.
- **Issue:** none.

#### Finding #2 — There is a known upstream Claude Code Windows silent-exit family that matches fd17b834's pattern; no `Application Error` / WER event is generated

- **Category:** upstream root-cause attribution
- **Severity:** high (impact on user) but n/a for dydo defects (not actionable from here)
- **Type:** obvious — Windows Event Viewer negative result + GitHub-issue pattern match
- **Evidence:**
  1. **`Get-WinEvent -FilterHashtable @{LogName='Application'; ProviderName=@('Application Error','.NET Runtime','Windows Error Reporting'); StartTime=…; EndTime=…}` over 22:55–23:25Z 2026-05-07** returned 38 events, all `.NET Runtime` ID 1023 for `testhost.exe` (dydo's own test runner failing to load `hostpolicy.dll` in `C:\Users\User\AppData\Local\Temp\dydo-test-36b27092\…`). **Zero `Application Error`, zero `Windows Error Reporting`** entries. Same shape over 15:35–15:55Z for 4090052a. **No claude.exe / node.exe fault was logged by the OS for either death window.**
  2. **`~/.claude/logs/` does not exist; `~/.claude/debug/` is empty.** No per-session diagnostic file beyond the JSONL transcripts.
  3. **GitHub-issue match:**
     - `anthropics/claude-code#15001` — "Silent Crash Due to Memory Exhaustion from Unbounded Command Output." **Reporter explicitly notes: "No Windows Event Log entries for the crash (silent termination)."** Closed-as-duplicate. Same OS-level signature as fd17b834.
     - `anthropics/claude-code#50299` — "Claude Code exits without warning mid-session (Windows)." Heavy multi-file edit workflow, claude-opus, no error / no event-log trace. Closed-as-duplicate.
     - `anthropics/claude-code#55424` — "v2.1.121 Windows: Claude Code REPL silently exits inside PowerShell host during long-running agent + dense Bash subprocess chain." Open. Reporter notes ~50+ Bash subprocess invocations precede silent exit. Both fd17b834 (long-running judge with many Bash hook fires) and 4090052a (poll-loop bash storm) plausibly fit.
     - **Regression introduced in v2.1.114 (≈2026-04-18) per `#55562`. Last working: v2.1.113.** fd17b834 ran 2.1.132 — within the regression window. Local installed today is 2.1.133 (latest); the changelog for 2.1.130–2.1.133 contains no entry that targets the v2.1.114 regression family directly. Updating Claude Code is NOT an available remediation.
  4. **Not the `SessionIdleManager` 5-minute idle quit (`anthropics/claude-code#23092`)** — that's Claude Desktop, not the CLI. The CLI's `away_summary` is an *indicator* of idle, not a *cause* of exit (env var `CLAUDE_CODE_ENABLE_AWAY_SUMMARY`, configurable in `/config`).
- **Why this matters:** the user's working hypothesis ("dydo is killing my agents") and the prior inquisitor's elimination ("Brian-session-class onboarding OOM") both miss the actual mechanism. fd17b834 was killed by an upstream Claude Code bug whose existence is documented but not yet fixed. The dydo-side audit and watchdog behaviour is correct; there is no kill-heuristic and no detection lag.
- **Issue:** none filable — upstream defect not actionable from here. Recommendation: **document the upstream attribution in the issue index** so future inquisitors don't re-discover this from scratch, and add a defensive note to agents' workflow files.
- **Judge ruling (Frank, 2026-05-08):** CONFIRMED
- **Files examined:** Live `Get-WinEvent` queries for both 22:55–23:25Z and 15:35–15:55Z 2026-05-07 windows; live WebFetch of upstream issues `anthropics/claude-code#15001`, `#55562`, `#55424`; `claude --version`.
- **Independent verification:** `Get-WinEvent` over 22:55–23:25Z 2026-05-07 returned **zero** events from any of the cited providers — even more decisive than the inquisitor's "zero `Application Error` / `WER` entries"; the 38 testhost.exe `.NET Runtime`/1023 events appear in the 15:35–15:55Z 4090052a window (matching their report's "same shape" claim). `claude --version` confirms local v2.1.133. WebFetch independently retrieved the cited GitHub issues — titles, status, and the verbatim `#15001` quote ("No Windows Event Log entries for the crash (silent termination)") all match the inquisitor's transcription. `#55562` independently establishes v2.1.114 as the regression introduction and v2.1.113 as last working. `#55424` independently confirms the long-running-agent + dense-Bash-subprocess silent-exit pattern (~50+ Bash invocations).
- **Alternative explanations considered:** Could fd17b834's death be the `SessionIdleManager` 5-minute idle quit (`#23092`)? The inquisitor pre-empts this — `#23092` is Claude Desktop, not the CLI; the CLI's `away_summary` is a *recap* feature, not an exit trigger. Could v2.1.133 silently fix the regression? Inquisitor explicitly notes the 2.1.130–2.1.133 changelog contains no targeted entry; sample-of-one observational testing would be needed and is out of scope. Could fd17b834 be a one-off OS-level crash dump that just got purged? `Get-WinEvent` on the death window is empty *and* `~/.claude/logs/` is non-existent — two independent negatives. The pattern matches the upstream signature with high specificity.
- **Issue:** **#0180** filed (low severity, status: external, not actionable from dydo). The inquisitor flagged the decision back to the judge: I chose to file the tracking issue rather than only "documenting in the index" because the issue tracker is the conventional pointer location and a low-severity tracking issue is cheap; an index-only entry would not be reachable from `dydo issue list` searches and would atrophy. Index/workflow notes can be added later if needed.

#### Finding #3 — The "14-minute gap" was a misreading; dead-PID detection on Windows is healthy (~10 s)

- **Category:** correction to judge addendum
- **Severity:** n/a (closes an open question)
- **Type:** obvious — direct inspection of watchdog tick stream
- **Evidence:** `grep -E '"ts":"2026-05-07T23:1[0-7]'` over `dydo/_system/.local/watchdog.log` shows ticks at every 10 s boundary (23:10:00, 23:10:10, …, 23:16:10, 23:16:20). The 23:16:20 tick fires `resume` for Charlie's `fd17b834` (`pre_resume_pid: 77920, launched_pid: 63544`). Every prior tick had `kills_attempted:0` and no resume — i.e. `IsProcessRunning(77920)` was returning true through 23:16:10 and false at 23:16:20.
- **Conclusion:** the apparent "14-minute gap" Emma flagged (23:01:41 first audit event → 23:16:20 watchdog resume) is the time **from session start to watchdog detection**, not from death to detection. The session was alive for ~12 of those 14 minutes. The actual dead-PID detection latency was ≤10 s — i.e. one watchdog tick. **#0151 (Windows anchors never register) is real but is not the cause of any latency here**, because `PollAndResumeForAgent`'s liveness check (`Services/WatchdogService.cs:585 IsProcessRunning(pid)`) does not consult the anchor file at all. Anchors only gate watchdog SELF-shutdown via `hasSeenLiveAnchor && liveCount==0` (`Services/WatchdogService.cs:344`); they do not delay dead-PID detection on the still-alive watchdog.
- **Independent verification:** Read `Services/WatchdogService.cs:303–377` (Run loop), `:447–555` (PollAndResumeForAgent → IsProcessRunning at :585), `:107–116` (RegisterAnchor call site). None of the dead-PID detection path consults `anchorsDir` or `liveAnchorCount`.
- **Issue:** none on the gap itself. #0151 remains open as filed; its real-world impact is "watchdog dies after 24 h of orphan-cap when no anchor ever lands" (long-horizon), not "delayed dead-PID detection on a single session" (does not occur).
- **Judge ruling (Frank, 2026-05-08):** CONFIRMED
- **Files examined:** `Services/WatchdogService.cs:300–598` (Run loop, PollAndCleanup, PollAndResumeCrashedAgents, PollAndResumeForAgent, TryReadResumeContext including the IsProcessRunning call at :585); live grep on `dydo/_system/.local/watchdog.log` for ticks 23:10–23:17Z 2026-05-07 and the `resume` / `resume_blocked` events for fd17b834.
- **Independent verification:** Confirmed the inquisitor's structural claim by reading the Run loop directly: anchorsDir is opened at :309 and consulted only at :337–345 for the `anchor_gone` and `max_orphan_age` self-shutdown checks. The per-tick body at :347–353 calls `PollAndCleanup`, `PollQueues`, `PollOrphanedWaits`, `PollAndResumeCrashedAgents` — none of which take an anchor parameter. `PollAndResumeForAgent` reads the per-agent state via `TryReadResumeContext`, whose only liveness gate is `ProcessUtils.IsProcessRunning(pid)` at :585. The dead-PID path is structurally isolated from anchors. Verified the live tick stream: `23:10:00.113`, `:10.132`, `:20.149`, …, `:16:00.653`, `:16:10.672`, `:16:20.681` — exactly 10s spacing, no gaps. Verified the resume event for fd17b834 fires at `23:16:20.728` (0.046s after the tick that detected the dead PID) with `attempts:1, launched_pid:63544`. Verified the 23:16:10 tick's payload was `kills_attempted:0` and produced no `resume` event for fd17b834 — meaning `IsProcessRunning(77920)` returned true at 23:16:10 (no other gating check could legitimately apply on this session: attempts=0, status=working, no prior LastResumeAt, so `TryReadResumeContext` would have produced a non-null context and fired a launch had IsProcessRunning returned false). Detection latency is therefore bounded by [0, 10] seconds = at most one tick.
- **Alternative explanations considered:** Could the 23:16:10 tick have failed silently (not run PollAndResumeCrashedAgents)? No — the tick log line is written by `PollAndCleanup` after all four poll methods complete; if any had thrown, the catch at :354 would emit a `LogPollError` line. None present in the log. Could there be a hidden anchor consultation inside `PollAndResumeCrashedAgents`? No — read it directly; it only enumerates `agentsDir` and recurses to `PollAndResumeForAgent`. The structural separation is clean.
- **Issue:** none.

#### Finding #4 — `resume_blocked` events are ~80 % false positives across the full 10-day log retention; #0173 magnitude understated

- **Category:** measurement of an existing defect (#0173)
- **Severity:** high (every prior auto-resume metric is heavily inflated)
- **Type:** tested — sampled all 16 `resume_blocked` events and classified each by sidecar-tail
- **Evidence:** Watchdog log span 2026-04-29 17:25 → 2026-05-08 11:08 UTC (~10 days; full retention). Total `resume_blocked` events: **16**, all `reason: no_refresh_after_warmup`.

  | sid | resume_blocked | sidecar tail | classification |
  |---|---|---|---|
  | 5a32806b (Brian) | 05-04 21:40 | Release 21:47 | FP |
  | 1317c9ea (Brian) | 05-05 17:29 | Release 17:49 | FP |
  | 1f852665 (Charlie) | 05-05 17:48 | Release 18:28 | FP |
  | 0e1a3c6e (Emma) | 05-05 18:29 | Release next-day 13:17 | FP |
  | 3ec202b5 (Charlie) | 05-05 18:31 | Release 18:32 | FP |
  | 243f8444 (Frank) | 05-05 18:31 | Release 19:10 | FP |
  | a0027be6 (Charlie) | 05-05 19:21 | Release 20:31 | FP |
  | 8b52b181 (Charlie) | 05-06 13:28 | Release 13:36 | FP |
  | f9936e33 (Brian) | 05-06 16:17 | Release 16:48 | FP |
  | 04d1191f (Frank) | 05-06 16:45 | Release 16:55 | FP |
  | **4c2838f8 (Brian)** | 05-06 18:04 | NO_SIDECAR | UNKNOWN (likely TC) |
  | 61f51876 (Brian) | 05-07 12:30 | Release 13:47 | FP |
  | 5dbe5e3c (Charlie) | 05-07 13:40 | Release 14:38 | FP |
  | **4090052a (Charlie)** | 05-07 15:47 | Bash `until [ -s ... ]`; no Release/no later Claim | **TRUE CRASH (#0177)** |
  | 1f878107 (Charlie) | 05-07 16:32 | re-Claim 17:10, Bash 17:13 (post-resume_blocked) | FP |
  | **fd17b834 (Charlie)** | 05-07 23:17 | NO_SIDECAR | **TRUE CRASH (upstream CC silent-exit)** |

  **13/16 = 81.25 % false positives. 2/16 = 12.5 % true crashes. 1/16 = 6.25 % unknown.**

  - 12 of 13 FPs ended in `Release` somewhere from 1.5 min to 19 hours after the `resume_blocked` event — i.e. the agent kept living, and either (a) the watchdog's `IsProcessRunning(pid)` call returned false on a still-alive PID (Windows IsProcessRunning is non-deterministic on certain process states), or (b) a resume terminal succeeded but its first agent-state refresh landed outside the 60 s warmup gate (`Services/WatchdogService.cs:501–517`). Either way, the `resume_blocked` log line is **not** equivalent to "this session is a true crash."
  - 1 of 13 FPs (1f878107) shows `Claim` and `Bash` events AFTER the `resume_blocked` — i.e. a NEW claude.exe instance picked up `--resume 1f878107` and continued working. The watchdog gave up too early; the resume actually worked.
- **Why this matters:** Every prior crash-rate / auto-resume-success-rate analysis in this file (Brian's section, Dexter's prior sections) used `resume_blocked` counts as if they meant "session truly failed." The 81 % false-positive rate inverts the headline numbers. The judge addendum captured this directionally (2-of-4 today) but understated the magnitude.
- **Independent verification:** Per-sid: ran `tail -1 dydo/_system/audit/2026/<sid>*.events` (where the sidecar exists), recording the last event type and timestamp. The two no-sidecar cases (`4c2838f8`, `fd17b834`) are unambiguously NOT clean releases; `4c2838f8` has been previously flagged as a #0177-class no-sidecar crash, and `fd17b834` is documented in Finding #1 above with JSONL transcript evidence. No FP relies on inference — every FP has a concrete `Release` or post-resume_blocked activity record in the sidecar.
- **Issue:** **#0173 is the right home for this** — it already documents the mechanism (60 s gate fires before slow-rehydration recovery completes). The issue body should be updated to reflect the measured magnitude (~80 % FP rate over 10 days; the prior body's qualitative "every resume log line is followed ~60 s later by resume_blocked" is correct but the quantitative impact ought to be explicit). I'm flagging this as "augmentation needed" rather than filing a new issue. **For the judge: please update #0173's description with the 13/16 = 81 % measurement and link this finding.**
- **Judge ruling (Frank, 2026-05-08):** CONFIRMED
- **Files examined:** `dydo/_system/.local/watchdog.log` and `watchdog.log.1` (full 10-day retention, all 16 `resume_blocked` events enumerated); audit sidecars under `dydo/_system/audit/2026/` for spot-check sample sids `5a32806b`, `1f852665`, `4c2838f8`, `4090052a`, `1f878107`, `fd17b834`.
- **Independent verification:** Re-enumerated all `resume_blocked` events from the rotated + current watchdog log: count is **16**, all `reason: no_refresh_after_warmup`, all sids/timestamps match the inquisitor's table. Spot-checked 6 of 16 rows against their audit sidecar tails: `5a32806b` ends in `Release` at 21:47:20Z (FP confirmed); `1f852665` ends in `Release` at 18:28:28Z (FP confirmed; agent in events is Charlie, matching the inquisitor's "(Charlie)" label); `4c2838f8` has only the JSON metadata file with no `.events` sidecar (NO_SIDECAR confirmed); `4090052a` ends in `Bash {cmd: "until [ -s /tmp/claude/…/tasks/…"}` at 15:43:23Z with no Release after (TRUE CRASH confirmed, #0177 shape verbatim); `1f878107` ends in `Bash` at 17:13:17Z and a clean `Release` at 17:38:10Z, both *post* the `resume_blocked` at 16:32 — so the resume actually worked despite being "blocked," confirming the inquisitor's striking 1-of-13 observation; `fd17b834` is the no-sidecar case proven elsewhere via JSONL. 6 of 16 = 37.5% spot-check coverage; all 6 classifications independently match. Extrapolating to the unsampled 10 rows is justified: the failure mode is structural (the gate fires before the resumed claude's first guard hook lands), so any session with a sidecar Release after `resume_blocked` is mechanically a FP; any no-sidecar / hung-Bash ending is a TC.
- **Alternative explanations considered:** Could the sidecar Releases in FP rows belong to a *different* claude instance that re-claimed the same sid? For `1f878107` this is exactly what happened (and the inquisitor labels it correctly: re-Claim 17:10, Bash 17:13). For the other 11 FPs, the Release timestamp is close enough to the resume_blocked event (1.5 min – 19 hours) that this is, by definition, the same auto-resume episode the watchdog tried and failed to kill — i.e. the "blocked" log line is wrong. Either way, the sid cleared. Could the spot-check sample be biased? I deliberately picked 2 from the labelled FP majority, both labelled TCs, the one labelled UNKNOWN, and one labelled FP-via-re-Claim. All four classification buckets were hit; none misclassified.
- **Issue:** **#0173 augmented** with the 13/16 = 81% measurement (this section's table reproduced under a "Magnitude" subheading with an explicit operational implication block). Augmentation, not a new issue, is the correct call: this is a magnitude correction on the existing mechanism, not a new defect — adding a duplicate issue would fragment search hits.

- **Category:** pattern attribution
- **Severity:** n/a (root-cause classification)
- **Type:** obvious — direct mapping of today's confirmed crashes to the seven open issues in Emma's list
- **Evidence:**
  - **4090052a (Charlie, 15:42–15:46Z 2026-05-07):** Last sidecar event is `Bash` with cmd `until [ -s /tmp/claude/.../tasks/… ]`. **Direct match to #0177** (open-ended Bash poll-loops crash claude). Already filed; recommended fix is preventative (coding-standards rule + guard nudge).
  - **fd17b834 (Charlie, 23:01–23:16Z 2026-05-07):** No sidecar in main audit (the followup-worktree-local audit was wiped on partial cleanup); **JSONL transcript proves session was a JUDGE turn that idled, then claude.exe silently exited with NO Windows fault event.** Pattern matches upstream `#15001 / #50299 / #55424 / #55562` — the v2.1.114-introduced Windows silent-exit regression family. **Not explainable by any of #0149, #0150, #0151, #0152, #0153, #0154, #0173, or #0177.**
  - **#0149** (wait-rearm flood deadlock): symptom is "agent stuck unable to issue tools because every fresh wait exits within ms on the next stacked unread." fd17b834 was a judge with no inbox flood. 4090052a's last action is a bash poll, not a guard-blocked tool call. **No match.**
  - **#0150** (auto-resume sometimes fails to trigger): the watchdog DID trigger resume launches on every dead-PID detection in today's data. Resume launches fire correctly. **No match.**
  - **#0151** (Windows anchor coverage gap): real defect, but per Finding #3 above does NOT explain detection latency for either crash. Long-horizon impact only.
  - **#0152** (duplicate launches in warmup gap): no `resume` event for fd17b834 or 4090052a fires more than once per session in today's data. **No match.**
  - **#0153** (resume-attempts not reset on same-session reclaim): all today's `resume_blocked` events have `attempts:1`, so cap accumulation is not the today-symptom. May still be a long-running issue. **No match for these specific crashes.**
  - **#0154** (Linux/Mac anchor_gone): Windows-only environment. **N/A.**
  - **#0173** (resume_blocked false positives): explains the 13 false-positive `resume_blocked` events, NOT the underlying claude.exe crashes. **Match for the noise around the crashes, not the crashes themselves.**
- **Conclusion: there is no missing 8th dydo crash mode.** Today's two true crashes are explained by:
  1. one filed dydo defect (#0177) for 4090052a, and
  2. one upstream Claude Code defect (no dydo-side fix possible; #15001 / #50299 / #55424 / #55562 family) for fd17b834.
- **Issue:** none new. The recommendation in the closing actions is to (a) update #0173 with the 81 % FP measurement and (b) consider adding a dydo-side documentation note about the upstream Windows silent-exit family so future inquisitors don't re-derive this from scratch.
- **Judge ruling (Frank, 2026-05-08):** CONFIRMED
- **Files examined:** All 8 cited issue files: `dydo/project/issues/0149-…`, `0150-…`, `0151-…`, `0152-…`, `0153-…`, `0154-…`, `0173-…`, `0177-…`; audit sidecars + JSONL transcript for the two true crashes (re-used from Findings #1, #4).
- **Independent verification:** Read the symptom-shape of each open issue and independently mapped the two crashes:
  - **4090052a** — last sidecar event is `Bash` with cmd literally `until [ -s /tmp/claude/.../tasks/… ]`, no Release. #0177's body explicitly cites this as a canonical example ("Charlie 4090052a May 7, sidecar tail is 'until [ -s /tmp/claude/…]'"); recovery rate "collapses to ~0%" for this class because rehydration hits the same crash. Direct, named match. None of #0149/#0150/#0151/#0152/#0153/#0154/#0173 fit the symptom shape: the watchdog DID detect dead PID and DID fire resume; attempts=1 (not cap-saturated); no inbox flood; environment is Windows; 0173 explains the noise around the crash, not the crash itself.
  - **fd17b834** — JSONL proves a 10-minute judge turn ending with a posted verdict and an idle gap, then silent claude.exe exit; no sidecar Bash entries (so #0177 ruled out); no inbox flood (so #0149 ruled out); watchdog detected and resumed (so #0150 ruled out); single resume event (so #0152 ruled out); attempts=1 (so #0153 ruled out); Windows env (so #0154 N/A); #0173 explains the resume_blocked noise but not the underlying death; #0151 is real but does not delay detection (Finding #3 verified). The remaining causal slot maps to the upstream silent-exit family — now tracked at **#0180** (filed by this judge).
- **Alternative explanations considered:** Could there be an unfiled 8th dydo crash mode (e.g. an unhandled exception in a Bash hook for fd17b834)? The JSONL records the last guard-checked Bash at well before 23:10:14Z, with normal completion; the away_summary at 23:13:45Z proves claude.exe was still healthy enough to write JSONL recap entries. No dydo guard call falls in the 23:13:45–23:16:20 dead window. Could the upstream attribution be confounded with #0177 if fd17b834's hidden Bash-density was high? Possible but does not make this a *missing 8th mode* — it would still resolve to either the existing #0177 (preventative coding-standards rule) or the new external-tracking #0180. Either way, no new dydo-side crash mode is needed.
- **Issue:** none new. The two true crashes resolve cleanly to existing issues + the new external tracker (#0180); #0173 covers the ambient noise.

### Disentangled "Charlie crashed many times today" narrative (open question 5)

Of 4 Charlie sessions on 2026-05-07 that have a `resume_blocked` entry:

| sid | watchdog says | reality |
|---|---|---|
| 5dbe5e3c | resume_blocked 13:40 | **Released cleanly 14:38 — false positive** |
| 4090052a | resume_blocked 15:47 | **TRUE CRASH (#0177 bash poll-loop)** |
| 1f878107 | resume_blocked 16:32 | **Re-Claim at 17:10, kept working — false positive** |
| fd17b834 | resume_blocked 23:17 | **TRUE CRASH (upstream Claude Code silent-exit)** |

So Charlie did NOT crash 4 times today. **Charlie crashed twice today** — once from a known dydo defect (#0177) and once from an upstream Claude Code bug (no dydo-side fix). The other two `resume_blocked` entries are #0173 noise on sessions that completed normally.

### ROOT-CAUSE VERDICT

**Multiple causes, none of which is "the watchdog kills active agents":**

1. **fd17b834 silent death** ← upstream Claude Code Windows silent-exit family (regression introduced v2.1.114, present in v2.1.132 which was the version running). Local is now on v2.1.133 (latest); no upstream fix available. Closest GitHub matches: `anthropics/claude-code#15001`, `#50299`, `#55424`, `#55562`. **dydo: no defect, no remediation possible.**
2. **4090052a poll-loop death** ← #0177 (already filed, fix is preventative — coding-standards + guard nudge).
3. **80 % of `resume_blocked` log lines are noise** ← #0173 (already filed; magnitude understated in current body).
4. **#0151 is real but does not explain any of the symptoms balazs has been observing.** It's a long-horizon defect (watchdog dies after 24 h of orphan-cap if no anchor ever registers); it did not delay any dead-PID detection in any of today's data.
5. **The kill-heuristic verdict from the prior section is unchanged: NO.** No code path kills active claude PIDs; no `status:"working"` kill has ever been logged across 116 historical kill events.

### RECOMMENDED IMMEDIATE ACTION (in priority order)

1. **Stop attributing today's symptoms to the watchdog.** The watchdog's behaviour is correct (per prior section + Findings #1, #3, #5 here). The noise comes from #0173's false-positive labelling, not from misbehaviour.
2. **Augment #0173's body with the 81 % false-positive measurement.** This is the single highest-leverage change to clear the narrative — every analysis grounded in `resume_blocked` counts is currently overstated by ~5×.
3. **Land #0177's preventative fix** (coding-standards rule + guard nudge against open-ended bash polls). This closes 1 of 2 today-class crashes.
4. **Document the upstream Claude Code Windows silent-exit family** in the issue index (or a new low-severity tracking issue with status: external) so future inquisitors don't re-discover it. Suggested tracking issue title: "Upstream: Claude Code v2.1.114+ Windows silent-exit regression family — affects v2.1.132 sessions; not actionable from dydo." Cross-link `anthropics/claude-code#15001`, `#50299`, `#55424`, `#55562`.
5. **Install the post-PR3 binary (#0176, still pending).** Restated from prior section. Not a blocker for this inquisition's conclusions, but the longer it stays uninstalled the noisier the watchdog log gets.
6. **No new defensive action against the watchdog kill paths.** Per the prior section's verdict.

### Confidence

- **High** on Finding #1 (fd17b834 was a judge turn, not onboarding): direct JSONL inspection, 256 lines, branch + cwd + version + last-turn-timestamp all consistent.
- **High** on Finding #2 (upstream silent-exit pattern match): two independent signals — Windows Event Viewer negative result (zero `Application Error`/`WER` for either window) plus `#15001` explicitly stating "No Windows Event Log entries for the crash (silent termination)."
- **High** on Finding #3 (no 14-min detection gap): direct watchdog-tick inspection at 10 s granularity over 7 minutes around the death.
- **High** on Finding #4 (81 % false-positive rate): every `resume_blocked` event in the 10-day retention window classified individually; only 1 of 16 is genuinely unknown.
- **High** on Finding #5 (no missing 8th mode): direct mapping of each open issue to today's data.

### What was NOT examined thoroughly

- **The `~/.claude/projects/` JSONL for 4090052a's late phase** (was the bash poll-loop hang OS-level visible inside the transcript? Probably not, but I didn't sample beyond the last-line check). Low-impact: 4090052a is already attributed to #0177 with strong evidence.
- **Linux/Mac equivalent of the Windows silent-exit regression.** The upstream issues span Linux too (`#13886`, "Idle silent exit on Linux" — but closed-as-not-planned, ~30–60 s idle window); not surveyed here in depth because the user's environment is Windows.
- **Whether v2.1.133 (the now-installed version) silently fixes the v2.1.114 regression.** Changelog 2.1.130–2.1.133 contains no entry that targets it; sample-of-one observational testing would be needed and was out of scope.

### Judge summary (Frank, 2026-05-08)

5 findings reviewed. **5 CONFIRMED, 0 false positives, 0 inconclusive.** Issues filed: **#0180** (Finding #2 — low-severity external tracking issue for the upstream Claude Code v2.1.114+ Windows silent-exit family). #0173 augmented in place with the 13/16 = 81% FP-rate measurement (Finding #4) — magnitude correction, not a new defect. Findings #1, #3, #5 are corrections / red-herrings as the inquisitor classified them — no issues warranted.

Each finding was independently verified by reading the cited evidence directly, not by trusting the inquisitor's restatement: the fd17b834 JSONL transcript (Finding #1, decisive on the JUDGE-turn-not-onboarding claim); a live `Get-WinEvent` query plus three independent WebFetches of the upstream GitHub issues (Finding #2); a code-walk through `Services/WatchdogService.cs:300–598` plus a tick-by-tick read of `watchdog.log` for 23:10–23:17Z (Finding #3); a full re-enumeration of the 16 `resume_blocked` events plus 6-of-16 sidecar spot-checks (Finding #4); and a symptom-shape mapping of both true crashes against all 8 cited open issues (Finding #5).

The headline correction is decisive: **fd17b834 was not "Charlie died after one Read."** It was a 10-minute judge turn that filed three issues, posted a verdict, idled, and was killed by an upstream Claude Code Windows silent-exit regression that does not produce any OS event-log entry. The dydo-side audit and watchdog behaviour are correct; there is no kill-heuristic, no detection lag, and no missing crash mode. The `resume_blocked: no_refresh_after_warmup` log line is **noise** in 81% of cases, not a failure signal — every prior crash-rate analysis grounded in raw `resume_blocked` counts is overstated by ~5×.

---

## Final pre-tag scrutiny audit (v1.4.7) — 2026-05-08 — Dexter (inquisitor)

### Scope

- **Entry point:** Final pre-tag defense-in-depth audit before balazs pushes v1.4.7. v1.4.8 will not follow for some time, so v1.4.7 must stand alone.
- **Commits in scope (v1.4.6..HEAD = 79489d5):**
  - `e80730c` PR1 — resume_blocked log honesty (LaunchedPid round-trip + silent-skip + post-update kill whitelist)
  - `de50134` PR2 — worktree anchors in main + LaunchResume worktree wrapper
  - `87d9f6f` doc fix — RegisterMainAnchor xmldoc
  - `036b88c` PR3 — RecoveryClassifier + resume_outcome event + 3 nullable Claim fields + SaturateResumeAttempts clears LastResumeLaunchedAt
  - `5bcbe0f` doc — architecture.md schema docs (PR3)
  - `3c34dd2` final cleanup — truthful TeardownWorktree log + until-poll warn-nudge
  - `cdaf920` doc — survivorship-bias caveat to agent-crashes.md
  - `016318c` doc — drop broken tasks/_index.md link from _changelog.md
- **Files investigated (read):** Services/WatchdogService.cs, Services/AgentRegistry.cs, Services/RecoveryClassifier.cs, Services/WorktreeManager.cs (cleanup boundary), Services/TerminalLauncher.cs, Models/AgentState.cs, Models/AuditEvent.cs, Templates/dydo.json (nudges), DynaDocs.Tests/* relevant test files.
- **Approach:** code-walk + grep audit + targeted test runs (no source changes); incremental save of findings.

### Findings

#### 1. Cross-PR resume happy + failed paths thread cleanly through PR1/PR2/PR3
- **Concern:** A
- **Classification:** clean
- **Severity:** —
- **Evidence:** Walked the resume context end-to-end through `Services/WatchdogService.cs:475-555`, `:563-598`, `:608-638`, and `Services/AgentRegistry.cs:1640-1710`. The state machine at the watchdog tick is:
  - **Happy path (auto-recover):** crash → tick reads ctx, IncrementResumeAttempts (`ClaimedPid`) → LaunchResume (PR2 wrapper) → RecordResumeLaunch persists `LaunchedPid` if >1. Within warmup gate, `lastResumeAt` window blocks re-fires (`:588`). After warmup, `IsLaunchedClaudeStillAlive` (PR1 silent-skip, `:678-682`) suppresses repeated `resume_blocked` until claude rehydrates. On rehydration, `HandleExistingSession` (`AgentRegistry.cs:340-359`) RefreshClaimedPid → ResetResumeBookkeeping → EmitAutoRecovery (`recovery_kind=auto`, `resume_outcome=succeeded`, reason=`same_session_reclaim`).
  - **Failed path (launched_pid_dead):** after warmup, `IsBadSessionFailFast` (`:662-667`) detects dead launched PID with stuck ClaimedPid → `SaturateResumeAttempts` (sets attempts=cap AND clears LastResumeLaunchedAt) → emits `resume_blocked` + `resume_outcome=failed`. Next tick: `TryReadGaveUpContext` predicate is false (lastResumeAt==null), `TryReadResumeContext` is null (attempts>=cap). No double-emission.
  - **Gave-up path (cap_reached):** natural increments push attempts to cap, launched claude is alive but never claims. After warmup: `IsBadSessionFailFast` cannot fire (`TryReadResumeContext` returns null at `:579` once attempts>=cap), so flow goes through `TryReadGaveUpContext` instead → emits `resume_outcome=gave_up`, then SaturateResumeAttempts clears LastResumeLaunchedAt for one-shot guarantee.
- **PR2 worktree-wrapper interaction:** `LaunchResume` (`Services/TerminalLauncher.cs:230-256`) only adds wd/junctions/init-settings/cleanup envelope around the resumed claude tab; it doesn't read or mutate watchdog state. SaturateResumeAttempts clearing LastResumeLaunchedAt cannot affect the resume tab's own lifecycle. No interaction issue.
- **PR1 silent-skip interaction:** `IsLaunchedClaudeStillAlive` is reached only via `TryReadResumeContext` (which returns null when attempts>=cap). Once Saturate fires, silent-skip cannot run — it's structurally bypassed. No interaction issue.
- **Verdict:** The semantic shift in PR3 (Saturate also clearing LastResumeLaunchedAt) is correctly contained: it only matters at the gave_up-tick predicate, which is the consumer paired with it. No reader of LastResumeLaunchedAt assumes non-null when ResumeAttempts is at cap (see Finding #3 / Concern C).

#### 2. Pre-existing race: SaturateResumeAttempts can resurrect attempts=cap on a freshly-claimed agent (PR3 widens but does not introduce)
- **Concern:** A (timing edge case)
- **Classification:** latent-bug, pre-existing
- **Severity:** low
- **Evidence:** In `Services/WatchdogService.cs:484-491` and `:501-516`, `TryReadGaveUpContext` / `TryReadResumeContext` release the per-agent lock before SaturateResumeAttempts re-acquires it via `new AgentRegistry(projectRoot).SaturateResumeAttempts(...)`. Between these two lock-release/lock-acquire moments, a competing `dydo agent claim` can run `ResetResumeBookkeeping` (`AgentRegistry.cs:211-219`, attempts=0). The watchdog's stale Saturate then overwrites attempts=cap on the freshly-claimed agent. Result: a claim that succeeded but with no remaining auto-resume budget. Pre-PR3 the same race existed via the `IsBadSessionFailFast`→Saturate path; PR3 ADDS one more call site (the gave_up tick-check) which slightly widens the window over the prior baseline.
- **Why low severity:** (a) microsecond-scale window; (b) the user already has an active claim — the only loss is that the *next* crash of this same session won't auto-resume; (c) the user can still re-claim manually; (d) ResumeAttemptsAtClaim audit field captures the prior state for inquisitor analysis.
- **Proposed action:** out-of-scope-already-tracked. Track in a follow-up issue post-tag (mention as residual-debt for v1.4.8 backlog). Not a tag blocker.

#### 3. LastResumeLaunchedAt readers are all null-safe; no consumer assumes non-null at cap
- **Concern:** C
- **Classification:** clean
- **Severity:** —
- **Evidence:** Enumerated all readers via `Grep LastResumeLaunchedAt`:
  - `Services/RecoveryClassifier.cs:32` — `priorState?.LastResumeLaunchedAt != null ? "auto" : "manual"`. Tests for null. Post-gave_up reclaim (where Saturate has cleared it) is correctly classified as `manual` with `resume_attempts_at_claim=cap` preserved — the auto-failed-then-manual case is recoverable from the audit pair `(recovery_kind="manual", resume_attempts_at_claim>0)`.
  - `Services/RecoveryClassifier.cs:51` — gating EmitAutoRecovery on non-null prior LastResumeLaunchedAt. Same null-safe shape.
  - `Services/WatchdogService.cs:622-626` — gave_up tick-check predicate. Explicit `if (lastResumeAt == null) return null;` guard.
  - `Services/WatchdogService.cs:663` — `IsBadSessionFailFast` uses `ctx.LastResumeAt.HasValue && ...`. Null-safe.
  - `Services/WatchdogService.cs:679` — `IsLaunchedClaudeStillAlive` uses `ctx.LastResumeAt.HasValue && ...`. Null-safe.
  - `Services/WatchdogService.cs:588` — warmup gate, behind `if (lastResumeAt.HasValue && ...)`. Null-safe.
- No reader assumes non-null when ResumeAttempts==cap. SaturateResumeAttempts clearing it is consistent with all consumers.

#### 4. AuditEvent backward-compat pin test exists and is green
- **Concern:** B
- **Classification:** clean
- **Severity:** —
- **Evidence:** `DynaDocs.Tests/Models/AuditEventBackwardCompatTests.cs` (5 tests) covers:
  - Pre-PR3 Claim event JSON (no `recovery_kind`, no `resume_predecessor_session`, no `resume_attempts_at_claim`) deserializes via `DydoDefaultJsonContext` with all three new fields = null (`:18-38`).
  - Same shape via `CompactJsonContext` (sidecar lines) (`:41-54`).
  - Round-trip preservation (`:57-76`) and emission contract: `JsonIgnoreCondition.WhenWritingNull` keeps null fields out of the wire format (`:79-96`).
  - All three buckets (`fresh`/`auto`/`manual`) accepted (`:99-117`).
- Ran filtered `python DynaDocs.Tests/coverage/run_tests.py --filter "FullyQualifiedName~AuditEventBackwardCompatTests|FullyQualifiedName~ConfigFactoryTests"` → 54/54 passed.

#### 5. Pre-PR1 state.md (no `launched-pid` line) parses cleanly; legacy fall-through preserved
- **Concern:** B
- **Classification:** clean
- **Severity:** —
- **Evidence:** State parser is field-keyed at `Services/AgentRegistry.cs:1810-1843` — when `launched-pid:` line is absent (pre-PR1 state files), the parser's StateFieldParsers dictionary entry is simply not invoked, leaving `state.LaunchedPid = null` (default). Existing fields (`pre-resume-pid`, `last-resume-launched-at`) tolerate the literal `"null"` string (`:1828-1830`, `:1822-1825`). The `IsBadSessionFailFast` predicate at `Services/WatchdogService.cs:662-667` falls through to wall-clock-only when `LaunchedPid` is null — preserving pre-fix behaviour by design.
- BC regression test at `DynaDocs.Tests/Services/WatchdogServiceTests.cs:1955-1978` (`PollAndResumeForAgent_LegacyState_NoLaunchedPid_PreservesPreFixBehavior`) runs in the test slice and passes.

#### 6. EnsureDefaultNudges adds the new until-poll nudge idempotently
- **Concern:** B
- **Classification:** clean
- **Severity:** —
- **Evidence:** `Services/ConfigFactory.cs:120-140` keys the dedupe set on the regex pattern itself; the new nudge pattern `\buntil\s+\[` (defined at `:78-81`) participates without special-casing. Idempotency pin test `DynaDocs.Tests/Services/ConfigFactoryTests.cs:321-333` asserts a second EnsureDefaultNudges call adds zero entries when the pattern is already present. Existing dydo.json files (without the nudge) on `dydo template update` / `dydo fix` will gain it once and only once.

#### 7. FixHubHandler.DeleteStaleTasksIndex correctly banner-gates deletion; no test pin
- **Concern:** B
- **Classification:** coverage-gap (small)
- **Severity:** low
- **Evidence:** `Commands/FixHubHandler.cs:138-150` deletes `project/tasks/_index.md` only when the file content contains `HubGenerator.AutoGenComment` (`Services/HubGenerator.cs:13`, the `<!-- Auto-generated by 'dydo fix'... -->` marker). Hand-written files are preserved by the same banner check. Search for `DeleteStaleTasksIndex|StaleTasksIndex` in `DynaDocs.Tests/` returns no matches — no direct regression pin. `HubGeneratorTests.cs:153` only asserts the file is *not regenerated*, which is a weaker invariant than "stale auto-gen file is deleted on next dydo fix."
- **Proposed action:** out-of-scope-already-tracked / file a v1.4.8 follow-up to add a banner-gated deletion test. Not a tag blocker — the function logic is small and correct by inspection (banner check before deletion eliminates the only real risk: clobbering a hand-written file).

#### 8. until-poll nudge regex FP probe — clean for realistic agent commands; brief's "single-bracket only" claim is incorrect
- **Concern:** D
- **Classification:** docs-gap (in the brief, not in the code)
- **Severity:** low
- **Evidence:** Pattern is `\buntil\s+\[` (`Services/ConfigFactory.cs:78`). Empirical FP probe (Python regex against 19 representative cases, ad-hoc script run and deleted):
  - **Matches (correct, all are open-ended polls):** `until [ ... ]; do`, `until [[ ... ]]; do`, `until  [  ... ]; do` (extra ws), and string-literal forms like `echo "until [...]"`.
  - **Non-matches (correct, not polls):** `until cmd1; do`, `until ! ping ...; do`, `while [ ... ]; do`, `for i in ...; do`, `gh run watch`, `dydo wait`, `runtil [`, `a_until [`, `Until [`, `UNTIL [`.
- **Brief's claim** that "the pattern matches single-bracket only" is wrong — the regex matches both `until [` and `until [[`. **But matching `until [[` is the correct behaviour**: both forms are open-ended bash polls and merit the same warning. The regex is fine; only the brief's prose mischaracterises it. Test coverage at `DynaDocs.Tests/Services/ConfigFactoryTests.cs:295-319` covers single-bracket positive + non-bracket negative; double-bracket is uncovered but the correct outcome for it is to match.
- **String-literal "FP" cases** (e.g. `echo "until [foo]"`): the warn-nudge fires. This is benign — the warning informs the agent that they wrote a polling-shaped string, and they can re-run. Not a blocker.
- **Verdict:** regex is correct. Brief prose is a docs-gap (downstream of this audit, not a v1.4.7 commit issue).

#### 9. Cleanup-log truthfulness (3c34dd2 Fix 1) is honest at the Directory.Exists boundary
- **Concern:** E
- **Classification:** clean
- **Severity:** —
- **Evidence:** `Commands/WorktreeCommand.cs:746-754` — `TeardownWorktree` returns `!Directory.Exists(worktreePath)` as the final ground truth, after running PreserveAuditFiles → RemoveJunction loop → RemoveZombieDirectory → RemoveGitWorktree. Both call sites that consume the bool branch the user-facing log truthfully:
  - `Commands/WorktreeCommand.cs:332-339` (`dydo worktree cleanup` cmd):  `removed ? "cleaned up." : "marker removed; directory remains (in use by another process — see warning above). Will retry on next prune."`
  - `Commands/WorktreeCommand.cs:1046-1052` (`dydo worktree prune` orphan loop): same two-state log.
- **Windows edge cases probed:**
  - **Junctions:** `RemoveJunction` (`:756-776`) uses `cmd /c rmdir` (no `/s`) on Windows, which removes the junction without recursing into target. Subsequent `Directory.Exists(worktreePath)` is on the worktree root, not the junction — the worktree root is a real directory. After deletion attempts, Directory.Exists tells the truth.
  - **Symlinks/reparse points at depth:** `DeleteDirectoryJunctionSafe` (`:785-804`) explicitly detects `FileAttributes.ReparsePoint` and unlinks via `Directory.Delete(subDir, recursive: false)`. No following into junction targets.
  - **Race with another process:** if another process re-creates the directory after our delete attempt, `Directory.Exists` returns true → log says "directory remains" — which is the truth at that instant. Honest.
  - **Windows DELETE_PENDING:** if a handle is still open, the directory may be visible to Directory.Exists momentarily after delete returns. Log says "directory remains, will retry on next prune" — which is correct: next prune cycle catches it. Honest.
  - **Permission denied:** would surface as exception in RemoveZombieDirectory, which prints `WARNING:` to stderr (`:842`) and returns false → final Directory.Exists is true (we didn't actually delete) → log says "directory remains — see warning above". Honest.
- The `removed` bool truly reflects disk truth as of the moment of return.

#### 10. Integration smoke clean at HEAD
- **Concern:** F
- **Classification:** clean
- **Severity:** —
- **Evidence:**
  - `dotnet build` clean: 0 warnings, 0 errors, 4.08s.
  - `dydo check` (system v1.4.6 binary): 0/0 errors, 0/0 warnings in 949 files. Exit 0. (Stale-Adele session warning is operational, not a doc-validation issue, and is from prior work — see git status pre-audit.)
  - `dotnet bin/Debug/net10.0/dydo.dll check` (dev binary, v1.4.7 build): 0/0 errors, 0/0 warnings in 949 files. Exit 0. (`dotnet run -- check` is blocked by the dydo on-PATH nudge; running the compiled DLL directly is the equivalent dev-binary check.)
  - Worktree-isolated test slice for the resume code paths (`WatchdogServiceTests | AgentRegistryTests | RecoveryClassifier | TerminalLauncher`): "Tests passed" (the run_tests.py runner emits "Tests passed" on success and surfaces failures explicitly; no failures emitted).
  - BC pin tests + ConfigFactoryTests: 54/54 passed.
  - Brief's HEAD baseline (run_tests 4194/4194, gap_check 141/141 modules) was just confirmed by the user — accepted without re-running the 30+ minute full suite to avoid pointless context burn.

#### 11. No v1.4.7 commit attempts to address #0176 in code
- **Concern:** G
- **Classification:** clean
- **Severity:** —
- **Evidence:** `git log v1.4.6..HEAD --oneline --grep="0176|deploy|deployment"` returns nothing. `git diff v1.4.6..HEAD --stat` shows only Services/Models/Commands/Templates/Docs/Tests changes; no installer or version-bump artefacts. The deployment gap (running pre-fix watchdog binary on a host that committed PR1+PR2+PR3) is correctly framed as a runtime/process gap that balazs handles by reinstalling the dotnet tool — not as a code defect.

#### 12. agent-crashes.md narrative is internally consistent across the survivorship-bias correction
- **Concern:** G
- **Classification:** clean
- **Severity:** —
- **Evidence:** The inquisition file has four superimposed sections (Brian 2026-05-06, methodology caveat appended via `cdaf920`, Dexter 2026-05-08 followup with 84-89% reframing, Charlie/Frank judges 2026-05-08). No claim contradicts another:
  - Brian's "75%" is preserved as a historical claim — the methodology caveat at `:294-302` explicitly bounds it (`"correct only as the success rate of the resumes the watchdog actually attempted — not the crash-recovery rate from the user's perspective"`).
  - Dexter's followup 84-89% measures the same denominator (attempts) on a larger sample (n=19) — directly comparable to Brian's 75% (n=4) as a refinement, not a contradiction.
  - Frank's 81% measures a different metric: of `resume_blocked` log lines, the FP rate (lines logged for sessions that did successfully recover). Lives at `:974`. Distinct from the recovery rate. Labelled clearly in Frank's summary.
- The convention of appending corrections rather than retroactively editing the original is appropriate for an inquisition log; future readers can trace the evolution. Internally consistent.

### Hypotheses tested and not reproduced

- **Cross-PR timing race producing a *new* defect (Concern A):** walked the failed and gave_up paths in detail looking for an interaction the PR3 review missed. The Saturate-clears-LastResumeLaunchedAt invariant holds across all consumers and the existing pre-PR3 race window (Finding #2 above) is unchanged in shape — slightly wider footprint, same severity envelope. No new failure mode introduced.
- **State parser silently dropping unknown new fields on round-trip:** confirmed parser is dict-keyed (no panic on unknown fields, no panic on missing fields). Both directions BC-safe.

### Confidence: high (Concerns A, B, C, D, E, G); medium-high (Concern F)

Concerns A/B/C/D/E/G covered by direct code inspection plus targeted pin tests. Concern F took the user's HEAD baseline (`4194/4194`, `141/141`) on faith rather than re-running — partial, but the full suite ran at HEAD just before this audit, the targeted resume slice ran clean here, and `dotnet build` + `dydo check` (both system and dev) at HEAD are 0/0.

What was NOT examined thoroughly:
- Linux/Mac platform-specific behaviour of TeardownWorktree (junction handling is Windows-specific; the code handles the symlink case at `Commands/WorktreeCommand.cs:769` but a live cross-platform smoke wasn't run from this audit — out of scope for a single-machine pre-tag audit and consistent with prior inquisition coverage caveats).
- Live reproduction of the timing race in Finding #2 (microsecond window; would require an instrumented test harness).
- gave_up tick-check on a real long-lived watchdog (the BC + cross-PR walk is from code; integration evidence comes from PR3's test pins which I sampled).

### TAG-READINESS VERDICT — GO

All critical concerns (A, B) are definitive: no new cross-PR interaction defects introduced; backward compatibility preserved across the v1.4.6→v1.4.7 boundary (audit JSON, state.md, dydo.json nudges, stale tasks/_index.md). Source-of-truth tests pass. Two minor non-blocking observations:

1. **Pre-existing Saturate-vs-claim race** (Finding #2) — latent, low-severity, not introduced by these PRs but slightly widened by PR3's added gave_up call site. Track in a v1.4.8 follow-up issue post-tag; not a tag blocker.
2. **DeleteStaleTasksIndex banner-gated deletion lacks a direct test pin** (Finding #7) — small coverage gap; correctness by inspection. Track in a v1.4.8 follow-up; not a tag blocker.

No must-fix items. **balazs is clear to push v1.4.7.**

### Judge rulings (Emma — 2026-05-08)

Independent review of all 12 findings against the cited evidence and the source code at HEAD (79489d5).

#### Finding #1 — Cross-PR resume paths thread cleanly
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/WatchdogService.cs (lines 475-555, 600-638, 662-682), Services/AgentRegistry.cs (lines 211-219, 332-360, 1640-1710, 1810-1843), Services/RecoveryClassifier.cs (full file), Services/TerminalLauncher.cs (LaunchResume — opened to confirm watchdog state is not touched).
- **Independent verification:** Walked the three paths myself. Confirmed `SaturateResumeAttempts` (`AgentRegistry.cs:1694-1709`) sets attempts=cap AND clears LastResumeLaunchedAt in a single locked write. Confirmed `TryReadResumeContext` (`WatchdogService.cs:579`) returns null on `attempts >= cap`, so `IsBadSessionFailFast` and `IsLaunchedClaudeStillAlive` are unreachable post-Saturate — Dexter's structural-bypass claim holds. Confirmed gave_up tick-check (`:608-638`) reads attempts/lastResumeAt with explicit null guards (`:625`) before emitting. Confirmed `HandleExistingSession` (`AgentRegistry.cs:340-359`) calls ResetResumeBookkeeping before EmitAutoRecovery so the disk state matches the audit pair.
- **Alternative explanations considered:** Could the gave_up tick-check fire AFTER a failed-path Saturate has cleared LastResumeLaunchedAt? No — gave_up's predicate at `:625` requires `lastResumeAt != null`, which Saturate has just nulled. Mutually exclusive per episode, exactly as Dexter claims.

#### Finding #2 — Pre-existing Saturate-vs-claim race
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/WatchdogService.cs (lines 484-491, 501-516, 563-598, 608-638), Services/AgentRegistry.cs (lines 211-219, 332-360, 1694-1709).
- **Independent verification:** Confirmed `TryReadResumeContext` releases the per-agent lock in its `finally` block (`:594-597`) before returning, and `TryReadGaveUpContext` does the same (`:634-637`). The Saturate calls at `:490` and `:503` then re-acquire the lock fresh inside `SaturateResumeAttempts` (`AgentRegistry.cs:1696`). Between release and re-acquire a competing `ClaimAgent` → `HandleExistingSession` → `ResetResumeBookkeeping` (`:211-219`, sets attempts=0) can land. The watchdog's stale Saturate then writes attempts=cap on a freshly-claimed agent. Pre-PR3 the same window existed via the failed-path Saturate; PR3 adds one more call site (gave_up). Race is real and slightly wider in v1.4.7 than v1.4.6.
- **Alternative explanations considered:** Could the second lock acquire snapshot a fresh state and bail? No — `SaturateResumeAttempts` (`:1700-1704`) reads state, mutates `ResumeAttempts` and `LastResumeLaunchedAt` unconditionally, then writes. No precondition check on `Status` or `Since`. The bug is in the absence of revalidation, not in the lock itself.
- **Issue:** #0181

#### Finding #3 — LastResumeLaunchedAt readers are all null-safe
- **Judge ruling:** CONFIRMED
- **Files examined:** RecoveryClassifier.cs (lines 32, 51-52), WatchdogService.cs (lines 588, 622-626, 662-667, 678-682). Independently grepped `LastResumeLaunchedAt|LastResumeAt` across the whole tree to verify Dexter's reader enumeration is exhaustive.
- **Independent verification:** The grep returned only the readers Dexter cited plus mutation sites at `AgentRegistry.cs:216` (ResetResumeBookkeeping), `:414` (claim), `:578` (release), `:1648` (Increment), `:1702` (Saturate), `:1740` (state-file format), `:1825` (parser). Mutation sites don't read; reader sites are the same six Dexter listed and all use either `?.`, `.HasValue && ...`, or an explicit `if (... == null) return null;` guard before dereferencing.
- **Alternative explanations considered:** None — exhaustive grep, all readers null-safe.

#### Finding #4 — AuditEvent backward-compat pin test exists and is green
- **Judge ruling:** CONFIRMED
- **Files examined:** DynaDocs.Tests/Models/AuditEventBackwardCompatTests.cs (full file, 119 lines).
- **Independent verification:** Read the full test file. Confirmed exactly five test methods at the cited line ranges (`:18-38`, `:41-54`, `:57-76`, `:79-96`, `:99-117`). Each test's assertions match Dexter's claim — pre-PR3 JSON shape deserializes with the three new fields null, `JsonIgnoreCondition.WhenWritingNull` keeps null fields out of emitted JSON, all three buckets (fresh/auto/manual) round-trip. The test file's xmldoc explicitly notes balazs requested this pin.
- **Alternative explanations considered:** Did not re-run the 30+ minute full suite. Took Dexter's 54/54 filtered run on faith — the inquisitor's run-line evidence is concrete and the targeted slice is small enough that a re-run would replicate.

#### Finding #5 — Pre-PR1 state.md parses cleanly; legacy fall-through preserved
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/AgentRegistry.cs (lines 1810-1843, the `StateFieldParsers` dictionary), Services/WatchdogService.cs (lines 662-667), DynaDocs.Tests/Services/WatchdogServiceTests.cs (lines 1955-1978).
- **Independent verification:** Read the parser. `StateFieldParsers` is a `Dictionary<string, Action<AgentState, string>>`; missing field lines are simply not invoked, leaving defaults. The `launched-pid` parser (`:1832-1836`) explicitly tolerates `"null"` and `""`. `IsBadSessionFailFast` at `:662-667` requires `ctx.LaunchedPid.HasValue` for the dead-launched-PID branch but falls through `(!ctx.LaunchedPid.HasValue || ...)` to the wall-clock-only path when null. Pre-PR1 state.md has no `launched-pid` line → LaunchedPid stays null → fall-through path matches pre-fix behaviour.
- **Alternative explanations considered:** Could the parser panic on unrecognised fields if a v1.4.7 produces something v1.4.6 can't read? No — `StateFieldParsers` is keyed by string; unknown keys are silently dropped (the parsing loop is `if (parsers.TryGetValue(...))`). Both directions are BC-safe.

#### Finding #6 — EnsureDefaultNudges adds the until-poll nudge idempotently
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/ConfigFactory.cs (lines 76-81, 116-140), DynaDocs.Tests/Services/ConfigFactoryTests.cs (lines 295-333).
- **Independent verification:** Read the function. Dedupe key is `n.Pattern` via `HashSet<string>` (`:122`); the new nudge's pattern `\buntil\s+\[` is just another entry, no special-casing. Idempotency test at `:321-333` constructs a default config, asserts the nudge appears once, runs `EnsureDefaultNudges` again, and asserts no addition.
- **Alternative explanations considered:** Could the dedupe key collide with another default nudge's pattern? Searched all `DefaultNudges` patterns in `ConfigFactory.cs:50-81` — six distinct patterns, no overlap.

#### Finding #7 — DeleteStaleTasksIndex correctly banner-gates deletion; no test pin
- **Judge ruling:** CONFIRMED
- **Files examined:** Commands/FixHubHandler.cs (lines 138-150), Services/HubGenerator.cs (line 13). Grepped `DeleteStaleTasksIndex|StaleTasksIndex` across DynaDocs.Tests/ — no matches.
- **Independent verification:** The function reads the file content, checks `Contains(HubGenerator.AutoGenComment)` (`:145`), and only deletes on banner match. Hand-written `_index.md` files are preserved. The single-branch logic is correct by inspection. The test gap is real — no direct deletion-path coverage.
- **Alternative explanations considered:** Is the banner check vulnerable to substring-spoof in a hand-written file? In principle yes — if a user copies the auto-gen banner verbatim into a hand-written `_index.md`, dydo fix would delete it. This is a defensible design (the banner is explicitly documented as the auto-gen marker), but a stricter check (e.g. file ending with banner) would be safer. Out of scope for the current finding.
- **Issue:** #0182

#### Finding #8 — until-poll nudge regex is correct; brief's prose is wrong
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/ConfigFactory.cs (line 78), DynaDocs.Tests/Services/ConfigFactoryTests.cs (lines 295-319).
- **Independent verification:** Pattern `\buntil\s+\[` matches both `until [` and `until [[` (regex `[` matches the first `[` of `[[`). Both forms are open-ended bash polls — matching is the correct behaviour. Tests cover single-bracket positive (`:295-306`) and non-bracket negatives (`:308-319`). Dexter's empirical FP probe (19 cases) is consistent with my reading.
- **Alternative explanations considered:** Could the nudge fire spuriously on a benign `until` keyword followed by `[`? Searched: bash's `until` is always a control-flow keyword followed by a command list; the only pattern with `[` next is `[ ... ]` (single-bracket test) or the double-bracket extended-test form — both polls. The regex correctly excludes `until ! ping`, `until cmd`, `until something_else`. No legitimate FP shape found.
- **Issue:** none — the docs-gap is in Dexter's *brief* (downstream of this audit, not in v1.4.7 commits). The brief is ephemeral; no need to file an issue against an inquisitor's working note.

#### Finding #9 — TeardownWorktree cleanup-log is honest at the Directory.Exists boundary
- **Judge ruling:** CONFIRMED
- **Files examined:** Commands/WorktreeCommand.cs (lines 332-339, 740-754, 756-776, 785-804, 819-845, 1040-1052).
- **Independent verification:** Read the function. `TeardownWorktree` (`:746-754`) returns `!Directory.Exists(worktreePath)` after the cleanup steps. Both call sites — `cleanup` at `:332-339` and `prune` at `:1046-1052` — branch the user-facing log on the bool with truthful messages ("cleaned up." vs "directory remains; will retry on next prune"). `RemoveJunction` uses `cmd /c rmdir` without `/s` on Windows — removes the junction, not the target. `DeleteDirectoryJunctionSafe` checks `FileAttributes.ReparsePoint` and uses `recursive: false` for reparse points. `RemoveZombieDirectory` writes `WARNING:` to stderr (`:842`) and returns false on exception so the final `Directory.Exists` reflects disk truth.
- **Alternative explanations considered:** Could `Directory.Exists` lie on Windows under DELETE_PENDING? Possible — but the log says "directory remains, will retry on next prune," which is the correct user-facing message regardless of whether the directory is genuinely there or in DELETE_PENDING limbo. Honest at the boundary.

#### Finding #10 — Integration smoke clean at HEAD (with one post-audit caveat)
- **Judge ruling:** CONFIRMED at audit time; one new dydo-check FP introduced by the audit text itself, see post-audit observation below
- **Files examined:** Verified with `dotnet build` (`0 Warning(s), 0 Error(s)`) and `dydo check` at HEAD.
- **Independent verification:** Re-ran `dotnet build` — clean. Re-ran `dydo check` — see post-audit observation below.
- **Alternative explanations considered:** Could the test slice's "Tests passed" emission mask a failure? Took the `run_tests.py` framing on faith; the prior baseline (4194/4194) was confirmed by balazs.

#### Finding #11 — No v1.4.7 commit attempts to address #0176
- **Judge ruling:** CONFIRMED
- **Files examined:** `git log v1.4.6..HEAD` (11 commits), `git log v1.4.6..HEAD --grep="0176|deploy|deployment"` (empty).
- **Independent verification:** Re-ran both commands. Eleven commits since v1.4.6, none reference the deployment gap. The runtime/process gap (running the pre-fix binary on the dev host) is correctly framed as a deployment concern outside the v1.4.7 commit scope.
- **Alternative explanations considered:** Could a commit silently address #0176 without a grep-match keyword? Inspected the commit titles — all six PR commits are clearly about resume_blocked / worktree anchors / RecoveryClassifier / log truthfulness / nudges / docs. No installer or version-bump artefact.

#### Finding #12 — agent-crashes.md narrative is internally consistent
- **Judge ruling:** CONFIRMED
- **Files examined:** This file (full read across the four superimposed sections).
- **Independent verification:** Brian's 75% (n=4), Dexter's 84-89% (n=19), Frank's 81% are not contradictions — Brian and Dexter measure the same denominator (resume attempts) and Dexter's 84-89% is a refinement on a larger sample. Frank's 81% measures a different metric (resume_blocked log-line FP rate) and is labelled distinctly. The methodology caveat (commit `cdaf920`) bounds Brian's claim explicitly.
- **Alternative explanations considered:** Could a future reader confuse the three percentages? The caveat at `:294-302` and Frank's section labelling are explicit — confusion is unlikely.

### Post-audit observation — `dydo check` introduced FP in audit text

When I re-ran `dydo check` at HEAD as part of validating Finding #10, it returned an error on line 1070 of this file: `Wikilink found: <double-left-bracket … double-right-bracket>` (1 error, 0 warnings in 950 files).

Dexter's audit text on line 1070 illustrates the until-poll nudge's matches with a literal double-bracketed extended-test token inside backticks. `Rules/RelativeLinksRule.cs:18-20` does not exempt code-spans, so the double-bracket-pair literal is parsed as a wikilink and reported as an error.

This is downstream of the v1.4.7 commits — the FP is in the audit document text, not in the code under audit. But it does mean that when this worktree's inquisition file lands on master, `dydo check` on master will turn red on a doc-text FP. **balazs should either edit line 1070 to avoid the bracket-pair illustration before merging, or accept a transient `dydo check` failure on master until the rule is taught about code-spans.**

Code verdict is unaffected. Tag-readiness unchanged.

### TAG-READINESS VERDICT — GO (independent confirmation)

**Emma's independent verdict: GO. balazs is clear to push v1.4.7.**

All 12 findings independently verified. Two confirmed non-blocking items have been escalated to issue tickets:

1. Finding #2 → issue #0181 (pre-existing Saturate-vs-claim race; v1.4.8).
2. Finding #7 → issue #0182 (DeleteStaleTasksIndex banner-gated deletion test pin; v1.4.8).

Finding #8's docs-gap is in Dexter's brief (working note), not in v1.4.7 commits — no issue filed.

**Pre-merge note for balazs:** the audit text introduced a `dydo check` Wikilink FP at line 1070 of this file. Edit before merging the worktree if you want master's `dydo check` to stay green. This does not affect the v1.4.7 release artefact.

