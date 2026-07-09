---
title: c1-7 Codex Paths E2E Regression Coverage
blocked-by: c1-1-read-verb, c1-2-durable-wait, c1-3-codex-posture, c1-4-dispatch-preflight, c1-5-role-validation, c1-6-model-provenance
due:
needs-human: false
priority: Medium
sprint: c1-codex-adoption
status: ready
work-type: test
area: backend
type: context
---

# c1-7 Codex Paths E2E Regression Coverage

Issue 0233 (in-flight): green tests currently miss codex host/model regressions and workflow
artifact drift. Partial progress landed with adopt-orphaned-codex-slices (launcher/dispatch
resolution + watchdog host-threading, commit de0d63f); this slice delivers the four still-open
asks plus regression coverage over the seams C1 just built. Live ground truth: the 2026-07-09
smoke (claim-via-hook CONFIRMED; resume still untested — that part is c1-8's, not simulatable).

## Tests to add (0233's open asks)

1. **Codex-shaped guard stdin claim:** feed a codex-shaped hook payload (shape per Noah's probe
   findings in `backlog/codex-mcp-delegation-experiment.md`: session_id, transcript_path into
   the vendor sessions dir, model field, NO agent_id/agent_type) through `dydo agent claim auto`
   and assert host/model survive into the pending/claimed session.
2. **Watchdog anchoring from a codex-owned session:** dispatch/ensure watchdog and assert a
   codex host anchor is registered.
3. **Legacy wait ownership:** a legacy codex `.session` with null `ClaimedPid` →
   `WaitCommand.ResolveHostLivenessPid` uses codex ancestry.
4. **Skill-only sync artifacts:** `dydo sync` emits skill-only artifacts for the Tier-1 modes
   without accidental `.codex/agents` role files.

## Tests to add (C1 seams, end-to-end shaped)

5. The full 0254 loop as an integration test: claim (codex-shaped payload) → role → durable wait
   registered → message arrives → `dydo read` → `inbox clear --all` succeeds → release removes
   the marker. This is the "can release" acceptance criterion as a regression test.
6. Launch command line: dispatch toward codex produces the configured posture flags (never the
   bypass flag) end-to-end through `DispatchService` → launcher.
7. Preflight matrix: each c1-4 failure mode surfaces its actionable message and leaves no
   reservation.

## Files

- NEW files only (collision-free by construction): `DynaDocs.Tests/Integration/CodexClaimE2ETests.cs`,
  `CodexReleaseLoopE2ETests.cs`, `CodexDispatchPostureE2ETests.cs`, and additions ONLY where an
  ask has an obvious existing home with no C1-slice edits (verify before touching; when in doubt,
  new file).
- Follow `IntegrationTestBase`/`IntegrationTestCollection` patterns; no live codex binary — hook
  payloads and PATH shims, per existing `TerminalLauncherTests` fixtures.
- Issue 0233: fill Resolution, move to `resolved/` (the resume ask transfers to c1-8's live
  checklist — note that explicitly in the Resolution).

## Gates (exact commands)

- `python DynaDocs.Tests/coverage/run_tests.py`
- `DynaDocs.Tests/coverage/gap_check.py --force-run`
- `dydo check`

## Sequencing

**After all code slices** (c1-1 … c1-6) — it exercises their landed seams.

## Success criteria

The four 0233 asks are covered; the 0254 release loop and 0253 posture emission have regression
tests; issue 0233 resolved with the resume caveat routed to c1-8. Suite green.
