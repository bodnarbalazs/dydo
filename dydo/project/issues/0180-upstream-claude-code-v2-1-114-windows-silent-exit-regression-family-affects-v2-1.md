---
id: 180
area: backend
type: issue
severity: low
status: open
found-by: inquisition
date: 2026-05-08
---

# Upstream: Claude Code v2.1.114+ Windows silent-exit regression family — affects v2.1.132 sessions; not actionable from dydo

Tracking pointer to the upstream Claude Code v2.1.114+ Windows silent-exit family (no Application Error/WER event); fd17b834 mapped to this on 2026-05-07.

## Description

This is a **status: external, not actionable from dydo** tracking issue. Filed so future inquisitors do not re-derive the upstream attribution from scratch.

A regression introduced in Claude Code **v2.1.114** (≈2026-04-18; last working: v2.1.113) causes the CLI to silently exit on Windows during long-running sessions, with **no `Application Error` / `Windows Error Reporting` event logged** by the OS. Confirmed present through v2.1.132 (the version Charlie's session `fd17b834` ran on 2026-05-07); local environment is currently v2.1.133 — the v2.1.130–v2.1.133 changelog contains no entry that targets this regression family directly. **No dydo-side fix possible.**

### Pattern signatures observed

- Long-running session (10–30+ minutes) with dense Bash subprocess invocations, OR a long-running judge / planning turn
- Session goes idle (Claude Code may emit a `system / subtype: away_summary` JSONL entry as an *indicator*; the away_summary is a recap feature, **not** the cause of exit)
- claude.exe / node.exe exits silently within ~2–5 minutes of the last user-visible turn
- No `Application Error` (event ID 1000), no `Windows Error Reporting` (event ID 1001), no `.NET Runtime` (event ID 1023) entry in the Application event log for claude.exe / node.exe in the death window
- The session JSONL is fully flushed; `claude --resume <sessionId>` recovers the conversation (cwd-sensitive — must be invoked from the same cwd the session was claimed in)

### Upstream issues (cross-link)

- [`anthropics/claude-code#15001`](https://github.com/anthropics/claude-code/issues/15001) — "Silent Crash Due to Memory Exhaustion from Unbounded Command Output." Closed-as-duplicate. Reporter: *"No Windows Event Log entries for the crash (silent termination)."*
- [`anthropics/claude-code#50299`](https://github.com/anthropics/claude-code/issues/50299) — "Claude Code exits without warning mid-session (Windows)." Heavy multi-file edit workflow, claude-opus, no error / no event-log trace. Closed-as-duplicate.
- [`anthropics/claude-code#55424`](https://github.com/anthropics/claude-code/issues/55424) — "v2.1.121 Windows: Claude Code REPL silently exits inside PowerShell host during long-running agent + dense Bash subprocess chain." Open. Reporter notes ~50+ Bash subprocess invocations precede silent exit.
- [`anthropics/claude-code#55562`](https://github.com/anthropics/claude-code/issues/55562) — "Claude Code Unusable on Windows (Max Subscription)." Open (marked as duplicate). Establishes v2.1.114 as regression introduction; v2.1.113 last working.

### dydo-side incident reference

- Inquisition `dydo/project/inquisitions/agent-crashes.md`, 2026-05-08 — Dexter — "Silent-death root-cause investigation," Findings #1 and #2.
- Charlie session `fd17b834-85dd-47d9-8713-da618d840467` (2026-05-07 23:01–23:16Z): judge turn that posted a verdict at 23:10:41Z, idled for ~3 minutes (final JSONL entry: `away_summary` at 23:13:45.556Z), then claude.exe silently exited. Watchdog detected dead PID at the next tick (23:16:20Z, ≤10s detection latency); the subsequent `resume_blocked: no_refresh_after_warmup` is #0173-class noise unrelated to the upstream crash.

## Reproduction

Not reproducible on demand from the dydo side; pattern-matched against the upstream issues. If the regression survives a future Claude Code release, file the dydo-side incident reference in this issue rather than opening a duplicate.

## Resolution

Closes when an upstream Claude Code release (≥ v2.1.134) ships the fix. Track via the upstream issues above. Until then: status `external`, severity `low`. **Do not allocate dydo work to this.**
