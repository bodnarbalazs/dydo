---
title: Codex first-class paths lack end-to-end regression coverage
id: 233
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
found-by-agent: Adele
found-by-vendor: unknown
found-by-model: unknown
date: 2026-07-07
---

# Codex first-class paths lack end-to-end regression coverage

Current tests pass while Codex claim hooks, watchdog anchoring, legacy wait ownership, and skill-only sync paths remain weakly covered.

## Description

Inquisition coverage finding: The suite has useful host/model unit and integration checks, but several Codex first-class paths are only indirectly covered. Missing tests: feed a Codex-shaped guard stdin payload for dydo agent claim auto and assert host/model survive into pending/claimed session; dispatch/ensure watchdog from a Codex-owned session and assert a Codex host anchor is registered; write a legacy Codex .session with null ClaimedPid and assert WaitCommand.ResolveHostLivenessPid uses Codex ancestry; assert sync emits skill-only artifacts for planner/orchestrator/co-thinker/chief-of-staff without accidental .codex/agents role files. Consequence: green tests can miss Codex host/model regressions and workflow artifact drift.

**Update 2026-07-08 (adopt-orphaned-codex-slices):** the watchdog resume path now explicitly rides on this issue's live-Codex smoke test. 0231's fix emits the documented `codex resume <session-id> <prompt>` subcommand form, but nothing in the repo establishes that Codex hook payloads deliver a `session_id` at all, nor that the delivered value is a codex-resumable session id — dydo stores the hook payload's `session_id` verbatim (see backlog `cross-vendor-agent-integration`, DR 037 "Codex guard adapter is follow-up work"). The smoke test is first in the v2.0.6 adoption sequence: claim an agent from a live Codex session, kill it, and verify the watchdog resume actually reattaches. If the payload lacks a resumable id, the per-vendor session-id provenance design question goes to the human gate list (CoS decision 2026-07-08). Partial progress on the original asks: launcher/dispatch resolution and watchdog host-threading now have real regression tests (de0d63f); the guard-stdin claim, watchdog anchoring, legacy wait ownership, and skill-only sync asks remain open.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Resolved by c1-7 (`sprint-tasks/c1-7-codex-e2e-coverage.md`). All four originally-named asks now
have end-to-end regression cover, plus the C1 seams the sprint built. New test files (no edits to
any C1 slice's own tests):

- `DynaDocs.Tests/Integration/CodexClaimE2ETests.cs`
  - **Ask 1 (codex-shaped guard-stdin claim):** a hook payload with session_id, a `.codex`
    transcript path, an explicit model, and NO agent_id/agent_type is fed through `dydo agent
    claim` — host (`codex`) and model survive into the claimed session and reach the whoami surface.
  - **Ask 2 (watchdog anchoring):** claiming a codex-owned session registers a watchdog anchor
    keyed to the codex host ancestor (`FindAgentHostAncestor("codex")`), proven with an override
    that resolves only the codex lookup. **RESIDUAL (named by the wave-4 sprint audit): this
    covers CLAIM-time anchoring only.** The dispatch-path re-registration
    (`WatchdogService.EnsureRunning`, WatchdogService.cs:123) is hardcoded claude-only and
    untested for codex — the literal "dispatch/ensure watchdog" surface of this ask transfers to
    issue 0267 (bounded impact: claim-time anchoring keeps the standard flow covered).
  - **Ask 3 (legacy wait ownership):** a legacy codex `.session` with null `ClaimedPid` →
    `WaitCommand.ResolveHostLivenessPid` falls back to the codex ancestry walk, never the claude one.
- `DynaDocs.Tests/Integration/CodexSyncArtifactsE2ETests.cs`
  - **Ask 4 (skill-only sync):** `dydo sync` emits skill-only artifacts for planner + the three
    Tier-1 manager modes on both surfaces (`.claude/skills`, `.agents/skills`) with NO
    `.codex/agents/<role>.toml` and NO `.claude/agents/<role>.md` — closing the codex-side drift the
    issue named (prior tests only pinned the `.claude/agents` absence).
- `DynaDocs.Tests/Integration/CodexReleaseLoopE2ETests.cs` — the full 0254 loop on a codex host:
  claim (codex-shaped payload) → role → durable wait `--register` → message arrives → `dydo read`
  → `inbox clear --all` → release clears the durable marker. The "can release" acceptance criterion.
- `DynaDocs.Tests/Integration/CodexDispatchPostureE2ETests.cs` — 0253 posture emission end-to-end
  (configured `--sandbox workspace-write --ask-for-approval on-request`, bypass flag NEVER present)
  and the DispatchPreflight failure matrix (vendor-disabled, missing tier, invalid posture, missing
  Windows sandbox prerequisite) each surfacing its actionable message with no reservation left
  behind. The executable-unresolvable and untrusted-hooks modes were already e2e-covered in
  `DispatchCommandTests` (c1-4's file), so they are not re-added here.

Gates green: `python DynaDocs.Tests/coverage/run_tests.py` + `gap_check.py --force-run`.

**Resume ask transfers to c1-8 (not simulatable here).** The 2026-07-08 update tied the watchdog
resume path to a live-codex smoke: claim from a live codex session, kill it, verify the watchdog
resume actually reattaches — and whether a codex hook payload even delivers a codex-*resumable*
session id. Nothing in-repo can establish that (no live codex binary; hook payloads and PATH shims
only). That verification is c1-8's live checklist, paired with the v2.0.7 release candidate. If the
delivered `session_id` is not codex-resumable, the per-vendor session-id provenance design question
goes to the human gate list (CoS decision 2026-07-08).
