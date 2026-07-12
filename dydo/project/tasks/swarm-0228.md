---
area: general
name: swarm-0228
status: pending
created: 2026-07-12T13:13:54.5745383Z
assigned: Quinn
needs-human: false
---

# Task: swarm-0228

CODEX swarm fix — issue 0228 (MEDIUM). Self-contained; report then RELEASE YOURSELF (codex msg delivery isn't wired; uncommitted work persists for the chief-of-staff to sequence). Do NOT run the python test gates (0282 - the Claude reviewer runs them); make the code compile + reason correctness.

READ: dydo/project/issues/0228-*.md for full detail.

ISSUE: WatchdogService.IsWatchdogProcess does a full command-line scan (spawning/reading process cmdlines) that adds ~350ms to EVERY release + auto-close. Replace the expensive full-cmdline scan with a cheaper in-process check (e.g. an in-proc PEB/parent-PID/own-process-identity read) that identifies the watchdog process without the per-call cmdline enumeration cost.

FILES (stay within these): Services/WatchdogService.cs (IsWatchdogProcess + callers) and Services/DispatchService.cs (if it calls it on the release/auto-close path). Add/extend tests in the matching DynaDocs.Tests/Services/WatchdogServiceTests.cs proving the cheaper check still correctly identifies watchdog vs non-watchdog processes.

REQUIRED: implement the cheaper identity check preserving correctness (must NOT misidentify a non-watchdog as watchdog or vice-versa); a test covering both classifications; note the perf rationale in a comment. Message Adele (dydo msg --to Adele --subject swarm-0228) with files, the new check, the test, ~time, any prompt. THEN release yourself.

CONSTRAINTS: do NOT touch Commands/WorktreeCommand.cs, Services/AgentClaimValidator.cs, or Services/AgentRegistry.cs (parallel swarm agents own those). DispatchService.cs is shared with care - only touch its IsWatchdogProcess call site if needed, flag Adele if you must change more. Under the dydo guard + auto mode.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

(Pending)