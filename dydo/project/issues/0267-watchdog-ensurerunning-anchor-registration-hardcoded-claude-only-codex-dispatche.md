---
title: Watchdog EnsureRunning anchor registration hardcoded claude-only - codex dispatcher registers no codex anchor on the dispatch path
id: 267
area: backend
type: issue
severity: low
status: open
found-by: audit
found-by-agent: Grace
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-10
---

# Watchdog EnsureRunning anchor registration hardcoded claude-only - codex dispatcher registers no codex anchor on the dispatch path

`WatchdogService.EnsureRunning` registers its anchor via
`RegisterAnchor(dydoRoot, ProcessUtils.FindClaudeAncestor())` (WatchdogService.cs:123, reached
from DispatchService on `--auto-close`), so a dispatch issued FROM a codex-owned session
registers no codex host anchor on that path. Found by the C1 wave-4 sprint audit while verifying
issue 0233 ask 2.

## Impact (bounded)

Claim-time anchoring IS host-aware (`AgentRegistry.cs:558` via `FindAgentHostAncestor(host)`,
regression-tested in `CodexClaimE2ETests`), so a claimed codex dispatcher's host PID is already
anchored in the standard flow. The gap is the dispatch-path re-registration only; no test pins
that surface for codex (a test written to 0233 ask 2's literal wording would fail today).

## Fix direction

Make `EnsureRunning`'s anchor registration host-aware (same `FindAgentHostAncestor(host)` chain
claim uses) + the dispatch/EnsureRunning codex regression test that 0233 ask 2 originally asked
for. Candidate for the post-2.0.7 codex workhorse batch (W1).

## Related

- Issue 0233 (resolved) — its Resolution names this residual explicitly.
- c1-8 live smoke — exercises auto-close from a Claude dispatcher only; not a blocker for it.

## Resolution

(Filled when resolved)
