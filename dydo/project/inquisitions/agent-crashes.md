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

