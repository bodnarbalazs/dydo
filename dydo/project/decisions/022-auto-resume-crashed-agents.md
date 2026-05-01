---
area: project
type: decision
status: accepted
date: 2026-04-29
---

# 022 — Auto-Resume Crashed Agents

When the watchdog detects that an agent's claude process has died while `state.md` still says `working`, it relaunches the same agent with `claude --resume <sessionId>` and a short continuation prompt instead of leaving the terminal stuck. Bounded to three respawns per crashed session; on the fourth crash the agent stays crashed.

This composes with — and depends on — Decision 021 (unified general wait): the resume launcher reuses the same backgrounded `dydo wait` startup pattern fresh launches use, so the resumed claude session has a live general wait without having to re-arm it itself.

---

## Context

Today, when claude crashes inside a dispatched terminal:

- `state.md` keeps saying `status: working`, `released: false`.
- The terminal stays open with the last claude output visible (Windows: `-NoExit` per `WindowsTerminalLauncher.cs:23`; Linux: `exec bash` fallback).
- The general wait child process (a separate `dydo wait` invocation) self-terminates when its claude ancestor is gone (`Commands/WaitCommand.cs:114`).
- Nothing relaunches claude. The user sees a dead tab and has to either redispatch (fresh agent, lost context) or do nothing (work stuck).

Two facts make resume tractable rather than aspirational:

1. **The claude session ID is already captured.** `Models/AgentSession.cs:8` — `AgentSession.SessionId` is the session ID Claude Code provides via its hook payload. Every claim writes it to `dydo/agents/<name>/.session`. We don't need to add capture; we need to read the file.
2. **Resume preserves dydo identity by construction.** `Services/AgentRegistry.cs:305` short-circuits `HandleExistingSession` when an incoming claim's session ID matches the stored one. `claude --resume` passes the same session ID, so the hook resolves to the same agent and the reclaim/archive path (#0130) does not fire. The agent's role, task, workspace, and notes are intact.

The remaining problem is therefore narrow: detect the crash, relaunch claude in resume mode, re-establish the wait, and cap retries.

## Decision

### Detection

Extended in the existing watchdog poll loop. `WatchdogService.PollAndCleanupForAgent` already iterates every agent's `state.md` every ~10 s. A new sibling check fires when:

- `state.md` has `status: working` (i.e. the agent has not released);
- the `.session` file's `ClaimedPid` is not a live process;
- `resume-attempts` (new field, see below) is less than the cap.

When all three hold, the watchdog launches a resume terminal for that agent and increments `resume-attempts`. The existing stale-working reclaim path in `AgentRegistry` remains unchanged — it covers the case where this resume mechanism eventually gives up (cap reached) and a fresh claim arrives.

### Resume launch

A new launcher path in `WindowsTerminalLauncher`, `LinuxTerminalLauncher`, `MacTerminalLauncher`, parametrised by `(agentName, claudeSessionId)`. The script body is the same shape as the normal launch path, with two changes:

- `claude '<Name> --inbox'` becomes `claude --resume <sessionId> '<continuation-prompt>'`.
- The backgrounded `dydo wait` startup the unified-general-wait work introduces (Decision 021) runs unchanged. Re-armed wait is a free byproduct of using the same launcher scaffolding.

The continuation prompt is short and identity-agnostic — claude already knows who it is from the resumed conversation:

> Your terminal tab crashed and you have been auto-resumed. Your dydo identity, role, and task are unchanged. Re-orient briefly from your most recent context and continue from where you left off.

### Retry cap

A new `resume-attempts: N` field in `state.md`. Watchdog increments it before each resume launch. At cap (default **3**), watchdog stops respawning and the agent stays crashed. The field resets to 0 on `dydo agent claim` (i.e. when the human or the workflow reclaims fresh) and on `dydo agent release`.

