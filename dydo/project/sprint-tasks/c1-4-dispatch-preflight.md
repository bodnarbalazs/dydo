---
title: c1-4 Dispatch Preflight Fail-Fast Checks
blocked-by:
due:
needs-human: false
priority: High
sprint: c1-codex-adoption
status: ready
work-type: feature
area: backend
type: context
---

# c1-4 Dispatch Preflight Fail-Fast Checks

Issue 0239 generalized: a dispatch that cannot succeed must fail at dispatch time with an
actionable message — not a downstream child-terminal `CommandNotFoundException` (today's smoke:
bare-name fallback) or a stale `Dispatched` reservation the watchdog reclaims. Expected shape per
the issue/DR 037 §6: name (1) the missing prerequisite and (2) the fix.

## Behavior

`dydo dispatch` runs a preflight for the resolved target vendor before reserving/launching:

1. **Executable resolvable** — extend `DispatchService.CanResolveLaunchExecutable`
   (DispatchService.cs:87-100, wrapping `TerminalLauncher.GetLaunchExecutable`): failure names
   the binary and the fix (install the CLI / PATH). Kill the bare-name fallback that today defers
   the failure into the child terminal.
2. **Vendor configured** — override targets a vendor with `integrations.<vendor>: false` or
   missing tier mapping / synced bodies → fail fast naming the prerequisite and fix (enable the
   integration, add the mapping, run `dydo sync`). Symmetric for `--claude` in a codex-only
   project (tri-modal, DR 037).
3. **Windows sandbox prerequisite (codex)** — the configured posture needs the codex Windows
   sandbox; when its prerequisite is missing (the smoke's absent `codex-windows-sandbox-setup.exe`),
   fail fast pointing at c1-3's documented setup. **Never silently degrade the sandbox** (balazs,
   co-think outcome 4).
4. **Hook trust (codex)** — the repo's `.codex/hooks.json` must be trust-enabled in the
   user-level codex config (`[hooks.state]`, path-keyed, SHA256-pinned — Noah's probe findings in
   `backlog/codex-mcp-delegation-experiment.md`); untrusted hooks are SILENTLY skipped, i.e. an
   unguarded agent. Untrusted/stale-hash → fail fast with the re-trust instruction. Trust-check
   must key on the path codex actually resolves (sandbox path-virtualization wrinkle — verify
   against live behavior in c1-8).

All four produce distinct, testable error messages; no reservation is made on preflight failure.

## Files

- `Services/DispatchPreflight.cs` — NEW: the four checks, one result type.
- `Services/DispatchService.cs` — call preflight in `Execute` before reservation (40-44 area);
  extend/replace `CanResolveLaunchExecutable`.
- Tests: NEW `DynaDocs.Tests/Services/DispatchPreflightTests.cs` (each check's pass/fail +
  message content); `DynaDocs.Tests/Integration/DispatchCommandTests.cs` — no-reservation-on-
  failure regression.
- Explicitly NOT touched: `Commands/DispatchCommand.cs` (c1-5's file), `TerminalLauncher.cs`
  (c1-3's file — preflight consumes its existing public resolution seam).

## Gates (exact commands)

- `python DynaDocs.Tests/coverage/run_tests.py`
- `DynaDocs.Tests/coverage/gap_check.py --force-run`
- `dydo check`

## Sequencing

Parallel-safe with every other slice (file-disjoint by construction above; c1-5 owns
`Commands/DispatchCommand.cs`, this row owns `Services/DispatchService.cs`).

## Success criteria

Dispatching toward a missing binary, unconfigured vendor, missing Windows sandbox, or untrusted
hooks fails in the dispatcher's terminal with the prerequisite + fix named; no stale
reservations; today's child-terminal CommandNotFoundException is unreachable. Issue 0239
resolved. Suite green.
