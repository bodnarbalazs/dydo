---
area: general
type: changelog
date: 2026-05-21
---

# Task: identity-hijack-f13-windows-replan

(No description)

## Progress

- [x] Investigated WindowsTerminalLauncher (dispatch + resume paths), Linux/Mac launchers, F13 finding, Dexter's plan, Slice A review.
- [x] Wrote the corrected F13/#0197 Windows plan: `dydo/agents/Brian/plan-f13-windows.md`.
- [ ] User sign-off (via Adele).
- [ ] Frank implements per the plan.

## Plan

See `dydo/agents/Brian/plan-f13-windows.md` — amends §F13/#0197 (Windows portion) of Dexter's plan.

**Chosen mechanism:** `-NoProfile` + in-`-Command` controlled profile re-source. The
`DYDO_AGENT` pin is the first `-Command` statement; profiles are then re-sourced so they
observe the correct value. Fix is entirely in the command string — no `ProcessStartInfo`
change, `UseShellExecute=true` kept, `psi.Environment` never touched on Windows.

**Why not the candidates:** `psi.Environment`/parent-env pins don't reach a tab opened in
a pre-existing Windows Terminal window (monarch/peasant model); `UseShellExecute=false`
also regresses the non-wt fallback's visible window. `-NoProfile` alone drops profile
customisations. No `wt`-native `--env` exists.

## Files Changed

(None — planning only. Implementation: `Services/WindowsTerminalLauncher.cs` +
`DynaDocs.Tests/Services/TerminalLauncherTests.cs`, per the plan.)

## Review Summary

(Pending — awaiting user sign-off, then Frank implements.)

## Approval

- Approved: 2026-05-21 19:06