Three is high enough to absorb transient crashes (a one-off OOM, a flaky terminal) and low enough that a poisoned context-window state — which would re-crash on every resume by reloading the same overflowed conversation — exits the loop within a minute or two of polling.

### What we explicitly do **not** handle in this decision

- **Distinguishing `/exit` from a crash.** Per user practice, `/exit` is not in use; users terminate via Ctrl-C or by closing the tab, both of which look like crashes and should be auto-resumed. If a user does invoke `/exit` deliberately, the cap will catch it within three respawns and the user can `dydo agent release` cleanly.
- **Detecting context-window-overflow specifically.** The retry cap subsumes this — overflow crashes hit the cap fast, no special-case detection required.
- **Opt-in flag.** The behaviour is on by default for any agent with a captured `.session` file. Adding a flag is straightforward later if needed; we don't ship one preemptively.

## Consequences

### Code changes

- **`Services/WatchdogService.cs`** — new method (e.g. `PollAndResumeForAgent`) called from `PollAndCleanup`'s per-agent loop, alongside the existing auto-close pass. Reads `.session` and `state.md`, writes back the incremented `resume-attempts`, calls into the launcher.
- **`Services/WindowsTerminalLauncher.cs`, `Services/LinuxTerminalLauncher.cs`, `Services/MacTerminalLauncher.cs`** — new entry point (e.g. `LaunchResume`) that builds a `claude --resume <id> '<prompt>'` script and reuses the existing dispatch script body for everything else.
- **`Services/TerminalLauncher.cs`** — façade routing for the new path; keeps the per-OS dispatch consistent with `LaunchNewTerminal`.
- **`Models/AgentState.cs` (or wherever `state.md` parsing lives)** — new `resume-attempts` field; default 0; reset on claim/release.
- **`Services/AgentRegistry.cs`** — clear `resume-attempts` in the claim and release write-state paths.
- **Tests** — unit tests for the watchdog detection trigger, retry cap, prompt content, and the launcher arguments. Integration test that simulates a crash (kill `.session.ClaimedPid`) and asserts a new launch was issued via the test-hook overrides already in place (`WatchdogService.StartProcessOverride`, `FindProcessesOverride`).

### Process changes

None for users. Crashes that previously left a dead tab now re-spawn a continuing one, up to three times. The continuation prompt is the only new surface a resumed agent sees.

### Migration

Additive, no breaking change. Ships in v1.4.0 alongside the unified-general-wait work (Decision 021), or in v1.4.1 if 021 lands first; the dependency is one-way (this decision relies on 021's launcher pattern, not the other way around).

### Re-evaluate

After ~2 weeks of lived practice:

- **Cap of 3** — verify it absorbs real-world transient crashes without masking systemic issues (e.g. an agent looping crash-resume-crash burns three respawns and then is silently stuck).
- **Continuation-prompt content** — adjust if resumed agents are mis-orienting (forgetting to re-read recent state, repeating already-completed work).
- **No-flag default-on stance** — revisit if a class of agent emerges for which resume is the wrong default.

---

## Affects

- `Services/WatchdogService.cs` — new resume-detection + launch trigger pass.
- `Services/WindowsTerminalLauncher.cs` — `LaunchResume` path.
- `Services/LinuxTerminalLauncher.cs` — same.
- `Services/MacTerminalLauncher.cs` — same.
- `Services/TerminalLauncher.cs` — façade routing.
- `Services/AgentRegistry.cs` — reset `resume-attempts` on claim/release.
- `Models/AgentSession.cs` — referenced (no edit) for session ID source.
- [Decision 021 — Unified General Wait](./021-unified-general-wait.md) — provides the backgrounded `dydo wait` startup pattern this decision reuses.
- [Issue #0130 — Stale-Working Reclaim Silently Archives In-Flight Work](../issues/0130-stale-working-reclaim-silently-archives-in-flight-work.md) — companion: this decision avoids the archive path for transient crashes; #0130's reclaim path remains the fallback for crashes past the cap.
